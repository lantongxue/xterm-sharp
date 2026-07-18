using System.Collections.Immutable;
using System.Threading.Channels;
using XtermSharp.Internal;
using XtermSharp.Internal.Parser;

namespace XtermSharp;

/// <summary>A headless, ordered terminal emulator.</summary>
public sealed class Terminal : IDisposable, IAsyncDisposable
{
    private readonly ITerminalLogger _logger;
    private readonly UnicodeRegistry _unicode;
    private readonly ParserRegistry _parser;
    private readonly TerminalEngine _engine;
    private readonly PendingByteLimiter _inputLimiter;
    private readonly Channel<TerminalCommand> _commands;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _processor;
    private readonly object _addonGate = new();
    private readonly List<ITerminalAddon> _addons = [];
    private readonly object _linkProviderGate = new();
    private readonly List<ITerminalLinkProvider> _linkProviders = [];
    private readonly object _decorationProviderGate = new();
    private readonly List<ITerminalDecorationProvider> _decorationProviders = [];
    private readonly object _selectionGate = new();
    private readonly object _markerGate = new();
    private readonly List<TerminalMarker> _markers = [];
    private TerminalSnapshot _latestSnapshot;
    private TerminalSelectionRange? _selection;
    private TerminalOptions _options;
    private int _columns;
    private int _rows;
    private long _revision;
    private int _disposeState;

    public Terminal(TerminalOptions? options = null)
    {
        TerminalOptions validated = (options ?? new TerminalOptions()).ValidateAndClone();
        _logger = validated.Logger ?? NullTerminalLogger.Instance;
        _unicode = new UnicodeRegistry(validated.UnicodeVersion);
        var parserCore = new EscapeSequenceParser();
        _engine = new TerminalEngine(validated, _unicode, parserCore);
        _parser = new ParserRegistry(parserCore);
        _inputLimiter = new PendingByteLimiter(validated.MaxPendingInputBytes);
        _commands = Channel.CreateUnbounded<TerminalCommand>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
        _columns = _engine.Columns;
        _rows = _engine.Rows;
        _options = validated;
        _latestSnapshot = _engine.CreateSnapshot(0, SnapshotScope.AllBuffers);
        _linkProviders.Add(new OscLinkProvider(this));
        _processor = ProcessCommandsAsync();
    }

    public int Columns => Volatile.Read(ref _columns);
    public int Rows => Volatile.Read(ref _rows);
    public long Revision => Interlocked.Read(ref _revision);
    public ITerminalParser Parser
    {
        get
        {
            ThrowIfProposedApiDisabled();
            return _parser;
        }
    }
    public ITerminalUnicode Unicode
    {
        get
        {
            ThrowIfProposedApiDisabled();
            return _unicode;
        }
    }
    public TerminalOptions Options => Volatile.Read(ref _options);
    public bool IsDisposed => Volatile.Read(ref _disposeState) != 0;
    internal OscLinkData? GetLinkDataForDiagnostics(int linkId) => _engine.GetLinkData(linkId);
    public TerminalSelectionRange? Selection
    {
        get
        {
            lock (_selectionGate)
            {
                return _selection;
            }
        }
    }
    public IReadOnlyList<TerminalDecoration> Decorations
    {
        get
        {
            ITerminalDecorationProvider[] providers;
            lock (_decorationProviderGate)
            {
                providers = _decorationProviders.ToArray();
            }

            var result = new List<TerminalDecoration>();
            foreach (ITerminalDecorationProvider provider in providers)
            {
                try
                {
                    IReadOnlyList<TerminalDecoration> decorations = provider.Decorations;
                    result.AddRange(decorations.Where(decoration => decoration is not null));
                }
                catch (Exception exception)
                {
                    _logger.Log(TerminalLogLevel.Error, "A terminal decoration provider threw an exception.", exception);
                }
            }
            return result;
        }
    }
    public TerminalModes Modes
    {
        get
        {
            ThrowIfProposedApiDisabled();
            return Volatile.Read(ref _latestSnapshot).Modes;
        }
    }
    public TerminalBufferCollection Buffer
    {
        get
        {
            ThrowIfProposedApiDisabled();
            TerminalSnapshot snapshot = Volatile.Read(ref _latestSnapshot);
            return new TerminalBufferCollection(
                snapshot.ActiveBuffer,
                snapshot.NormalBuffer ?? snapshot.ActiveBuffer,
                snapshot.AlternateBuffer ?? EmptyAlternateBuffer(snapshot));
        }
    }
    public IReadOnlyList<TerminalMarker> Markers
    {
        get
        {
            lock (_markerGate)
            {
                return _markers.ToArray();
            }
        }
    }

    public event EventHandler<TerminalEventArgs>? Bell;
    public event EventHandler<TerminalDataEventArgs>? Data;
    public event EventHandler<TerminalDataEventArgs>? Binary;
    public event EventHandler<TerminalEventArgs>? CursorMoved;
    public event EventHandler<TerminalEventArgs>? LineFeed;
    public event EventHandler<TerminalRenderEventArgs>? RenderRequested;
    public event EventHandler<TerminalResizeEventArgs>? Resized;
    public event EventHandler<TerminalScrollEventArgs>? Scrolled;
    public event EventHandler<TerminalTitleChangedEventArgs>? TitleChanged;
    public event EventHandler<TerminalColorRequestEventArgs>? ColorRequested;
    public event EventHandler<TerminalOptionsChangedEventArgs>? OptionsChanged;
    public event EventHandler<TerminalEventArgs>? WriteParsed;
    public event EventHandler<EventArgs>? SelectionChanged;
    public event EventHandler<EventArgs>? DecorationsChanged;

    /// <summary>Raised when an OSC 8 link is hovered in an interactive adapter.</summary>
    public event EventHandler<TerminalHyperlinkEventArgs>? HyperlinkHovered;

    /// <summary>Raised when the pointer leaves a previously hovered OSC 8 link.</summary>
    public event EventHandler<TerminalHyperlinkEventArgs>? HyperlinkLeft;

    /// <summary>
    /// Raised when an OSC 8 link is activated. XtermSharp never opens the URI automatically;
    /// applications must treat terminal-provided URIs as untrusted and explicitly validate them.
    /// </summary>
    public event EventHandler<TerminalHyperlinkEventArgs>? HyperlinkActivated;

    public ValueTask WriteAsync(string data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        long weight = Math.Max(1, (long)data.Length * sizeof(char));
        return EnqueueMutationAsync(engine => engine.WriteAsync(data), true, weight, cancellationToken);
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> utf8Data, CancellationToken cancellationToken = default)
    {
        byte[] copy = utf8Data.ToArray();
        return EnqueueMutationAsync(engine => engine.WriteAsync(copy), true, Math.Max(1, copy.Length), cancellationToken);
    }

    public async ValueTask WriteAsync(
        string data,
        Action callback,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(callback);
        long weight = Math.Max(1, (long)data.Length * sizeof(char));
        await EnqueueMutationAsync(
            engine => engine.WriteAsync(data),
            true,
            weight,
            cancellationToken,
            callback).ConfigureAwait(false);
    }

    public async ValueTask WriteAsync(
        ReadOnlyMemory<byte> utf8Data,
        Action callback,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);
        byte[] copy = utf8Data.ToArray();
        await EnqueueMutationAsync(
            engine => engine.WriteAsync(copy),
            true,
            Math.Max(1, copy.Length),
            cancellationToken,
            callback).ConfigureAwait(false);
    }

    public ValueTask WriteLineAsync(string data, CancellationToken cancellationToken = default) =>
        WriteAsync(string.Concat(data, "\r\n"), cancellationToken);

    public ValueTask WriteLineAsync(ReadOnlyMemory<byte> utf8Data, CancellationToken cancellationToken = default)
    {
        byte[] data = new byte[utf8Data.Length + 2];
        utf8Data.Span.CopyTo(data);
        data[^2] = (byte)'\r';
        data[^1] = (byte)'\n';
        return WriteAsync(data, cancellationToken);
    }

    public async ValueTask WriteLineAsync(
        string data,
        Action callback,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(callback);
        string line = string.Concat(data, "\r\n");
        long weight = Math.Max(1, (long)line.Length * sizeof(char));
        await EnqueueMutationAsync(
            engine => engine.WriteAsync(line),
            true,
            weight,
            cancellationToken,
            callback).ConfigureAwait(false);
    }

    public async ValueTask WriteLineAsync(
        ReadOnlyMemory<byte> utf8Data,
        Action callback,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);
        byte[] data = new byte[utf8Data.Length + 2];
        utf8Data.Span.CopyTo(data);
        data[^2] = (byte)'\r';
        data[^1] = (byte)'\n';
        await EnqueueMutationAsync(
            engine => engine.WriteAsync(data),
            true,
            Math.Max(1, data.Length),
            cancellationToken,
            callback).ConfigureAwait(false);
    }

    public ValueTask SendInputAsync(string data, bool wasUserInput = true, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        return EnqueueMutationAsync(
            engine =>
            {
                engine.SendInput(data, wasUserInput);
                return ValueTask.CompletedTask;
            },
            false,
            0,
            cancellationToken);
    }

    public ValueTask PasteAsync(string data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        return EnqueueMutationAsync(
            engine =>
            {
                engine.Paste(data);
                return ValueTask.CompletedTask;
            },
            false,
            0,
            cancellationToken);
    }

    public ValueTask SendFocusAsync(bool focused, CancellationToken cancellationToken = default) =>
        EnqueueMutationAsync(
            engine =>
            {
                engine.SendFocus(focused);
                return ValueTask.CompletedTask;
            },
            false,
            0,
            cancellationToken);

    public ValueTask SendMouseAsync(TerminalMouseEvent value, CancellationToken cancellationToken = default) =>
        EnqueueMutationAsync(
            engine =>
            {
                engine.SendMouse(value);
                return ValueTask.CompletedTask;
            },
            false,
            0,
            cancellationToken);

    public ValueTask SendKeyAsync(TerminalKeyEvent value, CancellationToken cancellationToken = default) =>
        EnqueueMutationAsync(
            engine =>
            {
                engine.SendKey(value);
                return ValueTask.CompletedTask;
            },
            false,
            0,
            cancellationToken);

    public ValueTask ResizeAsync(int columns, int rows, CancellationToken cancellationToken = default) =>
        EnqueueMutationAsync(
            engine =>
            {
                engine.Resize(columns, rows);
                return ValueTask.CompletedTask;
            },
            false,
            0,
            cancellationToken);

    public ValueTask ResetAsync(CancellationToken cancellationToken = default) =>
        EnqueueMutationAsync(engine => { engine.Reset(); return ValueTask.CompletedTask; }, false, 0, cancellationToken);

    public ValueTask ClearAsync(CancellationToken cancellationToken = default) =>
        EnqueueMutationAsync(engine => { engine.Clear(); return ValueTask.CompletedTask; }, false, 0, cancellationToken);

    public ValueTask UpdateOptionsAsync(
        TerminalOptionsUpdate update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        return EnqueueMutationAsync(
            engine =>
            {
                TerminalOptions updated = engine.UpdateOptions(update);
                Volatile.Write(ref _options, updated);
                return ValueTask.CompletedTask;
            },
            false,
            0,
            cancellationToken);
    }

    public ValueTask ScrollLinesAsync(int amount, CancellationToken cancellationToken = default) =>
        EnqueueMutationAsync(engine => { engine.ScrollLines(amount); return ValueTask.CompletedTask; }, false, 0, cancellationToken);

    public ValueTask ScrollPagesAsync(int pageCount, CancellationToken cancellationToken = default) =>
        ScrollLinesAsync(pageCount * Rows, cancellationToken);

    public ValueTask ScrollToTopAsync(CancellationToken cancellationToken = default) =>
        ScrollToLineAsync(0, cancellationToken);

    public ValueTask ScrollToBottomAsync(CancellationToken cancellationToken = default) =>
        EnqueueMutationAsync(engine => { engine.ScrollToBottom(); return ValueTask.CompletedTask; }, false, 0, cancellationToken);

    public ValueTask ScrollToLineAsync(int line, CancellationToken cancellationToken = default) =>
        EnqueueMutationAsync(engine => { engine.ScrollTo(line); return ValueTask.CompletedTask; }, false, 0, cancellationToken);

    public async ValueTask<TerminalSnapshot> GetSnapshotAsync(
        SnapshotScope scope = SnapshotScope.Viewport,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        var command = new SnapshotCommand(scope);
        if (!_commands.Writer.TryWrite(command))
        {
            ThrowIfDisposed();
            throw new InvalidOperationException("The terminal command queue is closed.");
        }
        return await command.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Gets the latest committed immutable snapshot without waiting for queued commands.</summary>
    public TerminalSnapshot GetCurrentSnapshot(SnapshotScope scope = SnapshotScope.Viewport)
    {
        ThrowIfDisposed();
        TerminalSnapshot snapshot = Volatile.Read(ref _latestSnapshot);
        if (scope == SnapshotScope.AllBuffers)
        {
            return snapshot;
        }

        TerminalBufferSnapshot active = snapshot.ActiveBuffer;
        if (scope == SnapshotScope.Viewport)
        {
            int start = Math.Clamp(active.ViewportY, 0, active.Lines.Length);
            int count = Math.Min(snapshot.Rows, active.Lines.Length - start);
            ImmutableArray<TerminalLineSnapshot> lines = active.Lines
                .Skip(start)
                .Take(count)
                .ToImmutableArray();
            active = active with { Lines = lines };
        }
        return snapshot with
        {
            ActiveBuffer = active,
            NormalBuffer = null,
            AlternateBuffer = null,
            Hyperlinks = FilterHyperlinks(snapshot.Hyperlinks, active)
        };
    }

    public void Select(int column, int row, int length)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(column, Columns);
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        int linearEnd = checked(column + length);
        SetSelection(new TerminalSelectionRange(
            column,
            row,
            linearEnd % Columns,
            checked(row + linearEnd / Columns)));
    }

    public void SetSelection(TerminalSelectionRange? selection)
    {
        ThrowIfDisposed();
        TerminalSelectionRange? normalized = selection?.Normalize();
        bool changed;
        lock (_selectionGate)
        {
            changed = _selection != normalized;
            _selection = normalized;
        }
        if (changed)
        {
            Raise(SelectionChanged, EventArgs.Empty);
        }
    }

    public void ClearSelection() => SetSelection(null);

    public async ValueTask<TerminalMarker> RegisterMarkerAsync(
        int cursorYOffset = 0,
        CancellationToken cancellationToken = default)
    {
        TerminalMarker marker = await EnqueueResultMutationAsync(
            engine => engine.RegisterMarker(cursorYOffset),
            cancellationToken).ConfigureAwait(false);
        marker.Disposed += RemoveMarker;
        lock (_markerGate)
        {
            if (!marker.IsDisposed)
            {
                _markers.Add(marker);
            }
        }
        return marker;
    }

    public IDisposable RegisterLinkProvider(ITerminalLinkProvider linkProvider)
    {
        ArgumentNullException.ThrowIfNull(linkProvider);
        ThrowIfDisposed();
        lock (_linkProviderGate)
        {
            ThrowIfDisposed();
            _linkProviders.Add(linkProvider);
        }
        return new DelegateDisposable(() =>
        {
            lock (_linkProviderGate)
            {
                _linkProviders.Remove(linkProvider);
            }
        });
    }

    public IDisposable RegisterDecorationProvider(ITerminalDecorationProvider decorationProvider)
    {
        ArgumentNullException.ThrowIfNull(decorationProvider);
        ThrowIfDisposed();
        lock (_decorationProviderGate)
        {
            ThrowIfDisposed();
            _decorationProviders.Add(decorationProvider);
            decorationProvider.DecorationsChanged += OnDecorationProviderChanged;
        }
        Raise(DecorationsChanged, EventArgs.Empty);
        return new DelegateDisposable(() =>
        {
            bool removed;
            lock (_decorationProviderGate)
            {
                removed = _decorationProviders.Remove(decorationProvider);
                if (removed)
                {
                    decorationProvider.DecorationsChanged -= OnDecorationProviderChanged;
                }
            }
            if (removed && !IsDisposed)
            {
                Raise(DecorationsChanged, EventArgs.Empty);
            }
        });
    }

    public async ValueTask<IReadOnlyList<TerminalLink>> GetLinksAsync(
        int bufferLineNumber,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfLessThan(bufferLineNumber, 1);
        cancellationToken.ThrowIfCancellationRequested();

        ITerminalLinkProvider[] providers;
        lock (_linkProviderGate)
        {
            providers = _linkProviders.ToArray();
        }
        if (providers.Length == 0)
        {
            return Array.Empty<TerminalLink>();
        }

        var result = new List<TerminalLink>();
        foreach (ITerminalLinkProvider provider in providers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<TerminalLink>? links;
            try
            {
                links = await provider.ProvideLinksAsync(bufferLineNumber, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.Log(TerminalLogLevel.Error, "A terminal link provider threw an exception.", exception);
                continue;
            }
            if (links is not null)
            {
                result.AddRange(links.Where(link => link is not null));
            }
        }
        return result;
    }

    public async ValueTask<TerminalLink?> GetLinkAtAsync(
        int column,
        int bufferLineNumber,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfLessThan(column, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(bufferLineNumber, 1);
        cancellationToken.ThrowIfCancellationRequested();

        ITerminalLinkProvider[] providers;
        lock (_linkProviderGate)
        {
            providers = _linkProviders.ToArray();
        }
        foreach (ITerminalLinkProvider provider in providers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<TerminalLink>? links;
            try
            {
                links = await provider.ProvideLinksAsync(bufferLineNumber, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.Log(TerminalLogLevel.Error, "A terminal link provider threw an exception.", exception);
                continue;
            }

            TerminalLink? link = links?.FirstOrDefault(candidate =>
                candidate is not null && candidate.Range.Contains(column, bufferLineNumber, Columns));
            if (link is not null)
            {
                return link;
            }
        }
        return null;
    }

    public void LoadAddon(ITerminalAddon addon)
    {
        ArgumentNullException.ThrowIfNull(addon);
        ThrowIfDisposed();
        addon.Activate(this);
        lock (_addonGate)
        {
            if (Volatile.Read(ref _disposeState) != 0)
            {
                addon.Dispose();
                ThrowIfDisposed();
            }
            _addons.Add(addon);
        }
    }

    public void Dispose() => BeginDispose();

    public async ValueTask DisposeAsync()
    {
        BeginDispose();
        await _processor.ConfigureAwait(false);
        _shutdown.Dispose();
    }

    private async ValueTask EnqueueMutationAsync(
        Func<TerminalEngine, ValueTask> action,
        bool isWrite,
        long inputWeight,
        CancellationToken cancellationToken,
        Action? callback = null)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        PendingByteLease? lease = null;
        if (inputWeight > 0)
        {
            lease = await _inputLimiter.AcquireAsync(inputWeight, cancellationToken).ConfigureAwait(false);
        }

        var command = new MutationCommand(action, isWrite, lease, callback);
        if (Volatile.Read(ref _disposeState) != 0 || !_commands.Writer.TryWrite(command))
        {
            lease?.Dispose();
            ThrowIfDisposed();
            throw new InvalidOperationException("The terminal command queue is closed.");
        }

        // Cancellation intentionally stops at queue admission to preserve stream ordering.
        await command.Task.ConfigureAwait(false);
    }

    private async ValueTask<TResult> EnqueueResultMutationAsync<TResult>(
        Func<TerminalEngine, TResult> action,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        var command = new ResultMutationCommand<TResult>(action);
        if (Volatile.Read(ref _disposeState) != 0 || !_commands.Writer.TryWrite(command))
        {
            ThrowIfDisposed();
            throw new InvalidOperationException("The terminal command queue is closed.");
        }
        return await command.Task.ConfigureAwait(false);
    }

    private async Task ProcessCommandsAsync()
    {
        Exception disposalException = new ObjectDisposedException(nameof(Terminal));
        try
        {
            while (await _commands.Reader.WaitToReadAsync(_shutdown.Token).ConfigureAwait(false))
            {
                while (_commands.Reader.TryRead(out TerminalCommand? command))
                {
                    bool executionCompleted = false;
                    try
                    {
                        long currentRevision = Interlocked.Read(ref _revision);
                        await command.ExecuteAsync(_engine, currentRevision).ConfigureAwait(false);
                        executionCompleted = true;
                        if (command.MutatesState)
                        {
                            CommitEngineState(command.IsWrite);
                        }
                        command.Complete();
                    }
                    catch (Exception exception)
                    {
                        if (command.IsWrite && !executionCompleted)
                        {
                            try
                            {
                                CommitEngineState(includeWriteParsed: false);
                            }
                            catch (Exception commitException)
                            {
                                exception.Data["XtermSharp.FailedWriteCommitException"] = commitException;
                            }
                        }
                        _logger.Log(TerminalLogLevel.Error, "A terminal command failed.", exception);
                        command.Fail(exception);
                    }
                    finally
                    {
                        command.Lease?.Dispose();
                    }
                }
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
        }
        finally
        {
            while (_commands.Reader.TryRead(out TerminalCommand? command))
            {
                command.Lease?.Dispose();
                command.Fail(disposalException);
            }
            DisposeAddons();
            _engine.Dispose();
        }
    }

    private void CommitEngineState(bool includeWriteParsed)
    {
        long revision = Interlocked.Increment(ref _revision);
        Volatile.Write(ref _options, _engine.Options);
        Volatile.Write(ref _columns, _engine.Columns);
        Volatile.Write(ref _rows, _engine.Rows);
        Volatile.Write(
            ref _latestSnapshot,
            _engine.CreateSnapshot(revision, SnapshotScope.AllBuffers));
        DispatchEvents(_engine.ConsumeEvents(includeWriteParsed), revision);
    }

    private void DispatchEvents(IReadOnlyList<EngineEvent> events, long revision)
    {
        foreach (EngineEvent terminalEvent in events)
        {
            switch (terminalEvent.Kind)
            {
                case EngineEventKind.Bell:
                    Raise(Bell, new TerminalEventArgs(revision));
                    break;
                case EngineEventKind.Data:
                    Raise(Data, new TerminalDataEventArgs(revision, terminalEvent.Text ?? string.Empty));
                    break;
                case EngineEventKind.Binary:
                    Raise(Binary, new TerminalDataEventArgs(revision, terminalEvent.Text ?? string.Empty, true));
                    break;
                case EngineEventKind.CursorMoved:
                    Raise(CursorMoved, new TerminalEventArgs(revision));
                    break;
                case EngineEventKind.LineFeed:
                    Raise(LineFeed, new TerminalEventArgs(revision));
                    break;
                case EngineEventKind.Render:
                    Raise(RenderRequested, new TerminalRenderEventArgs(revision, terminalEvent.First, terminalEvent.Second));
                    break;
                case EngineEventKind.Resize:
                    Raise(Resized, new TerminalResizeEventArgs(revision, terminalEvent.First, terminalEvent.Second));
                    break;
                case EngineEventKind.Scroll:
                    Raise(Scrolled, new TerminalScrollEventArgs(revision, terminalEvent.First));
                    break;
                case EngineEventKind.TitleChanged:
                    Raise(TitleChanged, new TerminalTitleChangedEventArgs(revision, terminalEvent.Text ?? string.Empty));
                    break;
                case EngineEventKind.ColorRequest:
                    Raise(
                        ColorRequested,
                        new TerminalColorRequestEventArgs(
                            revision,
                            terminalEvent.ColorRequests ?? Array.Empty<TerminalColorRequest>()));
                    break;
                case EngineEventKind.OptionsChanged:
                    Raise(
                        OptionsChanged,
                        new TerminalOptionsChangedEventArgs(
                            revision,
                            terminalEvent.PreviousOptions!,
                            terminalEvent.CurrentOptions!));
                    break;
                case EngineEventKind.WriteParsed:
                    Raise(WriteParsed, new TerminalEventArgs(revision));
                    break;
            }
        }
    }

    private void Raise<TEventArgs>(EventHandler<TEventArgs>? handlers, TEventArgs args)
        where TEventArgs : EventArgs
    {
        if (handlers is null)
        {
            return;
        }
        foreach (EventHandler<TEventArgs> handler in handlers.GetInvocationList().Cast<EventHandler<TEventArgs>>())
        {
            try
            {
                handler(this, args);
            }
            catch (Exception exception)
            {
                _logger.Log(TerminalLogLevel.Error, "A terminal event subscriber threw an exception.", exception);
            }
        }
    }

    private void BeginDispose()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }
        _inputLimiter.Dispose();
        lock (_linkProviderGate)
        {
            _linkProviders.Clear();
        }
        lock (_decorationProviderGate)
        {
            foreach (ITerminalDecorationProvider provider in _decorationProviders)
            {
                provider.DecorationsChanged -= OnDecorationProviderChanged;
            }
            _decorationProviders.Clear();
        }
        _commands.Writer.TryComplete();
        _shutdown.Cancel();
    }

    private void DisposeAddons()
    {
        ITerminalAddon[] addons;
        lock (_addonGate)
        {
            addons = _addons.ToArray();
            _addons.Clear();
        }
        Array.Reverse(addons);
        foreach (ITerminalAddon addon in addons)
        {
            try
            {
                addon.Dispose();
            }
            catch (Exception exception)
            {
                _logger.Log(TerminalLogLevel.Error, "A terminal addon failed during disposal.", exception);
            }
        }
    }

    private void RemoveMarker(object? sender, EventArgs args)
    {
        if (sender is not TerminalMarker marker)
        {
            return;
        }
        marker.Disposed -= RemoveMarker;
        lock (_markerGate)
        {
            _markers.Remove(marker);
        }
    }

    private void OnDecorationProviderChanged(object? sender, EventArgs args)
    {
        if (!IsDisposed)
        {
            Raise(DecorationsChanged, EventArgs.Empty);
        }
    }

    internal void NotifyHyperlinkHovered(TerminalLinkEvent terminalEvent, TerminalHyperlinkMetadata hyperlink) =>
        Raise(HyperlinkHovered, new TerminalHyperlinkEventArgs(terminalEvent, hyperlink));

    internal void NotifyHyperlinkLeft(TerminalLinkEvent terminalEvent, TerminalHyperlinkMetadata hyperlink) =>
        Raise(HyperlinkLeft, new TerminalHyperlinkEventArgs(terminalEvent, hyperlink));

    internal void NotifyHyperlinkActivated(TerminalLinkEvent terminalEvent, TerminalHyperlinkMetadata hyperlink) =>
        Raise(HyperlinkActivated, new TerminalHyperlinkEventArgs(terminalEvent, hyperlink));

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) != 0, this);

    private void ThrowIfProposedApiDisabled()
    {
        ThrowIfDisposed();
        if (!Options.AllowProposedApi)
        {
            throw new InvalidOperationException(
                "You must set the allowProposedApi option to true to use proposed API");
        }
    }

    private static TerminalBufferSnapshot EmptyAlternateBuffer(TerminalSnapshot snapshot) => new(
        TerminalBufferKind.Alternate,
        0,
        0,
        0,
        0,
        []);

    private static ImmutableDictionary<int, TerminalHyperlinkMetadata> FilterHyperlinks(
        ImmutableDictionary<int, TerminalHyperlinkMetadata> hyperlinks,
        TerminalBufferSnapshot buffer)
    {
        if (hyperlinks.Count == 0)
        {
            return hyperlinks;
        }
        var linkIds = new HashSet<int>();
        foreach (TerminalLineSnapshot line in buffer.Lines)
        {
            foreach (TerminalCellSnapshot cell in line.Cells)
            {
                if (cell.HyperlinkId != 0)
                {
                    linkIds.Add(cell.HyperlinkId);
                }
            }
        }
        return hyperlinks.Where(entry => linkIds.Contains(entry.Key)).ToImmutableDictionary();
    }
}

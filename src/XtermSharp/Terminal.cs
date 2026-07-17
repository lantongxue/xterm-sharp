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
    private readonly object _markerGate = new();
    private readonly List<TerminalMarker> _markers = [];
    private TerminalSnapshot _latestSnapshot;
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
}

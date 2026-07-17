namespace XtermSharp.Addons.Search;

/// <summary>Searches, selects and optionally decorates matches in the active terminal buffer.</summary>
public sealed class SearchAddon : ITerminalAddon
{
    private readonly object _gate = new();
    private readonly int _highlightLimit;
    private readonly SearchState _state = new();
    private readonly SearchLineCache _lineCache = new();
    private readonly SearchDecorationProvider _decorationProvider = new();
    private readonly SearchResultTracker _resultTracker = new();
    private SearchEngine? _engine;
    private Terminal? _terminal;
    private IDisposable? _decorationRegistration;
    private CancellationTokenSource? _highlightCancellation;
    private bool _disposed;

    public SearchAddon(SearchAddonOptions? options = null)
    {
        _highlightLimit = options?.HighlightLimit ?? 1000;
    }

    public event EventHandler<EventArgs>? BeforeSearch;
    public event EventHandler<EventArgs>? AfterSearch;
    public event EventHandler<SearchResultChangedEventArgs>? ResultsChanged;

    public void Activate(Terminal terminal)
    {
        ArgumentNullException.ThrowIfNull(terminal);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            DetachTerminalLocked();
            _terminal = terminal;
            _engine = new SearchEngine(_lineCache);
            _decorationRegistration = terminal.RegisterDecorationProvider(_decorationProvider);
            terminal.WriteParsed += OnTerminalChanged;
            terminal.Resized += OnTerminalChanged;
        }
    }

    public bool FindNext(string term, SearchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(term);
        lock (_gate)
        {
            EnsureActivated();
            BeforeSearch?.Invoke(this, EventArgs.Empty);
            _state.LastSearchOptions = options;
            if (_state.ShouldUpdateHighlighting(term, options))
            {
                HighlightAllMatches(term, options!);
            }
            bool found = FindNextAndSelect(term, options, noScroll: false);
            FireResults(options);
            _state.CachedSearchTerm = term;
            AfterSearch?.Invoke(this, EventArgs.Empty);
            return found;
        }
    }

    public bool FindPrevious(string term, SearchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(term);
        lock (_gate)
        {
            EnsureActivated();
            BeforeSearch?.Invoke(this, EventArgs.Empty);
            _state.LastSearchOptions = options;
            if (_state.ShouldUpdateHighlighting(term, options))
            {
                HighlightAllMatches(term, options!);
            }
            bool found = FindPreviousAndSelect(term, options, noScroll: false);
            FireResults(options);
            _state.CachedSearchTerm = term;
            AfterSearch?.Invoke(this, EventArgs.Empty);
            return found;
        }
    }

    public void ClearDecorations()
    {
        lock (_gate)
        {
            ClearDecorationsLocked(retainCachedSearchTerm: false);
        }
    }

    public void ClearActiveDecoration()
    {
        lock (_gate)
        {
            _resultTracker.SelectedResult = null;
            _decorationProvider.ClearActive();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            DetachTerminalLocked();
        }
    }

    private void HighlightAllMatches(string term, SearchOptions options)
    {
        Terminal terminal = _terminal!;
        SearchEngine engine = _engine!;
        if (term.Length == 0)
        {
            ClearDecorationsLocked(retainCachedSearchTerm: false);
            return;
        }

        ClearDecorationsLocked(retainCachedSearchTerm: true);
        TerminalSnapshot snapshot = terminal.GetCurrentSnapshot(SnapshotScope.ActiveBuffer);
        var results = new List<SearchResult>();
        SearchResult? previous = null;
        SearchResult? result = engine.Find(snapshot, term, 0, 0, options);
        while (result is not null &&
               (previous is null || previous.Row != result.Row || previous.Column != result.Column))
        {
            if (results.Count >= _highlightLimit)
            {
                break;
            }
            previous = result;
            results.Add(result);
            int nextColumn = result.Column + result.Size;
            int nextRow = result.Row;
            if (nextColumn >= snapshot.Columns)
            {
                nextRow += nextColumn / snapshot.Columns;
                nextColumn %= snapshot.Columns;
            }
            result = engine.Find(snapshot, term, nextRow, nextColumn, options);
        }
        _resultTracker.UpdateResults(results, _highlightLimit);
        _decorationProvider.SetHighlights(results, options.Decorations!, snapshot.Columns);
    }

    private bool FindNextAndSelect(string term, SearchOptions? options, bool noScroll)
    {
        Terminal terminal = _terminal!;
        if (term.Length == 0)
        {
            terminal.ClearSelection();
            ClearDecorationsLocked(retainCachedSearchTerm: false);
            return false;
        }
        TerminalSelectionRange? previousSelection = terminal.Selection;
        terminal.ClearSelection();
        TerminalSnapshot snapshot = terminal.GetCurrentSnapshot(SnapshotScope.ActiveBuffer);
        SearchResult? result = _engine!.FindNextWithSelection(
            snapshot,
            term,
            options,
            _state.CachedSearchTerm,
            previousSelection);
        return SelectResult(result, options?.Decorations, snapshot, noScroll);
    }

    private bool FindPreviousAndSelect(string term, SearchOptions? options, bool noScroll)
    {
        Terminal terminal = _terminal!;
        if (term.Length == 0)
        {
            terminal.ClearSelection();
            ClearDecorationsLocked(retainCachedSearchTerm: false);
            return false;
        }
        TerminalSelectionRange? previousSelection = terminal.Selection;
        terminal.ClearSelection();
        TerminalSnapshot snapshot = terminal.GetCurrentSnapshot(SnapshotScope.ActiveBuffer);
        SearchResult? result = _engine!.FindPreviousWithSelection(
            snapshot,
            term,
            options,
            _state.CachedSearchTerm,
            previousSelection);
        return SelectResult(result, options?.Decorations, snapshot, noScroll);
    }

    private bool SelectResult(
        SearchResult? result,
        SearchDecorationOptions? decorationOptions,
        TerminalSnapshot snapshot,
        bool noScroll)
    {
        Terminal terminal = _terminal!;
        _resultTracker.SelectedResult = null;
        _decorationProvider.ClearActive();
        if (result is null)
        {
            terminal.ClearSelection();
            return false;
        }

        terminal.Select(result.Column, result.Row, result.Size);
        if (decorationOptions is not null)
        {
            _decorationProvider.SetActive(result, decorationOptions, snapshot.Columns);
            _resultTracker.SelectedResult = result;
        }
        if (!noScroll &&
            (result.Row >= snapshot.ActiveBuffer.ViewportY + snapshot.Rows ||
             result.Row < snapshot.ActiveBuffer.ViewportY))
        {
            int amount = result.Row - snapshot.ActiveBuffer.ViewportY - snapshot.Rows / 2;
            _ = ScrollWithoutThrowAsync(terminal, amount);
        }
        return true;
    }

    private void FireResults(SearchOptions? options)
    {
        if (options?.Decorations is not null)
        {
            ResultsChanged?.Invoke(this, _resultTracker.CreateEventArgs());
        }
    }

    private void ClearDecorationsLocked(bool retainCachedSearchTerm)
    {
        _resultTracker.SelectedResult = null;
        _decorationProvider.ClearActive();
        _decorationProvider.ClearHighlights();
        _resultTracker.ClearResults();
        if (!retainCachedSearchTerm)
        {
            _state.ClearCachedTerm();
        }
    }

    private void OnTerminalChanged(object? sender, TerminalEventArgs args)
    {
        _ = args;
        lock (_gate)
        {
            _highlightCancellation?.Cancel();
            _highlightCancellation?.Dispose();
            _highlightCancellation = null;
            if (_state.CachedSearchTerm is null || _state.LastSearchOptions?.Decorations is null)
            {
                return;
            }
            var cancellation = new CancellationTokenSource();
            _highlightCancellation = cancellation;
            _ = UpdateMatchesAfterDelayAsync(cancellation);
        }
    }

    private async Task UpdateMatchesAfterDelayAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellation.Token).ConfigureAwait(false);
            lock (_gate)
            {
                if (_disposed || !ReferenceEquals(_highlightCancellation, cancellation) ||
                    _terminal is null || _engine is null || _state.CachedSearchTerm is not string term ||
                    _state.LastSearchOptions is not SearchOptions options)
                {
                    return;
                }
                _state.ClearCachedTerm();
                SearchOptions updatedOptions = options with { Incremental = true };
                _state.LastSearchOptions = updatedOptions;
                HighlightAllMatches(term, updatedOptions);
                FindPreviousAndSelect(term, updatedOptions, noScroll: true);
                FireResults(updatedOptions);
                _state.CachedSearchTerm = term;
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _terminal?.Options.Logger?.Log(
                TerminalLogLevel.Error,
                "Failed to update terminal search matches.",
                exception);
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_highlightCancellation, cancellation))
                {
                    _highlightCancellation = null;
                }
            }
            cancellation.Dispose();
        }
    }

    private void DetachTerminalLocked()
    {
        _highlightCancellation?.Cancel();
        _highlightCancellation?.Dispose();
        _highlightCancellation = null;
        if (_terminal is not null)
        {
            _terminal.WriteParsed -= OnTerminalChanged;
            _terminal.Resized -= OnTerminalChanged;
        }
        ClearDecorationsLocked(retainCachedSearchTerm: false);
        _decorationRegistration?.Dispose();
        _decorationRegistration = null;
        _terminal = null;
        _engine = null;
    }

    private void EnsureActivated()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_terminal is null || _engine is null)
        {
            throw new InvalidOperationException("Cannot use addon until it has been loaded.");
        }
    }

    private static async Task ScrollWithoutThrowAsync(Terminal terminal, int amount)
    {
        try
        {
            await terminal.ScrollLinesAsync(amount).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception exception)
        {
            terminal.Options.Logger?.Log(TerminalLogLevel.Error, "Failed to scroll to a search result.", exception);
        }
    }
}

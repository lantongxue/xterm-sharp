namespace XtermSharp.Addons.Search;

internal sealed class SearchDecorationProvider : ITerminalDecorationProvider
{
    private readonly object _gate = new();
    private TerminalDecoration[] _highlights = [];
    private TerminalDecoration[] _active = [];
    private HashSet<int> _highlightedLines = [];

    public IReadOnlyList<TerminalDecoration> Decorations
    {
        get
        {
            lock (_gate)
            {
                if (_active.Length == 0)
                {
                    return _highlights;
                }
                var result = new TerminalDecoration[_highlights.Length + _active.Length];
                _highlights.CopyTo(result, 0);
                _active.CopyTo(result, _highlights.Length);
                return result;
            }
        }
    }

    public event EventHandler<EventArgs>? DecorationsChanged;

    public void SetHighlights(
        IReadOnlyList<SearchResult> results,
        SearchDecorationOptions options,
        int columns)
    {
        var decorations = new List<TerminalDecoration>();
        var highlightedLines = new HashSet<int>();
        foreach (SearchResult result in results)
        {
            foreach (TerminalDecorationRange range in SplitResult(result, columns))
            {
                bool firstOnLine = highlightedLines.Add(range.Line);
                decorations.Add(new TerminalDecoration(
                    range,
                    TerminalDecorationLayer.Bottom,
                    options.MatchBackground,
                    options.MatchBorder,
                    firstOnLine ? options.MatchOverviewRuler : null));
            }
        }
        lock (_gate)
        {
            _highlights = decorations.ToArray();
            _highlightedLines = highlightedLines;
        }
        DecorationsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetActive(SearchResult result, SearchDecorationOptions options, int columns)
    {
        TerminalDecoration[] decorations;
        lock (_gate)
        {
            decorations = SplitResult(result, columns)
                .Select(range => new TerminalDecoration(
                    range,
                    TerminalDecorationLayer.Top,
                    options.ActiveMatchBackground,
                    options.ActiveMatchBorder,
                    _highlightedLines.Contains(range.Line) ? null : options.ActiveMatchOverviewRuler))
                .ToArray();
            _active = decorations;
        }
        DecorationsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearHighlights()
    {
        bool changed;
        lock (_gate)
        {
            changed = _highlights.Length != 0;
            _highlights = [];
            _highlightedLines = [];
        }
        if (changed)
        {
            DecorationsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ClearActive()
    {
        bool changed;
        lock (_gate)
        {
            changed = _active.Length != 0;
            _active = [];
        }
        if (changed)
        {
            DecorationsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static IEnumerable<TerminalDecorationRange> SplitResult(SearchResult result, int columns)
    {
        int currentColumn = result.Column;
        int remaining = result.Size;
        int row = result.Row;
        while (remaining > 0)
        {
            int amount = Math.Min(columns - currentColumn, remaining);
            if (amount <= 0)
            {
                yield break;
            }
            yield return new TerminalDecorationRange(currentColumn, row, amount);
            currentColumn = 0;
            remaining -= amount;
            row++;
        }
    }
}

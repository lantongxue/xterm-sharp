namespace XtermSharp.Addons.Search;

internal sealed class SearchResultTracker
{
    private SearchResult[] _results = [];

    public SearchResult? SelectedResult { get; set; }

    public void UpdateResults(IReadOnlyList<SearchResult> results, int maximum) =>
        _results = results.Take(Math.Max(0, maximum)).ToArray();

    public void ClearResults() => _results = [];

    public SearchResultChangedEventArgs CreateEventArgs()
    {
        int index = -1;
        if (SelectedResult is SearchResult selected)
        {
            index = Array.FindIndex(
                _results,
                result => result.Row == selected.Row &&
                    result.Column == selected.Column &&
                    result.Size == selected.Size);
        }
        return new SearchResultChangedEventArgs(index, _results.Length);
    }
}

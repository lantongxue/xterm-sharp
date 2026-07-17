namespace XtermSharp.Addons.Search;

internal sealed class SearchState
{
    public string? CachedSearchTerm { get; set; }
    public SearchOptions? LastSearchOptions { get; set; }

    public bool ShouldUpdateHighlighting(string term, SearchOptions? options)
    {
        if (options?.Decorations is null)
        {
            return false;
        }
        return CachedSearchTerm is null ||
            !string.Equals(term, CachedSearchTerm, StringComparison.Ordinal) ||
            DidOptionsChange(options);
    }

    public void ClearCachedTerm() => CachedSearchTerm = null;

    private bool DidOptionsChange(SearchOptions? options)
    {
        if (LastSearchOptions is null)
        {
            return true;
        }
        if (options is null)
        {
            return false;
        }
        return LastSearchOptions.CaseSensitive != options.CaseSensitive ||
            LastSearchOptions.Regex != options.Regex ||
            LastSearchOptions.WholeWord != options.WholeWord;
    }
}

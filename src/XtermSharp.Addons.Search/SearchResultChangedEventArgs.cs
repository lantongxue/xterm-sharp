namespace XtermSharp.Addons.Search;

public sealed class SearchResultChangedEventArgs(int resultIndex, int resultCount) : EventArgs
{
    public int ResultIndex { get; } = resultIndex;
    public int ResultCount { get; } = resultCount;
}

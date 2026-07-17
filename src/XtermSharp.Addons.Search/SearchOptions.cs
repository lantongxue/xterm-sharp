namespace XtermSharp.Addons.Search;

/// <summary>Options for one forward or reverse terminal-buffer search.</summary>
public sealed record SearchOptions
{
    public bool Regex { get; init; }
    public bool WholeWord { get; init; }
    public bool CaseSensitive { get; init; }
    public bool Incremental { get; init; }
    public SearchDecorationOptions? Decorations { get; init; }
}

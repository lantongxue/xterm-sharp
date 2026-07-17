namespace XtermSharp.Addons.Search;

/// <summary>Options that configure a <see cref="SearchAddon"/> instance.</summary>
public sealed record SearchAddonOptions
{
    public int HighlightLimit { get; init; } = 1000;
}

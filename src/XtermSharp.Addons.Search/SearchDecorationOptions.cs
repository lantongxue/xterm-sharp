namespace XtermSharp.Addons.Search;

/// <summary>Colors used to decorate all matches and the active match.</summary>
public sealed record SearchDecorationOptions
{
    public TerminalColor? MatchBackground { get; init; }
    public TerminalColor? MatchBorder { get; init; }
    public TerminalColor? MatchOverviewRuler { get; init; }
    public TerminalColor? ActiveMatchBackground { get; init; }
    public TerminalColor? ActiveMatchBorder { get; init; }
    public TerminalColor? ActiveMatchOverviewRuler { get; init; }
}

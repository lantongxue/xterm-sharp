namespace XtermSharp.Decorations;

/// <summary>A visual decoration covering a contiguous range on one active-buffer line.</summary>
public sealed record TerminalDecoration(
    TerminalDecorationRange Range,
    TerminalDecorationLayer Layer = TerminalDecorationLayer.Bottom,
    TerminalColor? Background = null,
    TerminalColor? Border = null,
    TerminalColor? OverviewRuler = null);

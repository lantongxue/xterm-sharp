namespace XtermSharp.Rendering;

public sealed record TerminalLineCommand(
    TerminalRect Rectangle,
    TerminalPoint Start,
    TerminalPoint End,
    TerminalRgbaColor Color,
    double Thickness,
    TerminalUnderlineStyle Style = TerminalUnderlineStyle.Single)
    : TerminalDrawCommand(TerminalDrawCommandKind.Line, Rectangle);

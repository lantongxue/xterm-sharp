namespace XtermSharp.Rendering;

public sealed record TerminalStrokeRectangleCommand(
    TerminalRect Rectangle,
    TerminalRgbaColor Color,
    double Thickness)
    : TerminalDrawCommand(TerminalDrawCommandKind.StrokeRectangle, Rectangle);

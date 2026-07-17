namespace XtermSharp.Rendering.Display;

public sealed record TerminalFillRectangleCommand(TerminalRect Rectangle, TerminalRgbaColor Color)
    : TerminalDrawCommand(TerminalDrawCommandKind.FillRectangle, Rectangle);

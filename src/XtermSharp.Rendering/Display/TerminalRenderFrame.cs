namespace XtermSharp.Rendering.Display;

public sealed record TerminalRenderFrame(
    long Revision,
    TerminalViewport Viewport,
    TerminalFontMetrics Metrics,
    int Columns,
    int Rows,
    int ViewportY,
    int BaseY,
    TerminalDisplayList DisplayList,
    TerminalDamage Damage)
{
    public TerminalModes? Modes { get; init; }
    public int CursorColumn { get; init; }
    public int CursorRow { get; init; }
}

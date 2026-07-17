namespace XtermSharp.Rendering.Display;

public sealed record TerminalTextCommand(
    TerminalRect Rectangle,
    string Text,
    TerminalRgbaColor Color,
    bool Bold,
    bool Italic,
    bool RescaleToFit)
    : TerminalDrawCommand(TerminalDrawCommandKind.Text, Rectangle)
{
    /// <summary>
    /// Gets the number of fixed-width terminal cells represented by <see cref="Text"/>.
    /// Values greater than one allow backends to render compatible text runs in one operation.
    /// </summary>
    public int CellCount { get; init; } = 1;
}

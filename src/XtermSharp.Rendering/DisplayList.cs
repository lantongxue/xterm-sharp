using System.Collections.Immutable;

namespace XtermSharp.Rendering;

public enum TerminalDrawCommandKind
{
    FillRectangle,
    StrokeRectangle,
    Text,
    Line
}

public abstract record TerminalDrawCommand(TerminalDrawCommandKind Kind, TerminalRect Bounds);

public sealed record TerminalFillRectangleCommand(TerminalRect Rectangle, TerminalRgbaColor Color)
    : TerminalDrawCommand(TerminalDrawCommandKind.FillRectangle, Rectangle);

public sealed record TerminalStrokeRectangleCommand(
    TerminalRect Rectangle,
    TerminalRgbaColor Color,
    double Thickness)
    : TerminalDrawCommand(TerminalDrawCommandKind.StrokeRectangle, Rectangle);

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

public sealed record TerminalLineCommand(
    TerminalRect Rectangle,
    TerminalPoint Start,
    TerminalPoint End,
    TerminalRgbaColor Color,
    double Thickness,
    TerminalUnderlineStyle Style = TerminalUnderlineStyle.Single)
    : TerminalDrawCommand(TerminalDrawCommandKind.Line, Rectangle);

public sealed record TerminalDisplayRow(int Row, ImmutableArray<TerminalDrawCommand> Commands);

public sealed record TerminalDisplayList(ImmutableArray<TerminalDisplayRow> Rows)
{
    public static TerminalDisplayList Empty { get; } = new(ImmutableArray<TerminalDisplayRow>.Empty);
}

public readonly record struct TerminalDamage(int StartRow, int EndRow)
{
    public static TerminalDamage Empty { get; } = new(0, -1);
    public static TerminalDamage Full(int rows) => new(0, Math.Max(0, rows - 1));
    public bool IsEmpty => EndRow < StartRow;
}

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

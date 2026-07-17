namespace XtermSharp.Decorations;

/// <summary>A zero-based, end-exclusive range on one active-buffer line.</summary>
public readonly record struct TerminalDecorationRange(int Column, int Line, int Width)
{
    public bool Contains(int column, int line) =>
        line == Line && column >= Column && column < Column + Width;
}

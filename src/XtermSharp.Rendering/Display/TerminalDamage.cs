namespace XtermSharp.Rendering.Display;

public readonly record struct TerminalDamage(int StartRow, int EndRow)
{
    public static TerminalDamage Empty { get; } = new(0, -1);
    public static TerminalDamage Full(int rows) => new(0, Math.Max(0, rows - 1));
    public bool IsEmpty => EndRow < StartRow;
}

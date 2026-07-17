namespace XtermSharp.Events;

public sealed class TerminalResizeEventArgs(long revision, int columns, int rows)
    : TerminalEventArgs(revision)
{
    public int Columns { get; } = columns;
    public int Rows { get; } = rows;
}

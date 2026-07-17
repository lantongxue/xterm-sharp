namespace XtermSharp.Events;

public sealed class TerminalRenderEventArgs(long revision, int startRow, int endRow)
    : TerminalEventArgs(revision)
{
    public int StartRow { get; } = startRow;
    public int EndRow { get; } = endRow;
}

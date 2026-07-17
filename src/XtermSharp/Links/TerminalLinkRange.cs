namespace XtermSharp.Links;

/// <summary>An xterm-compatible, one-based inclusive range in the active terminal buffer.</summary>
public readonly record struct TerminalLinkRange(TerminalLinkPosition Start, TerminalLinkPosition End)
{
    public bool Contains(int column, int bufferLine, int columns)
    {
        if (column <= 0 || bufferLine <= 0 || columns <= 0 ||
            Start.X <= 0 || Start.Y <= 0 || End.X <= 0 || End.Y <= 0)
        {
            return false;
        }

        long lower = ((long)Start.Y - 1) * columns + Start.X;
        long upper = ((long)End.Y - 1) * columns + End.X;
        long current = ((long)bufferLine - 1) * columns + column;
        return lower <= current && current <= upper;
    }
}

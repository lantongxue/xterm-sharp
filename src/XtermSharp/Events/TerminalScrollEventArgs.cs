namespace XtermSharp;

public sealed class TerminalScrollEventArgs(long revision, int viewportY)
    : TerminalEventArgs(revision)
{
    public int ViewportY { get; } = viewportY;
}

namespace XtermSharp.Events;

public class TerminalEventArgs(long revision) : EventArgs
{
    public long Revision { get; } = revision;
}

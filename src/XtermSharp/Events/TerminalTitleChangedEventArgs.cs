namespace XtermSharp;

public sealed class TerminalTitleChangedEventArgs(long revision, string title)
    : TerminalEventArgs(revision)
{
    public string Title { get; } = title;
}

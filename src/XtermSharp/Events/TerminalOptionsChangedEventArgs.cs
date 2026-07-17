namespace XtermSharp;

public sealed class TerminalOptionsChangedEventArgs(
    long revision,
    TerminalOptions previous,
    TerminalOptions current)
    : TerminalEventArgs(revision)
{
    public TerminalOptions Previous { get; } = previous;
    public TerminalOptions Current { get; } = current;
}

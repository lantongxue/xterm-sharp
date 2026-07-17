namespace XtermSharp.Events;

public sealed class TerminalColorRequestEventArgs(
    long revision,
    IReadOnlyList<TerminalColorRequest> requests)
    : TerminalEventArgs(revision)
{
    public IReadOnlyList<TerminalColorRequest> Requests { get; } = requests;
}

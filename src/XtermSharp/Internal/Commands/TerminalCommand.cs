namespace XtermSharp.Internal;

internal abstract class TerminalCommand
{
    protected TerminalCommand(bool mutatesState, bool isWrite, PendingByteLease? lease)
    {
        MutatesState = mutatesState;
        IsWrite = isWrite;
        Lease = lease;
    }

    public bool MutatesState { get; }
    public bool IsWrite { get; }
    public PendingByteLease? Lease { get; }
    public abstract ValueTask ExecuteAsync(TerminalEngine engine, long revision);
    public abstract void Complete();
    public abstract void Fail(Exception exception);
}

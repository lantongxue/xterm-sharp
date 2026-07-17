namespace XtermSharp.Internal;

internal sealed class MutationCommand(
    Func<TerminalEngine, ValueTask> action,
    bool isWrite,
    PendingByteLease? lease,
    Action? callback) : TerminalCommand(true, isWrite, lease)
{
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Task => _completion.Task;
    public override ValueTask ExecuteAsync(TerminalEngine engine, long revision) => action(engine);
    public override void Complete()
    {
        callback?.Invoke();
        _completion.TrySetResult();
    }
    public override void Fail(Exception exception) => _completion.TrySetException(exception);
}

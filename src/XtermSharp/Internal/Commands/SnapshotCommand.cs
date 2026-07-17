namespace XtermSharp.Internal;

internal sealed class SnapshotCommand(SnapshotScope scope) : TerminalCommand(false, false, null)
{
    private readonly TaskCompletionSource<TerminalSnapshot> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TerminalSnapshot? _snapshot;

    public Task<TerminalSnapshot> Task => _completion.Task;

    public override ValueTask ExecuteAsync(TerminalEngine engine, long revision)
    {
        _snapshot = engine.CreateSnapshot(revision, scope);
        return ValueTask.CompletedTask;
    }

    public override void Complete() => _completion.TrySetResult(_snapshot!);
    public override void Fail(Exception exception) => _completion.TrySetException(exception);
}

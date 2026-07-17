namespace XtermSharp.Internal.Commands;

internal sealed class ResultMutationCommand<TResult>(
    Func<TerminalEngine, TResult> action) : TerminalCommand(true, false, null)
{
    private readonly TaskCompletionSource<TResult> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TResult? _result;

    public Task<TResult> Task => _completion.Task;

    public override ValueTask ExecuteAsync(TerminalEngine engine, long revision)
    {
        _result = action(engine);
        return ValueTask.CompletedTask;
    }

    public override void Complete() => _completion.TrySetResult(_result!);
    public override void Fail(Exception exception) => _completion.TrySetException(exception);
}

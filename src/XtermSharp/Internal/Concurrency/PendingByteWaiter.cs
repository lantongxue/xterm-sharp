namespace XtermSharp.Internal.Concurrency;

internal sealed class PendingByteWaiter(long weight, CancellationToken cancellationToken)
{
    public long Weight { get; } = weight;
    public CancellationToken CancellationToken { get; } = cancellationToken;
    public TaskCompletionSource<PendingByteLease> Completion { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public LinkedListNode<PendingByteWaiter>? Node { get; set; }
    public CancellationTokenRegistration Registration { get; set; }
}

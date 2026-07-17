namespace XtermSharp.Internal.Concurrency;

internal sealed class PendingByteLease(PendingByteLimiter owner, long weight) : IDisposable
{
    private PendingByteLimiter? _owner = owner;

    public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Release(weight);
}

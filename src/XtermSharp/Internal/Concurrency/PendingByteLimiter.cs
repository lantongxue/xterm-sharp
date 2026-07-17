namespace XtermSharp.Internal;

internal sealed class PendingByteLimiter : IDisposable
{
    private readonly object _gate = new();
    private readonly long _limit;
    private readonly LinkedList<PendingByteWaiter> _waiters = [];
    private long _used;
    private bool _disposed;

    public PendingByteLimiter(long limit) => _limit = limit;

    public ValueTask<PendingByteLease> AcquireAsync(long requestedWeight, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        long weight = Math.Min(Math.Max(1, requestedWeight), _limit);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_waiters.Count == 0 && _used + weight <= _limit)
            {
                _used += weight;
                return ValueTask.FromResult(new PendingByteLease(this, weight));
            }

            var waiter = new PendingByteWaiter(weight, cancellationToken);
            waiter.Node = _waiters.AddLast(waiter);
            if (cancellationToken.CanBeCanceled)
            {
                waiter.Registration = cancellationToken.UnsafeRegister(
                    static state =>
                    {
                        var (owner, queued) = ((PendingByteLimiter, PendingByteWaiter))state!;
                        owner.Cancel(queued);
                    },
                    (this, waiter));
            }
            return new ValueTask<PendingByteLease>(waiter.Completion.Task);
        }
    }

    public void Dispose()
    {
        PendingByteWaiter[] waiters;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            waiters = _waiters.ToArray();
            _waiters.Clear();
            foreach (PendingByteWaiter waiter in waiters)
            {
                waiter.Node = null;
            }
        }

        var exception = new ObjectDisposedException(nameof(PendingByteLimiter));
        foreach (PendingByteWaiter waiter in waiters)
        {
            waiter.Registration.Dispose();
            waiter.Completion.TrySetException(exception);
        }
    }

    private void Cancel(PendingByteWaiter waiter)
    {
        List<(PendingByteWaiter Waiter, PendingByteLease Lease)> granted = [];
        bool removed;
        lock (_gate)
        {
            removed = waiter.Node?.List is not null;
            if (removed)
            {
                _waiters.Remove(waiter.Node!);
                waiter.Node = null;
                GrantWaiters(granted);
            }
        }
        if (removed)
        {
            waiter.Completion.TrySetCanceled(waiter.CancellationToken);
            CompleteGranted(granted);
        }
    }

    internal void Release(long weight)
    {
        List<(PendingByteWaiter Waiter, PendingByteLease Lease)> granted = [];
        lock (_gate)
        {
            _used -= weight;
            GrantWaiters(granted);
        }
        CompleteGranted(granted);
    }

    private void GrantWaiters(List<(PendingByteWaiter Waiter, PendingByteLease Lease)> granted)
    {
        while (_waiters.First is { } node && _used + node.Value.Weight <= _limit)
        {
            PendingByteWaiter waiter = node.Value;
            _waiters.RemoveFirst();
            waiter.Node = null;
            _used += waiter.Weight;
            granted.Add((waiter, new PendingByteLease(this, waiter.Weight)));
        }
    }

    private static void CompleteGranted(List<(PendingByteWaiter Waiter, PendingByteLease Lease)> granted)
    {
        foreach ((PendingByteWaiter waiter, PendingByteLease lease) in granted)
        {
            waiter.Registration.Dispose();
            waiter.Completion.TrySetResult(lease);
        }
    }
}

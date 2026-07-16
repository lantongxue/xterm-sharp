namespace XtermSharp.Internal;

internal sealed class PendingByteLimiter : IDisposable
{
    internal sealed class Lease(PendingByteLimiter owner, long weight) : IDisposable
    {
        private PendingByteLimiter? _owner = owner;

        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Release(weight);
    }

    private sealed class Waiter(long weight, CancellationToken cancellationToken)
    {
        public long Weight { get; } = weight;
        public CancellationToken CancellationToken { get; } = cancellationToken;
        public TaskCompletionSource<Lease> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public LinkedListNode<Waiter>? Node { get; set; }
        public CancellationTokenRegistration Registration { get; set; }
    }

    private readonly object _gate = new();
    private readonly long _limit;
    private readonly LinkedList<Waiter> _waiters = [];
    private long _used;
    private bool _disposed;

    public PendingByteLimiter(long limit) => _limit = limit;

    public ValueTask<Lease> AcquireAsync(long requestedWeight, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        long weight = Math.Min(Math.Max(1, requestedWeight), _limit);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_waiters.Count == 0 && _used + weight <= _limit)
            {
                _used += weight;
                return ValueTask.FromResult(new Lease(this, weight));
            }

            var waiter = new Waiter(weight, cancellationToken);
            waiter.Node = _waiters.AddLast(waiter);
            if (cancellationToken.CanBeCanceled)
            {
                waiter.Registration = cancellationToken.UnsafeRegister(
                    static state =>
                    {
                        var (owner, queued) = ((PendingByteLimiter, Waiter))state!;
                        owner.Cancel(queued);
                    },
                    (this, waiter));
            }
            return new ValueTask<Lease>(waiter.Completion.Task);
        }
    }

    public void Dispose()
    {
        Waiter[] waiters;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            waiters = _waiters.ToArray();
            _waiters.Clear();
            foreach (Waiter waiter in waiters)
            {
                waiter.Node = null;
            }
        }

        var exception = new ObjectDisposedException(nameof(PendingByteLimiter));
        foreach (Waiter waiter in waiters)
        {
            waiter.Registration.Dispose();
            waiter.Completion.TrySetException(exception);
        }
    }

    private void Cancel(Waiter waiter)
    {
        List<(Waiter Waiter, Lease Lease)> granted = [];
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

    private void Release(long weight)
    {
        List<(Waiter Waiter, Lease Lease)> granted = [];
        lock (_gate)
        {
            _used -= weight;
            GrantWaiters(granted);
        }
        CompleteGranted(granted);
    }

    private void GrantWaiters(List<(Waiter Waiter, Lease Lease)> granted)
    {
        while (_waiters.First is { } node && _used + node.Value.Weight <= _limit)
        {
            Waiter waiter = node.Value;
            _waiters.RemoveFirst();
            waiter.Node = null;
            _used += waiter.Weight;
            granted.Add((waiter, new Lease(this, waiter.Weight)));
        }
    }

    private static void CompleteGranted(List<(Waiter Waiter, Lease Lease)> granted)
    {
        foreach ((Waiter waiter, Lease lease) in granted)
        {
            waiter.Registration.Dispose();
            waiter.Completion.TrySetResult(lease);
        }
    }
}

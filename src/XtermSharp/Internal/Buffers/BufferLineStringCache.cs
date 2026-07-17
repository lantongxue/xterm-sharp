namespace XtermSharp.Internal.Buffers;

internal sealed class BufferLineStringCache : IDisposable
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(15);
    private readonly object _gate = new();
    private readonly HashSet<BufferLine> _entries = [];
    private Timer? _timer;
    private DateTimeOffset _lastTouch;
    private bool _disposed;

    public int EntryCount
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    public bool IsCleanupScheduled
    {
        get
        {
            lock (_gate)
            {
                return _timer is not null;
            }
        }
    }

    public void Touch(BufferLine line)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _entries.Add(line);
            _lastTouch = DateTimeOffset.UtcNow;
            _timer ??= new Timer(static state => ((BufferLineStringCache)state!).Sweep(), this, Lifetime, Timeout.InfiniteTimeSpan);
        }
    }

    public void Sweep(DateTimeOffset? now = null)
    {
        lock (_gate)
        {
            if (_disposed || _timer is null)
            {
                return;
            }
            TimeSpan idle = (now ?? DateTimeOffset.UtcNow) - _lastTouch;
            if (idle < Lifetime)
            {
                _timer.Change(Lifetime - idle, Timeout.InfiniteTimeSpan);
                return;
            }
            ClearCore();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            ClearCore();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            ClearCore();
        }
    }

    private void ClearCore()
    {
        foreach (BufferLine line in _entries)
        {
            line.ClearCachedString();
        }
        _entries.Clear();
        _timer?.Dispose();
        _timer = null;
    }
}

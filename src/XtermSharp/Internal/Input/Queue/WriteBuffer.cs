namespace XtermSharp.Internal;

/// <summary>Ordered, sliceable write queue matching xterm.js write/writeSync semantics.</summary>
internal sealed class WriteBuffer : IDisposable
{
    private const long DiscardWatermark = 50_000_000;

    private readonly object _gate = new();
    private readonly Queue<(WriteBufferChunk Chunk, Action? Callback)> _queue = [];
    private readonly Func<WriteBufferChunk, bool, ValueTask<bool>> _action;
    private bool _scheduled;
    private bool _disposed;
    private bool _isSyncWriting;
    private bool _didUserInput;
    private int _syncCalls;
    private long _pendingData;

    public WriteBuffer(Func<WriteBufferChunk, bool, ValueTask<bool>> action)
    {
        _action = action ?? throw new ArgumentNullException(nameof(action));
    }

    public event Action? WriteParsed;

    public void HandleUserInput()
    {
        lock (_gate)
        {
            if (!_disposed) _didUserInput = true;
        }
    }

    public void Write(string data, Action? callback = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        Write(WriteBufferChunk.FromText(data), callback);
    }

    public void Write(ReadOnlyMemory<byte> data, Action? callback = null) =>
        Write(WriteBufferChunk.FromBytes(data), callback);

    public void WriteSync(string data, int? maximumSubsequentCalls = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        WriteSync(WriteBufferChunk.FromText(data), maximumSubsequentCalls);
    }

    public void WriteSync(ReadOnlyMemory<byte> data, int? maximumSubsequentCalls = null) =>
        WriteSync(WriteBufferChunk.FromBytes(data), maximumSubsequentCalls);

    public void FlushSync()
    {
        List<(WriteBufferChunk Chunk, Action? Callback)> pending;
        lock (_gate)
        {
            if (_disposed || _isSyncWriting || _queue.Count == 0)
            {
                return;
            }
            _isSyncWriting = true;
            pending = DrainQueue();
        }

        try
        {
            foreach ((WriteBufferChunk chunk, Action? callback) in pending)
            {
                _action(chunk, true).GetAwaiter().GetResult();
                callback?.Invoke();
            }
            WriteParsed?.Invoke();
        }
        finally
        {
            lock (_gate) _isSyncWriting = false;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _queue.Clear();
            _pendingData = 0;
            _scheduled = false;
            WriteParsed = null;
        }
    }

    private void Write(WriteBufferChunk data, Action? callback)
    {
        bool processImmediately;
        lock (_gate)
        {
            if (_disposed) return;
            if (_pendingData > DiscardWatermark)
            {
                throw new InvalidOperationException("Write data discarded; use flow control to avoid losing data.");
            }
            processImmediately = _queue.Count == 0 && _didUserInput;
            _didUserInput = false;
            _queue.Enqueue((data, callback));
            _pendingData += data.Length;
            if (!processImmediately && !_scheduled)
            {
                _scheduled = true;
                _ = ProcessScheduledAsync();
            }
        }

        if (processImmediately)
        {
            ProcessOneSynchronously();
        }
    }

    private void WriteSync(WriteBufferChunk data, int? maximumSubsequentCalls)
    {
        lock (_gate)
        {
            if (_disposed) return;
            if (maximumSubsequentCalls is int maximum && _syncCalls > maximum)
            {
                _syncCalls = 0;
                return;
            }
            _queue.Enqueue((data, null));
            _pendingData += data.Length;
            _syncCalls++;
            if (_isSyncWriting) return;
            _isSyncWriting = true;
        }

        try
        {
            while (true)
            {
                (WriteBufferChunk Chunk, Action? Callback) item;
                lock (_gate)
                {
                    if (_disposed || _queue.Count == 0) break;
                    item = _queue.Dequeue();
                    _pendingData -= item.Chunk.Length;
                }
                _action(item.Chunk, true).GetAwaiter().GetResult();
                item.Callback?.Invoke();
            }
        }
        finally
        {
            lock (_gate)
            {
                _isSyncWriting = false;
                _syncCalls = 0;
            }
        }
    }

    private async Task ProcessScheduledAsync()
    {
        // Match JavaScript's next-macrotask scheduling. A small delay guarantees synchronous
        // flush/writeSync calls in the current turn can drain the queue first.
        await Task.Delay(10).ConfigureAwait(false);
        while (true)
        {
            (WriteBufferChunk Chunk, Action? Callback) item;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }
                if (_queue.Count == 0)
                {
                    _scheduled = false;
                    break;
                }
                item = _queue.Dequeue();
                _pendingData -= item.Chunk.Length;
            }

            await _action(item.Chunk, true).ConfigureAwait(false);
            lock (_gate)
            {
                if (_disposed) return;
            }
            item.Callback?.Invoke();
        }

        lock (_gate)
        {
            if (_disposed) return;
        }
        WriteParsed?.Invoke();
    }

    private void ProcessOneSynchronously()
    {
        (WriteBufferChunk Chunk, Action? Callback) item;
        lock (_gate)
        {
            if (_disposed || _queue.Count == 0) return;
            item = _queue.Dequeue();
            _pendingData -= item.Chunk.Length;
        }
        _action(item.Chunk, true).GetAwaiter().GetResult();
        item.Callback?.Invoke();
        WriteParsed?.Invoke();
    }

    private List<(WriteBufferChunk Chunk, Action? Callback)> DrainQueue()
    {
        var result = new List<(WriteBufferChunk Chunk, Action? Callback)>(_queue.Count);
        while (_queue.TryDequeue(out (WriteBufferChunk Chunk, Action? Callback) item))
        {
            result.Add(item);
        }
        _pendingData = 0;
        return result;
    }
}

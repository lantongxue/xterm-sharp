namespace XtermSharp;

/// <summary>A line anchor that follows buffer insertion, trimming and reflow.</summary>
public sealed class TerminalMarker : IDisposable
{
    private static int _nextId;
    private readonly object _gate = new();
    private int _line;
    private bool _isDisposed;

    internal TerminalMarker(int line)
    {
        Id = Interlocked.Increment(ref _nextId);
        _line = line;
    }

    public int Id { get; }
    public int Line => Volatile.Read(ref _line);
    public bool IsDisposed => Volatile.Read(ref _isDisposed);

    public event EventHandler? Disposed;

    public void Dispose()
    {
        EventHandler? handlers;
        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }
            _isDisposed = true;
            _line = -1;
            handlers = Disposed;
            Disposed = null;
        }

        if (handlers is null)
        {
            return;
        }
        foreach (EventHandler handler in handlers.GetInvocationList().Cast<EventHandler>())
        {
            try
            {
                handler(this, EventArgs.Empty);
            }
            catch
            {
                // Marker cleanup must not be interrupted by an observer.
            }
        }
    }

    internal void SetLine(int line)
    {
        if (!IsDisposed)
        {
            Volatile.Write(ref _line, line);
        }
    }
}

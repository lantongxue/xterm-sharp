namespace XtermSharp.Internal.Utilities;

/// <summary>
/// Minimal synchronous event emitter with snapshot iteration semantics.
/// </summary>
internal sealed class Emitter<T> : IDisposable
{
    private readonly List<EmitterListenerEntry<T>> _listeners = [];
    private bool _disposed;
    private XtermEvent<T>? _event;

    public XtermEvent<T> Event => _event ??= Subscribe;

    public void Fire(T value)
    {
        if (_disposed)
        {
            return;
        }

        switch (_listeners.Count)
        {
            case 0:
                return;
            case 1:
                _listeners[0].Listener(value);
                return;
            default:
                EmitterListenerEntry<T>[] listeners = _listeners.ToArray();
                foreach (EmitterListenerEntry<T> listener in listeners)
                {
                    listener.Listener(value);
                }
                return;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _listeners.Clear();
    }

    private IDisposable Subscribe(Action<T> listener, ICollection<IDisposable>? disposables)
    {
        ArgumentNullException.ThrowIfNull(listener);
        if (_disposed)
        {
            return EmptyDisposable.Instance;
        }

        var entry = new EmitterListenerEntry<T>(listener);
        _listeners.Add(entry);
        var result = new Subscription(() => _listeners.Remove(entry));
        disposables?.Add(result);
        return result;
    }
}

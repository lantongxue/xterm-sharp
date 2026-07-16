namespace XtermSharp.Internal.Utilities;

internal delegate IDisposable XtermEvent<T>(
    Action<T> listener,
    ICollection<IDisposable>? disposables = null);

/// <summary>
/// Minimal synchronous event emitter with snapshot iteration semantics.
/// </summary>
internal sealed class Emitter<T> : IDisposable
{
    private readonly List<ListenerEntry> _listeners = [];
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
                ListenerEntry[] listeners = _listeners.ToArray();
                foreach (ListenerEntry listener in listeners)
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

        var entry = new ListenerEntry(listener);
        _listeners.Add(entry);
        var result = new Subscription(() => _listeners.Remove(entry));
        disposables?.Add(result);
        return result;
    }

    private sealed record ListenerEntry(Action<T> Listener);
}

internal static class EventUtils
{
    public static IDisposable Forward<T>(XtermEvent<T> from, Emitter<T> to) =>
        from(to.Fire);

    public static XtermEvent<TOutput> Map<TInput, TOutput>(
        XtermEvent<TInput> source,
        Func<TInput, TOutput> map) =>
        (listener, disposables) => source(value => listener(map(value)), disposables);

    public static XtermEvent<T> Any<T>(params XtermEvent<T>[] events) =>
        (listener, disposables) =>
        {
            var store = new SubscriptionStore();
            foreach (XtermEvent<T> source in events)
            {
                store.Add(source(listener));
            }
            disposables?.Add(store);
            return store;
        };

    public static IDisposable RunAndSubscribe<T>(
        XtermEvent<T> source,
        Action<T?> handler,
        T? initial = default)
    {
        handler(initial);
        return source(handler);
    }
}

internal sealed class SubscriptionStore : IDisposable
{
    private readonly List<IDisposable> _subscriptions = [];
    private bool _disposed;

    public void Add(IDisposable subscription)
    {
        if (_disposed)
        {
            subscription.Dispose();
            return;
        }
        _subscriptions.Add(subscription);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        foreach (IDisposable subscription in _subscriptions)
        {
            subscription.Dispose();
        }
        _subscriptions.Clear();
    }
}

internal sealed class Subscription(Action dispose) : IDisposable
{
    private Action? _dispose = dispose;

    public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
}

internal sealed class EmptyDisposable : IDisposable
{
    public static EmptyDisposable Instance { get; } = new();

    private EmptyDisposable()
    {
    }

    public void Dispose()
    {
    }
}

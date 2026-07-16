using System.Runtime.ExceptionServices;

namespace XtermSharp.Internal.Parser;

internal abstract class HandlerCollection<THandler> : IDisposable where THandler : class
{
    private readonly object _gate = new();
    private readonly object _completionGate = new();
    private readonly Dictionary<int, List<THandler>> _handlers = [];
    private int _activeGeneration;
    private int _completionIndex = -1;
    private bool _disposed;

    protected IReadOnlyList<THandler> ActiveHandlers { get; private set; } = [];
    protected int ActiveIdentifier { get; private set; }

    public IDisposable RegisterHandler(int identifier, THandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_handlers.TryGetValue(identifier, out List<THandler>? handlers))
            {
                handlers = [];
                _handlers.Add(identifier, handlers);
            }
            handlers.Add(handler);
        }
        return new DelegateDisposable(() =>
        {
            lock (_gate)
            {
                if (!_handlers.TryGetValue(identifier, out List<THandler>? handlers))
                {
                    return;
                }
                handlers.Remove(handler);
                if (handlers.Count == 0)
                {
                    _handlers.Remove(identifier);
                }
            }
        });
    }

    public void ClearHandler(int identifier)
    {
        lock (_gate)
        {
            _handlers.Remove(identifier);
        }
    }

    protected IReadOnlyList<THandler> Activate(int identifier)
    {
        ActiveIdentifier = identifier;
        Volatile.Write(ref _completionIndex, -1);
        lock (_gate)
        {
            ActiveHandlers = _handlers.TryGetValue(identifier, out List<THandler>? handlers)
                ? handlers.ToArray()
                : [];
        }
        return ActiveHandlers;
    }

    protected async ValueTask<bool> CompleteActiveHandlersAsync(
        bool success,
        Func<THandler, bool, ValueTask<bool>> complete)
    {
        IReadOnlyList<THandler> handlers = ActiveHandlers;
        int generation = Volatile.Read(ref _activeGeneration);
        bool handled = false;
        for (int index = handlers.Count - 1; index >= 0; index--)
        {
            ValueTask<bool> pending;
            lock (_completionGate)
            {
                if (generation != _activeGeneration)
                {
                    return false;
                }
                _completionIndex = index;
                pending = complete(handlers[index], handled ? false : success);
            }
            bool result = await pending.ConfigureAwait(false);
            if (generation != Volatile.Read(ref _activeGeneration))
            {
                return false;
            }
            handled |= result;
        }

        Volatile.Write(ref _completionIndex, -1);
        ClearActive();
        return true;
    }

    protected void AbortActiveHandlers(Func<THandler, ValueTask<bool>> abort)
    {
        IReadOnlyList<THandler> handlers;
        int start;
        lock (_completionGate)
        {
            handlers = ActiveHandlers;
            _activeGeneration++;
            start = _completionIndex >= 0 ? _completionIndex - 1 : handlers.Count - 1;
            _completionIndex = -1;
            ClearActive();
        }
        Exception? firstException = null;

        for (int index = start; index >= 0; index--)
        {
            try
            {
                ValueTask<bool> result = abort(handlers[index]);
                if (result.IsCompleted)
                {
                    result.GetAwaiter().GetResult();
                }
                else
                {
                    _ = ObserveAbortAsync(result);
                }
            }
            catch (Exception exception)
            {
                firstException ??= exception;
            }
        }

        if (firstException is not null)
        {
            ExceptionDispatchInfo.Capture(firstException).Throw();
        }
    }

    protected void ClearActive()
    {
        ActiveIdentifier = 0;
        ActiveHandlers = [];
    }

    private static async Task ObserveAbortAsync(ValueTask<bool> result)
    {
        try
        {
            await result.ConfigureAwait(false);
        }
        catch
        {
            // Reset is synchronous like the upstream parser. Asynchronous cleanup failures cannot
            // be rethrown to its caller, but the task must still be observed.
        }
    }

    public virtual void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _handlers.Clear();
        }
        ClearActive();
    }
}

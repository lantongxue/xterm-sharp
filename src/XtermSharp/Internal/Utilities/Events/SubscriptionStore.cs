namespace XtermSharp.Internal.Utilities.Events;

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

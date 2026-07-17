namespace XtermSharp.Internal.Utilities.Events;

internal sealed class Subscription(Action dispose) : IDisposable
{
    private Action? _dispose = dispose;

    public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
}

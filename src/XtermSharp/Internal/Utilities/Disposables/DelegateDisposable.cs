namespace XtermSharp.Internal.Utilities.Disposables;

internal sealed class DelegateDisposable(Action dispose) : IDisposable
{
    private Action? _dispose = dispose;

    public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
}

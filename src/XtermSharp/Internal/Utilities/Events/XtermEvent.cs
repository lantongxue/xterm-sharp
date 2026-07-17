namespace XtermSharp.Internal.Utilities;

internal delegate IDisposable XtermEvent<T>(
    Action<T> listener,
    ICollection<IDisposable>? disposables = null);

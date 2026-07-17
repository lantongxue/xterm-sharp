namespace XtermSharp.Internal.Utilities.Events;

internal delegate IDisposable XtermEvent<T>(
    Action<T> listener,
    ICollection<IDisposable>? disposables = null);

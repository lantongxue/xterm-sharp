namespace XtermSharp.Internal.Utilities;

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

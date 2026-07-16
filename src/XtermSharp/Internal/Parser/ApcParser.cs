using XtermSharp.Internal.Utilities;

namespace XtermSharp.Internal.Parser;

internal sealed class ApcParser : HandlerCollection<IApcParserHandler>
{
    private Action<int, StringParserAction, object?>? _fallback;

    public void SetHandlerFallback(Action<int, StringParserAction, object?> fallback) =>
        Volatile.Write(ref _fallback, fallback ?? throw new ArgumentNullException(nameof(fallback)));

    public void Start(int identifier)
    {
        Reset();
        IReadOnlyList<IApcParserHandler> handlers = Activate(identifier);
        if (handlers.Count == 0)
        {
            Volatile.Read(ref _fallback)?.Invoke(identifier, StringParserAction.Start, null);
            return;
        }
        for (int index = handlers.Count - 1; index >= 0; index--)
        {
            handlers[index].Start();
        }
    }

    public void Put(ReadOnlySpan<uint> data)
    {
        IReadOnlyList<IApcParserHandler> handlers = ActiveHandlers;
        if (handlers.Count == 0)
        {
            Action<int, StringParserAction, object?>? fallback = Volatile.Read(ref _fallback);
            if (fallback is not null)
            {
                fallback(ActiveIdentifier, StringParserAction.Put, TextDecoder.Utf32ToString(data));
            }
            return;
        }
        for (int index = handlers.Count - 1; index >= 0; index--)
        {
            handlers[index].Put(data);
        }
    }

    public async ValueTask EndAsync(bool success)
    {
        IReadOnlyList<IApcParserHandler> handlers = ActiveHandlers;
        if (handlers.Count == 0)
        {
            Volatile.Read(ref _fallback)?.Invoke(ActiveIdentifier, StringParserAction.End, success);
            ClearActive();
            return;
        }

        await CompleteActiveHandlersAsync(success, static (handler, value) => handler.EndAsync(value))
            .ConfigureAwait(false);
    }

    public void Reset()
    {
        try
        {
            if (ActiveHandlers.Count != 0)
            {
                AbortActiveHandlers(static handler => handler.EndAsync(false));
            }
        }
        finally
        {
            ClearActive();
        }
    }

    public override void Dispose()
    {
        try
        {
            Reset();
        }
        finally
        {
            Volatile.Write(ref _fallback, null);
            base.Dispose();
        }
    }
}

namespace XtermSharp.Internal.Parser;

internal sealed class DcsParser : HandlerCollection<IDcsParserHandler>
{
    private Action<int, StringParserAction, object?>? _fallback;

    public void SetHandlerFallback(Action<int, StringParserAction, object?> fallback) =>
        Volatile.Write(ref _fallback, fallback ?? throw new ArgumentNullException(nameof(fallback)));

    public void Hook(int identifier, CsiParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        Reset();
        IReadOnlyList<IDcsParserHandler> handlers = Activate(identifier);
        if (handlers.Count == 0)
        {
            Volatile.Read(ref _fallback)?.Invoke(identifier, StringParserAction.Hook, parameters);
            return;
        }
        for (int index = handlers.Count - 1; index >= 0; index--)
        {
            handlers[index].Hook(parameters);
        }
    }

    public void Put(ReadOnlySpan<uint> data)
    {
        IReadOnlyList<IDcsParserHandler> handlers = ActiveHandlers;
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

    public async ValueTask UnhookAsync(bool success)
    {
        IReadOnlyList<IDcsParserHandler> handlers = ActiveHandlers;
        if (handlers.Count == 0)
        {
            Volatile.Read(ref _fallback)?.Invoke(ActiveIdentifier, StringParserAction.Unhook, success);
            ClearActive();
            return;
        }

        await CompleteActiveHandlersAsync(success, static (handler, value) => handler.UnhookAsync(value))
            .ConfigureAwait(false);
    }

    public void Reset()
    {
        try
        {
            if (ActiveHandlers.Count != 0)
            {
                AbortActiveHandlers(static handler => handler.UnhookAsync(false));
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

using XtermSharp.Internal.Utilities;

namespace XtermSharp.Internal.Parser;

internal sealed class OscParser : HandlerCollection<IOscParserHandler>
{
    private Action<int, StringParserAction, object?>? _fallback;
    private OscParserState _state;
    private int _identifier = -1;

    public void SetHandlerFallback(Action<int, StringParserAction, object?> fallback) =>
        Volatile.Write(ref _fallback, fallback ?? throw new ArgumentNullException(nameof(fallback)));

    public void Start()
    {
        Reset();
        _state = OscParserState.Identifier;
    }

    public void Put(ReadOnlySpan<uint> data)
    {
        if (_state == OscParserState.Abort)
        {
            return;
        }
        int index = 0;
        if (_state == OscParserState.Identifier)
        {
            while (index < data.Length)
            {
                uint value = data[index++];
                if (value == ';')
                {
                    _state = OscParserState.Payload;
                    AnnounceStart();
                    break;
                }
                if (value is < '0' or > '9')
                {
                    _state = OscParserState.Abort;
                    return;
                }
                if (_identifier == -1)
                {
                    _identifier = 0;
                }
                if (_identifier > (int.MaxValue - (value - '0')) / 10)
                {
                    _state = OscParserState.Abort;
                    return;
                }
                _identifier = _identifier * 10 + (int)(value - '0');
            }
        }
        if (_state == OscParserState.Payload && index < data.Length)
        {
            PutPayload(data[index..]);
        }
    }

    public async ValueTask EndAsync(bool success)
    {
        if (_state == OscParserState.Start)
        {
            return;
        }
        if (_state == OscParserState.Abort)
        {
            ClearState();
            return;
        }

        if (_state == OscParserState.Identifier)
        {
            AnnounceStart();
        }
        IReadOnlyList<IOscParserHandler> handlers = ActiveHandlers;
        if (handlers.Count == 0)
        {
            Volatile.Read(ref _fallback)?.Invoke(_identifier, StringParserAction.End, success);
            ClearState();
            return;
        }

        if (await CompleteActiveHandlersAsync(success, static (handler, value) => handler.EndAsync(value))
            .ConfigureAwait(false))
        {
            ClearState();
        }
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
            ClearState();
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

    private void AnnounceStart()
    {
        IReadOnlyList<IOscParserHandler> handlers = Activate(_identifier);
        if (handlers.Count == 0)
        {
            Volatile.Read(ref _fallback)?.Invoke(_identifier, StringParserAction.Start, null);
            return;
        }
        for (int index = handlers.Count - 1; index >= 0; index--)
        {
            handlers[index].Start();
        }
    }

    private void PutPayload(ReadOnlySpan<uint> data)
    {
        IReadOnlyList<IOscParserHandler> handlers = ActiveHandlers;
        if (handlers.Count == 0)
        {
            Action<int, StringParserAction, object?>? fallback = Volatile.Read(ref _fallback);
            if (fallback is not null)
            {
                fallback(_identifier, StringParserAction.Put, TextDecoder.Utf32ToString(data));
            }
            return;
        }
        for (int index = handlers.Count - 1; index >= 0; index--)
        {
            handlers[index].Put(data);
        }
    }

    private void ClearState()
    {
        _state = OscParserState.Start;
        _identifier = -1;
        ClearActive();
    }
}

namespace XtermSharp.Internal.Parser;

internal sealed class OscStringHandler(
    Func<string, ValueTask<bool>> handler,
    int payloadLimit = StringPayloadHandler.DefaultPayloadLimit)
    : StringPayloadHandler(payloadLimit), IOscParserHandler
{
    public void Start() => StartPayload();
    public void Put(ReadOnlySpan<uint> data) => PutPayload(data);
    public ValueTask<bool> EndAsync(bool success) => EndPayloadAsync(success, handler);
}

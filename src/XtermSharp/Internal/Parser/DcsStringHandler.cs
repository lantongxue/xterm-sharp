namespace XtermSharp.Internal.Parser;

internal sealed class DcsStringHandler(
    Func<string, CsiParameters, ValueTask<bool>> handler,
    int payloadLimit = StringPayloadHandler.DefaultPayloadLimit)
    : StringPayloadHandler(payloadLimit), IDcsParserHandler
{
    private CsiParameters _parameters = new([]);

    public void Hook(CsiParameters parameters)
    {
        _parameters = parameters;
        StartPayload();
    }

    public void Put(ReadOnlySpan<uint> data) => PutPayload(data);

    public ValueTask<bool> UnhookAsync(bool success) =>
        EndPayloadAsync(success, value => handler(value, _parameters));
}

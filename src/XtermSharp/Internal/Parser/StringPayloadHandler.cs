using XtermSharp.Internal.Utilities.Text;

namespace XtermSharp.Internal.Parser;

internal abstract class StringPayloadHandler
{
    internal const int DefaultPayloadLimit = 10_000_000;

    private readonly LimitedStringBuilder _data;
    private bool _hitLimit;

    protected StringPayloadHandler(int payloadLimit)
    {
        _data = new LimitedStringBuilder(payloadLimit);
    }

    protected void StartPayload()
    {
        _data.Reset();
        _hitLimit = false;
    }

    protected void PutPayload(ReadOnlySpan<uint> data)
    {
        if (!_hitLimit && _data.AppendUtf32(data))
        {
            _hitLimit = true;
        }
    }

    protected async ValueTask<bool> EndPayloadAsync(
        bool success,
        Func<string, ValueTask<bool>> callback)
    {
        try
        {
            if (!_hitLimit && success)
            {
                return await callback(_data.ToString()).ConfigureAwait(false);
            }
            return false;
        }
        finally
        {
            StartPayload();
        }
    }
}

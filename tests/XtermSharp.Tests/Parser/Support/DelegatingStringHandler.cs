using XtermSharp.Internal.Parser;

namespace XtermSharp.Tests.Parser;

internal sealed class DelegatingStringHandler(Func<bool, ValueTask<bool>> complete) :
    IOscParserHandler,
    IDcsParserHandler,
    IApcParserHandler
{
    public void Start()
    {
    }

    public void Hook(CsiParameters parameters)
    {
    }

    public void Put(ReadOnlySpan<uint> data)
    {
    }

    public ValueTask<bool> EndAsync(bool success) => complete(success);

    public ValueTask<bool> UnhookAsync(bool success) => complete(success);
}

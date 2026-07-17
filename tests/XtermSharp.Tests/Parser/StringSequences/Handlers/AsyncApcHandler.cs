using XtermSharp.Internal;
using XtermSharp.Internal.Parser;

namespace XtermSharp.Tests.Parser;

internal sealed class AsyncApcHandler(string name, List<string> reports, bool returnFalse) : IApcParserHandler
{
    public void Start() => reports.Add($"{name}:start");
    public void Put(ReadOnlySpan<uint> data) => reports.Add($"{name}:put:{TextDecoder.Utf32ToString(data)}");
    public async ValueTask<bool> EndAsync(bool success)
    {
        await Task.Yield();
        reports.Add($"{name}:end:{success}");
        return !returnFalse;
    }
}

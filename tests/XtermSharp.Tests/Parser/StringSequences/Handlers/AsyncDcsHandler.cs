using XtermSharp.Internal;
using XtermSharp.Internal.Parser;

namespace XtermSharp.Tests.Parser.StringSequences.Handlers;

internal sealed class AsyncDcsHandler(string name, List<string> reports, bool returnFalse) : IDcsParserHandler
{
    public void Hook(CsiParameters parameters) => reports.Add($"{name}:hook");
    public void Put(ReadOnlySpan<uint> data) => reports.Add($"{name}:put:{TextDecoder.Utf32ToString(data)}");
    public async ValueTask<bool> UnhookAsync(bool success)
    {
        await Task.Yield();
        reports.Add($"{name}:unhook:{success}");
        return !returnFalse;
    }
}

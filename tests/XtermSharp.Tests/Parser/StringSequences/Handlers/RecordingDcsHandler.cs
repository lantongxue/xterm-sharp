using XtermSharp.Internal;
using XtermSharp.Internal.Parser;

namespace XtermSharp.Tests.Parser.StringSequences.Handlers;

internal sealed class RecordingDcsHandler(string name, List<string> reports, bool returnFalse) : IDcsParserHandler
{
    public void Hook(CsiParameters parameters) => reports.Add($"{name}:hook:{string.Join(',', parameters.Values)}");
    public void Put(ReadOnlySpan<uint> data) => reports.Add($"{name}:put:{TextDecoder.Utf32ToString(data)}");
    public ValueTask<bool> UnhookAsync(bool success)
    {
        reports.Add($"{name}:unhook:{success}");
        return ValueTask.FromResult(!returnFalse);
    }
}

using XtermSharp.Internal;
using XtermSharp.Internal.Parser;

namespace XtermSharp.Tests.Parser;

internal sealed class RecordingApcHandler(string name, List<string> reports, bool returnFalse) : IApcParserHandler
{
    public void Start() => reports.Add($"{name}:start");
    public void Put(ReadOnlySpan<uint> data) => reports.Add($"{name}:put:{TextDecoder.Utf32ToString(data)}");
    public ValueTask<bool> EndAsync(bool success)
    {
        reports.Add($"{name}:end:{success}");
        return ValueTask.FromResult(!returnFalse);
    }
}

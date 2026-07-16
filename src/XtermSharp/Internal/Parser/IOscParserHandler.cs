namespace XtermSharp.Internal.Parser;

internal interface IOscParserHandler
{
    void Start();
    void Put(ReadOnlySpan<uint> data);
    ValueTask<bool> EndAsync(bool success);
}

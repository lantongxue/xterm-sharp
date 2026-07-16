namespace XtermSharp.Internal.Parser;

internal interface IApcParserHandler
{
    void Start();
    void Put(ReadOnlySpan<uint> data);
    ValueTask<bool> EndAsync(bool success);
}

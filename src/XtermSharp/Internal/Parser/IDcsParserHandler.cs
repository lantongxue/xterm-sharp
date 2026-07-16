namespace XtermSharp.Internal.Parser;

internal interface IDcsParserHandler
{
    void Hook(CsiParameters parameters);
    void Put(ReadOnlySpan<uint> data);
    ValueTask<bool> UnhookAsync(bool success);
}

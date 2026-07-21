namespace XtermSharp.Parsing;

public interface ITerminalParser
{
    IDisposable RegisterCsiHandler(FunctionIdentifier identifier, Func<CsiParameters, ValueTask<bool>> handler);
    IDisposable RegisterEscHandler(FunctionIdentifier identifier, Func<ValueTask<bool>> handler);
    IDisposable RegisterOscHandler(int identifier, Func<string, ValueTask<bool>> handler);
    IDisposable RegisterOscHandler(
        int identifier,
        Func<string, ITerminalParserContext, ValueTask<bool>> handler);
    IDisposable RegisterDcsHandler(FunctionIdentifier identifier, Func<string, CsiParameters, ValueTask<bool>> handler);
    IDisposable RegisterApcHandler(FunctionIdentifier identifier, Func<string, ValueTask<bool>> handler);
}

namespace XtermSharp.Internal.Parser;

internal sealed class TerminalParserContext(Action<string> sendResponse) : ITerminalParserContext
{
    private readonly object _gate = new();
    private Action<string>? _sendResponse = sendResponse;

    public void SendResponse(string data)
    {
        ArgumentNullException.ThrowIfNull(data);
        lock (_gate)
        {
            if (_sendResponse is null)
            {
                throw new InvalidOperationException("The parser handler context is no longer active.");
            }
            _sendResponse(data);
        }
    }

    public void Complete()
    {
        lock (_gate)
        {
            _sendResponse = null;
        }
    }
}

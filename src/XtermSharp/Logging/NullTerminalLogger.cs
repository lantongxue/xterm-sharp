namespace XtermSharp;

internal sealed class NullTerminalLogger : ITerminalLogger
{
    public static NullTerminalLogger Instance { get; } = new();

    private NullTerminalLogger()
    {
    }

    public void Log(TerminalLogLevel level, string message, Exception? exception = null)
    {
    }
}

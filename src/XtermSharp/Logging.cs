namespace XtermSharp;

public enum TerminalLogLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error
}

public interface ITerminalLogger
{
    void Log(TerminalLogLevel level, string message, Exception? exception = null);
}

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


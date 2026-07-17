namespace XtermSharp;

public interface ITerminalLogger
{
    void Log(TerminalLogLevel level, string message, Exception? exception = null);
}

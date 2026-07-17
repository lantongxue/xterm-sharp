namespace XtermSharp.Logging;

public interface ITerminalLogger
{
    void Log(TerminalLogLevel level, string message, Exception? exception = null);
}

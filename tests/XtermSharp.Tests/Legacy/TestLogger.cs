using XtermSharp;

sealed class TestLogger : ITerminalLogger
{
    public int ExceptionCount { get; private set; }
    public void Log(TerminalLogLevel level, string message, Exception? exception = null)
    {
        if (exception is not null)
        {
            ExceptionCount++;
        }
    }
}

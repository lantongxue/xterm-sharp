namespace XtermSharp.Events;

public sealed class TerminalDataEventArgs(long revision, string data, bool isBinary = false)
    : TerminalEventArgs(revision)
{
    public string Data { get; } = data;
    public bool IsBinary { get; } = isBinary;
}

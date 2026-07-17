namespace XtermSharp.Tests.Addon;

internal sealed class RecordingAddon : ITerminalAddon
{
    public Terminal? ActivatedTerminal { get; private set; }

    public int ActivationCount { get; private set; }

    public int DisposeCount { get; private set; }

    public void Activate(Terminal terminal)
    {
        ActivatedTerminal = terminal;
        ActivationCount++;
    }

    public void Dispose() => DisposeCount++;
}

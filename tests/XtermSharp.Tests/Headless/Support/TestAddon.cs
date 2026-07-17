namespace XtermSharp.Tests.Headless;

internal sealed class TestAddon(Func<Terminal, int> activation) : ITerminalAddon
{
    public int ActivationValue { get; private set; }
    public bool IsDisposed { get; private set; }

    public void Activate(Terminal terminal) => ActivationValue = activation(terminal);

    public void Dispose() => IsDisposed = true;
}

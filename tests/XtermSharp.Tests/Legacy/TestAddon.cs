using XtermSharp;

namespace XtermSharp.Tests.Legacy;

sealed class TestAddon : ITerminalAddon
{
    public bool Activated { get; private set; }
    public bool Disposed { get; private set; }
    public void Activate(Terminal terminal) => Activated = true;
    public void Dispose() => Disposed = true;
}

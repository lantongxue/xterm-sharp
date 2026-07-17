namespace XtermSharp;

public interface ITerminalAddon : IDisposable
{
    void Activate(Terminal terminal);
}

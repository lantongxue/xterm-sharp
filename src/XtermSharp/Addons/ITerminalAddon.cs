namespace XtermSharp.Addons;

public interface ITerminalAddon : IDisposable
{
    void Activate(Terminal terminal);
}

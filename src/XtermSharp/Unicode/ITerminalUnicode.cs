namespace XtermSharp.Unicode;

public interface ITerminalUnicode
{
    string ActiveVersion { get; set; }
    IReadOnlyCollection<string> Versions { get; }
    IDisposable Register(IUnicodeProvider provider);
}

using System.Text;

namespace XtermSharp;

public interface IUnicodeProvider
{
    string Version { get; }
    int GetWidth(Rune rune);

    UnicodeCharacterProperties GetProperties(Rune rune, Rune? preceding)
    {
        int width = GetWidth(rune);
        return new UnicodeCharacterProperties(width, width == 0 && preceding is not null);
    }
}

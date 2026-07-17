using System.Text;

namespace XtermSharp.Unicode;

public interface IUnicodeProvider
{
    string Version { get; }
    int GetWidth(Rune rune);

    UnicodeCharacterProperties GetProperties(Rune rune, Rune? preceding)
    {
        int width = GetWidth(rune);
        return new UnicodeCharacterProperties(width, width == 0 && preceding is not null);
    }

    UnicodeCharacterProperties GetProperties(
        Rune rune,
        UnicodeCharacterProperties preceding,
        Rune? precedingRune)
    {
        UnicodeCharacterProperties properties = GetProperties(rune, precedingRune);
        return properties.JoinPrevious && preceding.Width > properties.Width
            ? properties with { Width = preceding.Width }
            : properties;
    }
}

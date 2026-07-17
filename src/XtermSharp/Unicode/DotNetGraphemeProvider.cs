using System.Globalization;
using System.Text;

namespace XtermSharp;

/// <summary>Provider using .NET Unicode categories and joining format/combining code points.</summary>
public sealed class DotNetGraphemeProvider : IUnicodeProvider
{
    public const string VersionName = "dotnet-graphemes";
    private readonly UnicodeV11Provider _width = new();

    public string Version => VersionName;

    public int GetWidth(Rune rune) => _width.GetWidth(rune);

    public UnicodeCharacterProperties GetProperties(Rune rune, Rune? preceding)
    {
        int value = rune.Value;
        UnicodeCategory category = Rune.GetUnicodeCategory(rune);
        bool join = preceding is not null &&
            (preceding.Value.Value == 0x200D || value == 0x200D ||
             value is >= 0x1F3FB and <= 0x1F3FF || value is >= 0xFE00 and <= 0xFE0F ||
             category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format);
        return new UnicodeCharacterProperties(join ? 0 : GetWidth(rune), join);
    }
}

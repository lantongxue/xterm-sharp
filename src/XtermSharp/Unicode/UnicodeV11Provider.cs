using System.Globalization;
using System.Text;

namespace XtermSharp.Unicode;

/// <summary>A practical Unicode 11 width provider with modern emoji ranges.</summary>
public sealed class UnicodeV11Provider : IUnicodeProvider
{
    public const string VersionName = "11";

    public string Version => VersionName;

    public int GetWidth(Rune rune)
    {
        int value = rune.Value;
        if (value < 32 || value is >= 0x7F and < 0xA0)
        {
            return 0;
        }

        UnicodeCategory category = Rune.GetUnicodeCategory(rune);
        if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format)
        {
            return 0;
        }

        if (value is >= 0x1100 and <= 0x115F ||
            value is >= 0x231A and <= 0x231B ||
            value is >= 0x2329 and <= 0x232A ||
            value is >= 0x2E80 and <= 0xA4CF && value != 0x303F ||
            value is >= 0xAC00 and <= 0xD7A3 ||
            value is >= 0xF900 and <= 0xFAFF ||
            value is >= 0xFE10 and <= 0xFE19 ||
            value is >= 0xFE30 and <= 0xFE6B ||
            value is >= 0xFF01 and <= 0xFF60 ||
            value is >= 0xFFE0 and <= 0xFFE6 ||
            value is >= 0x1F300 and <= 0x1FAFF ||
            value is >= 0x20000 and <= 0x3FFFD)
        {
            return 2;
        }
        return 1;
    }
}

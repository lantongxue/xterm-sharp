using XtermSharp.Internal;

namespace XtermSharp.Tests.Input;

public sealed class UnicodeV6Tests
{
    [UpstreamFact("XTJS-0001", "wcwidth should match all values from the old implementation")]
    public void Width_matches_the_legacy_implementation_for_the_full_bmp()
    {
        var provider = new UnicodeV6Provider();
        for (int codePoint = 0; codePoint < 65_536; codePoint++)
        {
            Assert.Equal(LegacyWidth(codePoint), provider.GetWidth(codePoint));
        }
    }

    private static int LegacyWidth(int codePoint)
    {
        if (codePoint == 0 || codePoint < 32 || codePoint is >= 0x7F and < 0xA0)
        {
            return 0;
        }
        if (UnicodeV6Provider.IsCombining(codePoint))
        {
            return 0;
        }
        return codePoint >= 0x1100 &&
            (codePoint <= 0x115F ||
             codePoint is 0x2329 or 0x232A ||
             codePoint is >= 0x2E80 and <= 0xA4CF && codePoint != 0x303F ||
             codePoint is >= 0xAC00 and <= 0xD7A3 ||
             codePoint is >= 0xF900 and <= 0xFAFF ||
             codePoint is >= 0xFE10 and <= 0xFE19 ||
             codePoint is >= 0xFE30 and <= 0xFE6F ||
             codePoint is >= 0xFF00 and <= 0xFF60 ||
             codePoint is >= 0xFFE0 and <= 0xFFE6)
            ? 2
            : 1;
    }
}

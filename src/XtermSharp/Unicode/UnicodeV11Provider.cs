using System.Text;

namespace XtermSharp.Unicode;

/// <summary>Width provider matching the pinned xterm.js Unicode 11 addon.</summary>
public sealed class UnicodeV11Provider : IUnicodeProvider
{
    public const string VersionName = "11";

    private static readonly byte[] BmpWidths = CreateBmpWidths();

    public string Version => VersionName;

    public int GetWidth(Rune rune) => GetWidth(rune.Value);

    internal int GetWidth(int value)
    {
        if (value < 32 || value is >= 0x7F and < 0xA0)
        {
            return 0;
        }
        if (value < 0x10000)
        {
            return BmpWidths[value];
        }
        if (IsInRanges(value, UnicodeV11Data.HighCombiningRanges))
        {
            return 0;
        }
        return IsInRanges(value, UnicodeV11Data.HighWideRanges) ? 2 : 1;
    }

    private static byte[] CreateBmpWidths()
    {
        var widths = new byte[0x10000];
        Array.Fill(widths, (byte)1);
        widths[0] = 0;
        Array.Fill(widths, (byte)0, 1, 31);
        Array.Fill(widths, (byte)0, 0x7F, 0x21);
        ApplyRanges(widths, UnicodeV11Data.BmpCombiningRanges, 0);
        ApplyRanges(widths, UnicodeV11Data.BmpWideRanges, 2);
        return widths;
    }

    private static void ApplyRanges(byte[] widths, ReadOnlySpan<int> ranges, byte width)
    {
        for (int index = 0; index < ranges.Length; index += 2)
        {
            int start = ranges[index];
            int count = ranges[index + 1] - start + 1;
            Array.Fill(widths, width, start, count);
        }
    }

    private static bool IsInRanges(int value, ReadOnlySpan<int> ranges)
    {
        int low = 0;
        int high = ranges.Length / 2 - 1;
        while (low <= high)
        {
            int middle = (low + high) >>> 1;
            int start = ranges[middle * 2];
            int end = ranges[middle * 2 + 1];
            if (value < start)
            {
                high = middle - 1;
            }
            else if (value > end)
            {
                low = middle + 1;
            }
            else
            {
                return true;
            }
        }
        return false;
    }
}

using System.Text;

namespace XtermSharp.Internal.Decoding;

/// <summary>
/// UTF-32 conversion helpers matching the streaming decoders used by xterm.js.
/// </summary>
internal static class TextDecoder
{
    public static string StringFromCodePoint(uint codePoint)
    {
        if (codePoint > 0xFFFF)
        {
            codePoint -= 0x10000;
            return string.Concat(
                (char)((codePoint >> 10) + 0xD800),
                (char)((codePoint % 0x400) + 0xDC00));
        }

        return ((char)codePoint).ToString();
    }

    public static string Utf32ToString(ReadOnlySpan<uint> data, int start = 0, int? end = null)
    {
        int stop = end ?? data.Length;
        var result = new StringBuilder(stop - start);
        for (int i = start; i < stop; i++)
        {
            uint codePoint = data[i];
            if (codePoint > 0xFFFF)
            {
                codePoint -= 0x10000;
                result.Append((char)((codePoint >> 10) + 0xD800));
                result.Append((char)((codePoint % 0x400) + 0xDC00));
            }
            else
            {
                result.Append((char)codePoint);
            }
        }

        return result.ToString();
    }
}

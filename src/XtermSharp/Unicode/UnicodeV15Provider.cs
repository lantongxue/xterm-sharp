using System.Text;

namespace XtermSharp.Unicode;

/// <summary>Unicode 15 width and optional extended-grapheme provider.</summary>
public sealed class UnicodeV15Provider : IUnicodeProvider
{
    public const string VersionName = "15";
    public const string GraphemeVersionName = "15-graphemes";

    private const byte WidthMask = 0x03;
    private const byte GraphemeBreakMask = 0x3C;
    private const int GraphemeBreakShift = 2;
    private const byte ExtendedPictographic = 0x40;

    private const int StateGraphemeBreakMask = 0x0F;
    private const int StateRegionalIndicatorOdd = 0x10;
    private const int StateEmojiShift = 5;
    private const int StateEmojiMask = 0x60;
    private const int EmojiNone = 0;
    private const int EmojiExtendSequence = 1;
    private const int EmojiZwjSequence = 2;

    public UnicodeV15Provider(bool handleGraphemes = true)
    {
        HandleGraphemes = handleGraphemes;
    }

    public string Version => HandleGraphemes ? GraphemeVersionName : VersionName;

    public bool HandleGraphemes { get; }

    public bool AmbiguousCharactersAreWide { get; set; }

    public int GetWidth(Rune rune)
    {
        byte info = GetInfo(rune.Value);
        GraphemeBreakKind kind = GetGraphemeBreak(info);
        if (kind is GraphemeBreakKind.Extend or GraphemeBreakKind.Prepend)
        {
            return 0;
        }

        int widthInfo = info & WidthMask;
        return widthInfo >= 2 && (widthInfo == 3 || AmbiguousCharactersAreWide) ? 2 : 1;
    }

    public UnicodeCharacterProperties GetProperties(Rune rune, Rune? preceding)
    {
        UnicodeCharacterProperties previous = preceding is Rune previousRune
            ? GetProperties(previousRune, default, null)
            : default;
        return GetProperties(rune, previous, preceding);
    }

    public UnicodeCharacterProperties GetProperties(
        Rune rune,
        UnicodeCharacterProperties preceding,
        Rune? precedingRune)
    {
        byte info = GetInfo(rune.Value);
        GraphemeBreakKind currentKind = GetGraphemeBreak(info);
        bool currentExtendedPictographic = (info & ExtendedPictographic) != 0;
        int width = GetPrintWidth(rune.Value, info);

        if (!HandleGraphemes)
        {
            return new UnicodeCharacterProperties(width, false);
        }

        int previousState = preceding.State;
        GraphemeBreakKind previousKind = (GraphemeBreakKind)(previousState & StateGraphemeBreakMask);
        bool join = preceding.Encode() != 0 &&
            ShouldJoin(previousState, previousKind, currentKind, currentExtendedPictographic);
        if (join)
        {
            width = Math.Max(width, preceding.Width);
            if (previousKind == GraphemeBreakKind.RegionalIndicator &&
                currentKind == GraphemeBreakKind.RegionalIndicator)
            {
                width = 2;
            }
        }

        int state = GetNextState(
            previousState,
            previousKind,
            currentKind,
            currentExtendedPictographic);
        return new UnicodeCharacterProperties(width, join, state);
    }

    internal static byte GetInfo(int codePoint)
    {
        int index = Array.BinarySearch(UnicodeV15Data.RunStarts, codePoint);
        if (index < 0)
        {
            index = ~index - 1;
        }
        return index >= 0 ? UnicodeV15Data.RunValues[index] : (byte)0;
    }

    private int GetPrintWidth(int codePoint, byte info)
    {
        int widthInfo = info & WidthMask;
        if (widthInfo >= 2)
        {
            return widthInfo == 3 || AmbiguousCharactersAreWide || codePoint == 0xFE0F ? 2 : 1;
        }
        return 1;
    }

    private static bool ShouldJoin(
        int previousState,
        GraphemeBreakKind previousKind,
        GraphemeBreakKind currentKind,
        bool currentExtendedPictographic)
    {
        if (previousKind == GraphemeBreakKind.CarriageReturn && currentKind == GraphemeBreakKind.LineFeed)
        {
            return true;
        }
        if (IsControl(previousKind) || IsControl(currentKind))
        {
            return false;
        }
        if (previousKind == GraphemeBreakKind.HangulL &&
            currentKind is GraphemeBreakKind.HangulL or GraphemeBreakKind.HangulV or
                GraphemeBreakKind.HangulLv or GraphemeBreakKind.HangulLvt)
        {
            return true;
        }
        if (previousKind is GraphemeBreakKind.HangulLv or GraphemeBreakKind.HangulV &&
            currentKind is GraphemeBreakKind.HangulV or GraphemeBreakKind.HangulT)
        {
            return true;
        }
        if (previousKind is GraphemeBreakKind.HangulLvt or GraphemeBreakKind.HangulT &&
            currentKind == GraphemeBreakKind.HangulT)
        {
            return true;
        }
        if (currentKind is GraphemeBreakKind.Extend or GraphemeBreakKind.ZeroWidthJoiner or
            GraphemeBreakKind.SpacingMark)
        {
            return true;
        }
        if (previousKind == GraphemeBreakKind.Prepend)
        {
            return true;
        }

        int emojiState = (previousState & StateEmojiMask) >> StateEmojiShift;
        if (emojiState == EmojiZwjSequence && currentExtendedPictographic)
        {
            return true;
        }
        return previousKind == GraphemeBreakKind.RegionalIndicator &&
            currentKind == GraphemeBreakKind.RegionalIndicator &&
            (previousState & StateRegionalIndicatorOdd) != 0;
    }

    private static int GetNextState(
        int previousState,
        GraphemeBreakKind previousKind,
        GraphemeBreakKind currentKind,
        bool currentExtendedPictographic)
    {
        int state = (int)currentKind;
        if (currentKind == GraphemeBreakKind.RegionalIndicator)
        {
            bool previousOdd = (previousState & StateRegionalIndicatorOdd) != 0;
            if (previousKind != GraphemeBreakKind.RegionalIndicator || !previousOdd)
            {
                state |= StateRegionalIndicatorOdd;
            }
        }

        int previousEmojiState = (previousState & StateEmojiMask) >> StateEmojiShift;
        int emojiState = EmojiNone;
        if (currentExtendedPictographic)
        {
            emojiState = EmojiExtendSequence;
        }
        else if (currentKind == GraphemeBreakKind.Extend && previousEmojiState == EmojiExtendSequence)
        {
            emojiState = EmojiExtendSequence;
        }
        else if (currentKind == GraphemeBreakKind.ZeroWidthJoiner &&
            previousEmojiState == EmojiExtendSequence)
        {
            emojiState = EmojiZwjSequence;
        }
        return state | (emojiState << StateEmojiShift);
    }

    private static GraphemeBreakKind GetGraphemeBreak(byte info) =>
        (GraphemeBreakKind)((info & GraphemeBreakMask) >> GraphemeBreakShift);

    private static bool IsControl(GraphemeBreakKind kind) =>
        kind is GraphemeBreakKind.CarriageReturn or GraphemeBreakKind.LineFeed or GraphemeBreakKind.Control;

    private enum GraphemeBreakKind : byte
    {
        Other,
        CarriageReturn,
        LineFeed,
        Control,
        Extend,
        RegionalIndicator,
        Prepend,
        SpacingMark,
        HangulL,
        HangulV,
        HangulT,
        HangulLv,
        HangulLvt,
        ZeroWidthJoiner
    }
}

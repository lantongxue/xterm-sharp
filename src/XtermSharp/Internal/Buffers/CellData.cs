using System.Text;

namespace XtermSharp.Internal;

internal struct CellData
{
    public int CodePoint;
    public string? CombinedText;
    public byte Width;
    public CellStyle Style;
    public ExtendedCellAttributes? Extended;

    public static CellData Blank(CellStyle style) => new()
    {
        CodePoint = 0,
        CombinedText = null,
        Width = 1,
        Style = style,
        Extended = CreateExtended(style)
    };

    public static CellData FromRune(Rune rune, byte width, CellStyle style) => new()
    {
        CodePoint = rune.Value,
        CombinedText = null,
        Width = width,
        Style = style,
        Extended = CreateExtended(style)
    };

    public static CellData FromText(string text, byte width, CellStyle style)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            return new CellData
            {
                CodePoint = 0,
                CombinedText = null,
                Width = width,
                Style = style,
                Extended = CreateExtended(style)
            };
        }

        Rune first = Rune.GetRuneAt(text, 0);
        int runeCount = 0;
        Rune last = first;
        foreach (Rune rune in text.EnumerateRunes())
        {
            last = rune;
            runeCount++;
        }
        return new CellData
        {
            CodePoint = last.Value,
            CombinedText = runeCount > 1 ? text : null,
            Width = width,
            Style = style,
            Extended = CreateExtended(style)
        };
    }

    public readonly bool IsCombined => CombinedText is not null;

    public readonly bool HasContent => CodePoint != 0 || CombinedText is not null;

    public readonly string GetText()
    {
        if (CombinedText is not null)
        {
            return CombinedText;
        }
        return CodePoint == 0 ? string.Empty : char.ConvertFromUtf32(CodePoint);
    }

    public readonly bool AttributesEquals(in CellData other) =>
        Style.Foreground == other.Style.Foreground &&
        Style.Background == other.Style.Background &&
        Style.UnderlineColor == other.Style.UnderlineColor &&
        Style.Attributes == other.Style.Attributes &&
        Style.UnderlineStyle == other.Style.UnderlineStyle &&
        Style.IsProtected == other.Style.IsProtected;

    public void UpdateExtended()
    {
        if (Extended is null)
        {
            return;
        }
        Style = Style with
        {
            UnderlineColor = Extended.UnderlineColor,
            UnderlineStyle = Extended.UnderlineStyle,
            HyperlinkId = Extended.UrlId
        };
    }

    public TerminalCellSnapshot ToSnapshot() => new(
        GetText(),
        CodePoint,
        Width,
        Style.Foreground,
        Style.Background,
        Style.UnderlineColor,
        Style.Attributes,
        Style.UnderlineStyle,
        Style.HyperlinkId,
        Style.IsProtected);

    private static ExtendedCellAttributes? CreateExtended(CellStyle style)
    {
        if (style.UnderlineColor == TerminalColor.Default &&
            style.UnderlineStyle == TerminalUnderlineStyle.None &&
            style.HyperlinkId == 0)
        {
            return null;
        }
        return new ExtendedCellAttributes
        {
            UnderlineColor = style.UnderlineColor,
            UnderlineStyle = style.UnderlineStyle,
            UrlId = style.HyperlinkId
        };
    }
}

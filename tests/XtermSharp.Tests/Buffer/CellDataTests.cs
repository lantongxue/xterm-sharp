using XtermSharp.Internal;

namespace XtermSharp.Tests.Buffer;

public sealed class CellDataTests
{
    [UpstreamFact("XTJS-0135", "CellData attributesEquals returns true for same attributes with different chars")]
    public void AttributesEquals_ReturnsTrueForSameAttributesWithDifferentCharacters()
    {
        CellData first = CreateStyledCell("A", TerminalUnderlineStyle.Double, 45);
        CellData second = CreateStyledCell("B", TerminalUnderlineStyle.Double, 45);
        Assert.True(first.AttributesEquals(second));
    }

    [UpstreamFact("XTJS-0136", "CellData attributesEquals detects underline style changes")]
    public void AttributesEquals_DetectsUnderlineStyleChanges()
    {
        CellData first = CreateStyledCell("A", TerminalUnderlineStyle.Double, 45);
        CellData second = CreateStyledCell("B", TerminalUnderlineStyle.Single, 45);
        Assert.False(first.AttributesEquals(second));
    }

    [UpstreamFact("XTJS-0137", "CellData attributesEquals detects underline color changes")]
    public void AttributesEquals_DetectsUnderlineColorChanges()
    {
        CellData first = CreateStyledCell("A", TerminalUnderlineStyle.Single, 45);
        CellData second = CreateStyledCell("B", TerminalUnderlineStyle.Single, 46);
        Assert.False(first.AttributesEquals(second));
    }

    [UpstreamFact("XTJS-0138", "CellData attributesEquals ignores underline variant offsets")]
    public void AttributesEquals_IgnoresUnderlineVariantOffsets()
    {
        CellData first = CreateStyledCell("A", TerminalUnderlineStyle.Single, 45);
        CellData second = CreateStyledCell("B", TerminalUnderlineStyle.Single, 45);
        first.Extended!.UnderlineVariantOffset = 1;
        second.Extended!.UnderlineVariantOffset = 3;
        first.UpdateExtended();
        second.UpdateExtended();
        Assert.True(first.AttributesEquals(second));
    }

    [UpstreamFact("XTJS-0139", "CellData attributesEquals ignores url ids")]
    public void AttributesEquals_IgnoresUrlIds()
    {
        CellData first = CreateStyledCell("A", TerminalUnderlineStyle.Single, 45);
        CellData second = CreateStyledCell("B", TerminalUnderlineStyle.Single, 45);
        first.Extended!.UrlId = 1;
        second.Extended!.UrlId = 2;
        first.UpdateExtended();
        second.UpdateExtended();
        Assert.True(first.AttributesEquals(second));
    }

    private static CellData CreateStyledCell(string text, TerminalUnderlineStyle underlineStyle, int underlineColor)
    {
        var style = new CellStyle(
            TerminalColor.Palette(12),
            TerminalColor.Palette(2),
            TerminalColor.Palette(underlineColor),
            CellAttributes.Bold | CellAttributes.Underline | CellAttributes.Italic,
            underlineStyle,
            0,
            false);
        CellData cell = CellData.FromText(text, 1, style);
        cell.Extended = new ExtendedCellAttributes
        {
            UnderlineStyle = underlineStyle,
            UnderlineColor = TerminalColor.Palette(underlineColor)
        };
        return cell;
    }
}

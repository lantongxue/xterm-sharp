namespace XtermSharp.Internal.Buffers;

internal sealed class AttributeData
{
    public TerminalColor Foreground { get; set; } = TerminalColor.Default;
    public bool Underline { get; set; }
    public bool HasExtendedAttributes { get; set; }
    public ExtendedCellAttributes Extended { get; set; } = new();

    public bool HasExtendedAttrs() => HasExtendedAttributes;

    public int GetUnderlineColor()
    {
        TerminalColor color = HasExtendedAttributes && Extended.UnderlineColor != TerminalColor.Default
            ? Extended.UnderlineColor
            : Foreground;
        return color.Mode == TerminalColorMode.Default ? -1 : color.Value;
    }

    public TerminalColorMode GetUnderlineColorMode() =>
        HasExtendedAttributes ? Extended.UnderlineColor.Mode : Foreground.Mode;

    public bool IsUnderlineColorRgb() => GetUnderlineColorMode() == TerminalColorMode.Rgb;

    public bool IsUnderlineColorPalette() => GetUnderlineColorMode() == TerminalColorMode.Palette;

    public bool IsUnderlineColorDefault() => GetUnderlineColorMode() == TerminalColorMode.Default;

    public TerminalUnderlineStyle GetUnderlineStyle()
    {
        if (!Underline)
        {
            return TerminalUnderlineStyle.None;
        }
        return HasExtendedAttributes ? Extended.UnderlineStyle : TerminalUnderlineStyle.Single;
    }

    public int GetUnderlineVariantOffset() => Extended.UnderlineVariantOffset & 7;
}

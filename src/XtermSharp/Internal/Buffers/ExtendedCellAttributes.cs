namespace XtermSharp.Internal.Buffers;

internal sealed class ExtendedCellAttributes
{
    public TerminalColor UnderlineColor { get; set; } = TerminalColor.Default;
    public TerminalUnderlineStyle UnderlineStyle { get; set; } = TerminalUnderlineStyle.None;
    public int UnderlineVariantOffset { get; set; }
    public int UrlId { get; set; }

    public ExtendedCellAttributes Clone() => new()
    {
        UnderlineColor = UnderlineColor,
        UnderlineStyle = UnderlineStyle,
        UnderlineVariantOffset = UnderlineVariantOffset,
        UrlId = UrlId
    };
}

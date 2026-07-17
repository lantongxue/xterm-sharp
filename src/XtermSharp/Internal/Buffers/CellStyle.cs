namespace XtermSharp.Internal;

internal readonly record struct CellStyle(
    TerminalColor Foreground,
    TerminalColor Background,
    TerminalColor UnderlineColor,
    CellAttributes Attributes,
    TerminalUnderlineStyle UnderlineStyle,
    int HyperlinkId,
    bool IsProtected)
{
    public static CellStyle Default => new(
        TerminalColor.Default,
        TerminalColor.Default,
        TerminalColor.Default,
        CellAttributes.None,
        TerminalUnderlineStyle.None,
        0,
        false);

    public CellStyle ForErase() => this with
    {
        Foreground = TerminalColor.Default,
        Attributes = CellAttributes.None,
        UnderlineColor = TerminalColor.Default,
        UnderlineStyle = TerminalUnderlineStyle.None,
        HyperlinkId = 0,
        IsProtected = false
    };
}

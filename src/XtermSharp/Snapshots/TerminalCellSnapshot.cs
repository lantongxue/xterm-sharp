namespace XtermSharp;

public readonly record struct TerminalCellSnapshot(
    string Text,
    int CodePoint,
    byte Width,
    TerminalColor Foreground,
    TerminalColor Background,
    TerminalColor UnderlineColor,
    CellAttributes Attributes,
    TerminalUnderlineStyle UnderlineStyle,
    int HyperlinkId,
    bool IsProtected)
{
    public string GetChars() => Text;

    public int GetWidth() => Width;
}

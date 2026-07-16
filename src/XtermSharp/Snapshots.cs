using System.Collections.Immutable;

namespace XtermSharp;

public enum SnapshotScope
{
    Viewport,
    ActiveBuffer,
    AllBuffers
}

public enum TerminalBufferKind
{
    Normal,
    Alternate
}

public enum TerminalColorMode : byte
{
    Default,
    Palette,
    Rgb
}

public readonly record struct TerminalColor(TerminalColorMode Mode, int Value)
{
    public static TerminalColor Default => new(TerminalColorMode.Default, 0);
    public static TerminalColor Palette(int index) => new(TerminalColorMode.Palette, index);
    public static TerminalColor Rgb(byte red, byte green, byte blue) =>
        new(TerminalColorMode.Rgb, (red << 16) | (green << 8) | blue);
}

[Flags]
public enum CellAttributes : ushort
{
    None = 0,
    Bold = 1 << 0,
    Dim = 1 << 1,
    Italic = 1 << 2,
    Underline = 1 << 3,
    Blink = 1 << 4,
    Inverse = 1 << 5,
    Invisible = 1 << 6,
    Strikethrough = 1 << 7,
    Overline = 1 << 8
}

public enum TerminalUnderlineStyle : byte
{
    None,
    Single,
    Double,
    Curly,
    Dotted,
    Dashed
}

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

public sealed record TerminalLineSnapshot(
    bool IsWrapped,
    ImmutableArray<TerminalCellSnapshot> Cells)
{
    public int Length => Cells.Length;

    public TerminalCellSnapshot? GetCell(int column) =>
        (uint)column < (uint)Cells.Length ? Cells[column] : null;

    public string TranslateToString(bool trimRight = false, int startColumn = 0, int? endColumn = null)
    {
        int start = Math.Clamp(startColumn, 0, Cells.Length);
        int end = Math.Clamp(endColumn ?? Cells.Length, start, Cells.Length);
        if (trimRight)
        {
            int trimmedLength = 0;
            for (int index = Cells.Length - 1; index >= 0; index--)
            {
                TerminalCellSnapshot cell = Cells[index];
                if (cell.CodePoint != 0 || cell.Text.Length != 0)
                {
                    trimmedLength = index + cell.Width;
                    break;
                }
            }
            end = Math.Min(end, trimmedLength);
        }
        var builder = new System.Text.StringBuilder();
        while (start < end)
        {
            TerminalCellSnapshot cell = Cells[start];
            builder.Append(cell.Text.Length == 0 ? " " : cell.Text);
            start += cell.Width == 0 ? 1 : cell.Width;
        }
        return builder.ToString();
    }
}

public sealed record TerminalBufferSnapshot(
    TerminalBufferKind Kind,
    int CursorX,
    int CursorY,
    int ViewportY,
    int BaseY,
    ImmutableArray<TerminalLineSnapshot> Lines)
{
    public int Length => Lines.Length;

    public TerminalLineSnapshot? GetLine(int line) =>
        (uint)line < (uint)Lines.Length ? Lines[line] : null;
}

public sealed record TerminalBufferCollection(
    TerminalBufferSnapshot Active,
    TerminalBufferSnapshot Normal,
    TerminalBufferSnapshot Alternate);

public sealed record TerminalModes(
    bool ApplicationCursorKeys,
    bool ApplicationKeypad,
    bool BracketedPaste,
    bool Insert,
    bool Origin,
    bool ReverseWraparound,
    bool SendFocus,
    bool ShowCursor,
    bool SynchronizedOutput,
    bool Wraparound,
    TerminalMouseTrackingMode MouseTracking,
    TerminalMouseEncodingMode MouseEncoding,
    TerminalKittyKeyboardFlags KittyKeyboardFlags,
    bool Win32InputMode,
    TerminalCursorStyle? CursorStyle,
    bool? CursorBlink,
    bool ColorSchemeUpdates);

public enum TerminalCursorStyle
{
    Block,
    Underline,
    Bar
}

public enum TerminalMouseTrackingMode
{
    None,
    X10,
    Vt200,
    Drag,
    Any
}

public enum TerminalMouseEncodingMode
{
    Default,
    Sgr,
    SgrPixels
}

public sealed record TerminalSnapshot(
    long Revision,
    int Columns,
    int Rows,
    TerminalBufferKind ActiveBufferKind,
    TerminalModes Modes,
    TerminalBufferSnapshot ActiveBuffer,
    TerminalBufferSnapshot? NormalBuffer,
    TerminalBufferSnapshot? AlternateBuffer);

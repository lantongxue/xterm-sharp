namespace XtermSharp;

public readonly record struct TerminalColor(TerminalColorMode Mode, int Value)
{
    public static TerminalColor Default => new(TerminalColorMode.Default, 0);
    public static TerminalColor Palette(int index) => new(TerminalColorMode.Palette, index);
    public static TerminalColor Rgb(byte red, byte green, byte blue) =>
        new(TerminalColorMode.Rgb, (red << 16) | (green << 8) | blue);
}

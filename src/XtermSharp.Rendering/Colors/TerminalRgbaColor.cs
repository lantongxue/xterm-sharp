namespace XtermSharp.Rendering.Colors;

public readonly record struct TerminalRgbaColor(byte Red, byte Green, byte Blue, byte Alpha = 255)
{
    public static TerminalRgbaColor FromRgb(int value) => new(
        (byte)((value >> 16) & 0xFF),
        (byte)((value >> 8) & 0xFF),
        (byte)(value & 0xFF));

    public TerminalRgbaColor Blend(TerminalRgbaColor background, double opacity)
    {
        opacity = Math.Clamp(opacity, 0, 1);
        return new TerminalRgbaColor(
            BlendChannel(Red, background.Red, opacity),
            BlendChannel(Green, background.Green, opacity),
            BlendChannel(Blue, background.Blue, opacity),
            BlendChannel(Alpha, background.Alpha, opacity));
    }

    private static byte BlendChannel(byte foreground, byte background, double opacity) =>
        (byte)Math.Clamp(Math.Round(foreground * opacity + background * (1 - opacity)), 0, 255);
}

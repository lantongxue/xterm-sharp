namespace XtermSharp.Rendering.Configuration;

public sealed record TerminalRenderOptions
{
    public string? FontFamily { get; init; }
    public double? FontSize { get; init; }
    public double? LineHeight { get; init; }
    public double? CursorWidth { get; init; }
    public double LetterSpacing { get; init; }
    public bool CursorBlink { get; init; } = true;
    public TerminalCursorStyle CursorStyle { get; init; } = TerminalCursorStyle.Block;
    public bool DrawBoldTextInBrightColors { get; init; } = true;
    public bool RescaleOverlappingGlyphs { get; init; } = true;
    public double MinimumContrastRatio { get; init; } = 1;
    public bool HandleColorRequests { get; init; } = true;
    public TimeSpan SynchronizedOutputTimeout { get; init; } = TimeSpan.FromSeconds(1);

    public TerminalRenderConfiguration Resolve(TerminalOptions terminalOptions, double renderScale = 1) => new(
        FontFamily ?? terminalOptions.FontFamily,
        FontSize ?? terminalOptions.FontSize,
        LineHeight ?? terminalOptions.LineHeight,
        CursorWidth ?? terminalOptions.CursorWidth,
        LetterSpacing,
        CursorBlink,
        CursorStyle,
        DrawBoldTextInBrightColors,
        RescaleOverlappingGlyphs,
        MinimumContrastRatio,
        Math.Max(0.01, renderScale));
}

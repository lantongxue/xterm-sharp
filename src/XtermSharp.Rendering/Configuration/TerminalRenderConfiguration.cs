namespace XtermSharp.Rendering;

public sealed record TerminalRenderConfiguration(
    string FontFamily,
    double FontSize,
    double LineHeight,
    double CursorWidth,
    double LetterSpacing,
    bool CursorBlink,
    TerminalCursorStyle CursorStyle,
    bool DrawBoldTextInBrightColors,
    bool RescaleOverlappingGlyphs,
    double MinimumContrastRatio,
    double RenderScale);

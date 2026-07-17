namespace XtermSharp.Rendering.Geometry;

public readonly record struct TerminalFontMetrics(
    double CellWidth,
    double CellHeight,
    double Baseline,
    double UnderlineOffset,
    double UnderlineThickness,
    double StrikeOffset)
{
    public static TerminalFontMetrics Fallback(double fontSize, double lineHeight, double letterSpacing)
    {
        double width = Math.Max(1, fontSize * 0.6 + letterSpacing);
        double height = Math.Max(1, fontSize * lineHeight);
        return new TerminalFontMetrics(
            width,
            height,
            height * 0.8,
            height * 0.88,
            Math.Max(1, fontSize / 14),
            height * 0.55);
    }
}

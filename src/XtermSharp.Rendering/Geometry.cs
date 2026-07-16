namespace XtermSharp.Rendering;

public readonly record struct TerminalPoint(double X, double Y);

public readonly record struct TerminalSize(double Width, double Height);

public readonly record struct TerminalRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
}

public readonly record struct TerminalThickness(double Left, double Top, double Right, double Bottom)
{
    public TerminalThickness(double uniform) : this(uniform, uniform, uniform, uniform)
    {
    }

    public double Horizontal => Left + Right;
    public double Vertical => Top + Bottom;
}

public readonly record struct TerminalViewport(
    double Width,
    double Height,
    double RenderScale = 1,
    TerminalThickness Padding = default);

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

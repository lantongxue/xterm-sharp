namespace XtermSharp.Rendering.Geometry;

public readonly record struct TerminalRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
}

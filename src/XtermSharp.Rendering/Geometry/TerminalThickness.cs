namespace XtermSharp.Rendering;

public readonly record struct TerminalThickness(double Left, double Top, double Right, double Bottom)
{
    public TerminalThickness(double uniform) : this(uniform, uniform, uniform, uniform)
    {
    }

    public double Horizontal => Left + Right;
    public double Vertical => Top + Bottom;
}

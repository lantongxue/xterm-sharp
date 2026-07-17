namespace XtermSharp.Rendering;

public interface ITerminalFontMetricsProvider
{
    TerminalFontMetrics MeasureFont(TerminalRenderConfiguration configuration);
}

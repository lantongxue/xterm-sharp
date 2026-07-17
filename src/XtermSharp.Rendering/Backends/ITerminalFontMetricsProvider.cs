namespace XtermSharp.Rendering.Backends;

public interface ITerminalFontMetricsProvider
{
    TerminalFontMetrics MeasureFont(TerminalRenderConfiguration configuration);
}

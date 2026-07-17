namespace XtermSharp.Addons.Search.Tests.Support;

internal sealed class FixedMetrics : ITerminalFontMetricsProvider
{
    public TerminalFontMetrics MeasureFont(TerminalRenderConfiguration configuration) =>
        new(10, 10, 8, 9, 1, 5);
}

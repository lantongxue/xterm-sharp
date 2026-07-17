namespace XtermSharp.Rendering.Backends;

[System.Diagnostics.CodeAnalysis.Experimental("XTSR0001")]
public interface ITerminalRenderBackend<in TSurface> : ITerminalFontMetricsProvider, IDisposable
{
    void Render(TSurface surface, TerminalRenderFrame frame);
}

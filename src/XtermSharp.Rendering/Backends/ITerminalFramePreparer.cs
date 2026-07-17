namespace XtermSharp.Rendering.Backends;

/// <summary>
/// Optionally prepares backend-specific retained resources before a frame reaches the UI render thread.
/// </summary>
public interface ITerminalFramePreparer
{
    void PrepareFrame(TerminalRenderFrame frame);
}

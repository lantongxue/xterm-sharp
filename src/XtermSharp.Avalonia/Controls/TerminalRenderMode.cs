namespace XtermSharp.Avalonia.Controls;

/// <summary>
/// Identifies the renderer that presented the most recent terminal frame.
/// </summary>
public enum TerminalRenderMode
{
    /// <summary>
    /// No terminal frame has been presented yet.
    /// </summary>
    Unknown,

    /// <summary>
    /// The frame was rendered by a software-backed Skia surface.
    /// </summary>
    Software,

    /// <summary>
    /// The frame was rendered by a GPU-backed Skia surface.
    /// </summary>
    Gpu
}

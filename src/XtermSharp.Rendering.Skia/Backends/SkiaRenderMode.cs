namespace XtermSharp.Rendering.Skia.Backends;

/// <summary>Describes how a Skia terminal frame is presented.</summary>
public enum SkiaRenderMode
{
    /// <summary>No frame has been presented by the current control session.</summary>
    Unknown,

    /// <summary>The frame is rasterized by the CPU.</summary>
    Software,

    /// <summary>The frame is replayed on a GPU-backed Skia surface.</summary>
    Gpu
}

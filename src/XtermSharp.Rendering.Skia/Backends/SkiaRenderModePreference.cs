namespace XtermSharp.Rendering.Skia.Backends;

/// <summary>Specifies the preferred presentation path for a Skia terminal view.</summary>
public enum SkiaRenderModePreference
{
    /// <summary>Use the platform GPU surface when available and fall back to software.</summary>
    Auto,

    /// <summary>Always rasterize terminal content through a CPU-backed Skia surface.</summary>
    Software,

    /// <summary>Request a GPU-backed Skia surface and fall back to software if unavailable.</summary>
    Gpu
}

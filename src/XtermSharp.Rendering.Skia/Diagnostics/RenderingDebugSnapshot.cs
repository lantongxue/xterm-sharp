namespace XtermSharp.Rendering.Skia.Diagnostics;

internal readonly record struct RenderingDebugSnapshot(
    int SampleCount,
    double FramesPerSecond,
    double AverageFrameTimeMilliseconds,
    double MaximumFrameTimeMilliseconds,
    double MinimumFrameTimeMilliseconds)
{
    public static RenderingDebugSnapshot Empty { get; } = new(0, 0, 0, 0, 0);
}

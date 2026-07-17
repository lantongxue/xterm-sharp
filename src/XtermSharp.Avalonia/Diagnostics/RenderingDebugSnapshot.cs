using System.Diagnostics;

namespace XtermSharp.Avalonia;

internal readonly record struct RenderingDebugSnapshot(
    int SampleCount,
    double FramesPerSecond,
    double AverageFrameTimeMilliseconds,
    double MaximumFrameTimeMilliseconds,
    double MinimumFrameTimeMilliseconds)
{
    public static RenderingDebugSnapshot Empty { get; } = new(0, 0, 0, 0, 0);
}


using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;
using XtermSharp.Rendering;
using XtermSharp.Rendering.Skia;

namespace XtermSharp.Avalonia;

internal sealed class SkiaDrawOperation(
    Rect bounds,
    SkiaTerminalRenderBackend backend,
    TerminalRenderFrame frame,
    RenderingDebugMetrics? debugMetrics) : ICustomDrawOperation
{
    private SkiaTerminalRenderBackend Backend { get; } = backend;
    private TerminalRenderFrame Frame { get; } = frame;
    private RenderingDebugMetrics? DebugMetrics { get; } = debugMetrics;

    public Rect Bounds { get; } = bounds;

    public bool HitTest(Point p) => Bounds.Contains(p);

    public void Render(ImmediateDrawingContext context)
    {
        ISkiaSharpApiLeaseFeature? feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (feature is null)
        {
            return;
        }
        using ISkiaSharpApiLease lease = feature.Lease();
        Backend.Render(lease.SkCanvas, Frame);
        if (DebugMetrics is not null)
        {
            RenderingDebugOverlay.Draw(
                lease.SkCanvas,
                new SKRect(
                    (float)Bounds.Left,
                    (float)Bounds.Top,
                    (float)Bounds.Right,
                    (float)Bounds.Bottom),
                DebugMetrics.RecordFrame());
        }
    }

    public bool Equals(ICustomDrawOperation? other) =>
        other is SkiaDrawOperation operation && Bounds == operation.Bounds &&
        ReferenceEquals(Backend, operation.Backend) && ReferenceEquals(Frame, operation.Frame) &&
        ReferenceEquals(DebugMetrics, operation.DebugMetrics);

    public void Dispose()
    {
    }
}

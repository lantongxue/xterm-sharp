using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using XtermSharp.Rendering;
using XtermSharp.Rendering.Skia;

namespace XtermSharp.Avalonia.Controls;

internal sealed class SkiaDrawOperation(
    Rect bounds,
    SkiaTerminalRenderBackend backend,
    TerminalRenderFrame frame,
    RenderingBackendState renderingBackendState,
    bool showRenderingDebugOverlay) : ICustomDrawOperation
{
    private SkiaTerminalRenderBackend Backend { get; } = backend;
    private TerminalRenderFrame Frame { get; } = frame;
    private RenderingBackendState RenderingBackendState { get; } = renderingBackendState;
    private bool ShowRenderingDebugOverlay { get; } = showRenderingDebugOverlay;

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
        TerminalRenderMode mode = lease.GrContext is null
            ? TerminalRenderMode.Software
            : TerminalRenderMode.Gpu;
        RenderingBackendState.Record(mode);
        Backend.ShowRenderingDebugOverlay = ShowRenderingDebugOverlay;
        Backend.Render(
            lease.SkCanvas,
            Frame,
            mode == TerminalRenderMode.Gpu ? SkiaRenderMode.Gpu : SkiaRenderMode.Software);
    }

    public bool Equals(ICustomDrawOperation? other) =>
        other is SkiaDrawOperation operation && Bounds == operation.Bounds &&
        ReferenceEquals(Backend, operation.Backend) && ReferenceEquals(Frame, operation.Frame) &&
        ReferenceEquals(RenderingBackendState, operation.RenderingBackendState) &&
        ShowRenderingDebugOverlay == operation.ShowRenderingDebugOverlay;

    public void Dispose()
    {
    }
}

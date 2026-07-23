using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;
using XtermSharp.Rendering;
using XtermSharp.Rendering.Skia;

namespace XtermSharp.Avalonia.Controls;

internal sealed class SkiaDrawOperation(
    Rect bounds,
    SkiaTerminalRenderBackend backend,
    TerminalRenderFrame frame,
    RenderingBackendState renderingBackendState,
    bool showRenderingDebugOverlay,
    SkiaRenderModePreference requestedRenderMode) : ICustomDrawOperation
{
    private SkiaTerminalRenderBackend Backend { get; } = backend;
    private TerminalRenderFrame Frame { get; } = frame;
    private RenderingBackendState RenderingBackendState { get; } = renderingBackendState;
    private bool ShowRenderingDebugOverlay { get; } = showRenderingDebugOverlay;
    private SkiaRenderModePreference RequestedRenderMode { get; } = requestedRenderMode;

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
        Backend.ShowRenderingDebugOverlay = ShowRenderingDebugOverlay;
        TerminalRenderMode mode;
        if (RequestedRenderMode == SkiaRenderModePreference.Software)
        {
            RenderSoftware(lease.SkCanvas);
            mode = TerminalRenderMode.Software;
        }
        else
        {
            mode = lease.GrContext is null
                ? TerminalRenderMode.Software
                : TerminalRenderMode.Gpu;
            Backend.Render(
                lease.SkCanvas,
                Frame,
                mode == TerminalRenderMode.Gpu ? SkiaRenderMode.Gpu : SkiaRenderMode.Software);
        }
        RenderingBackendState.Record(mode);
    }

    public bool Equals(ICustomDrawOperation? other) =>
        other is SkiaDrawOperation operation && Bounds == operation.Bounds &&
        ReferenceEquals(Backend, operation.Backend) && ReferenceEquals(Frame, operation.Frame) &&
        ReferenceEquals(RenderingBackendState, operation.RenderingBackendState) &&
        ShowRenderingDebugOverlay == operation.ShowRenderingDebugOverlay &&
        RequestedRenderMode == operation.RequestedRenderMode;

    private void RenderSoftware(SKCanvas target)
    {
        float scale = (float)Math.Max(0.01, Frame.Viewport.RenderScale);
        int width = Math.Max(1, (int)Math.Ceiling(Bounds.Width * scale));
        int height = Math.Max(1, (int)Math.Ceiling(Bounds.Height * scale));
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using SKSurface surface = SKSurface.Create(info) ??
            throw new InvalidOperationException("Skia could not create a software render surface.");
        surface.Canvas.Clear(SKColors.Transparent);
        surface.Canvas.Scale(scale);
        Backend.Render(surface.Canvas, Frame, SkiaRenderMode.Software);
        surface.Canvas.Flush();
        using SKImage image = surface.Snapshot();
        using var restore = new SKAutoCanvasRestore(target, true);
        target.ClipRect(new SKRect(
            (float)Bounds.Left,
            (float)Bounds.Top,
            (float)Bounds.Right,
            (float)Bounds.Bottom));
        target.Clear(SKColors.Transparent);
        target.DrawImage(
            image,
            new SKRect((float)Bounds.Left, (float)Bounds.Top, (float)Bounds.Right, (float)Bounds.Bottom));
    }

    public void Dispose()
    {
    }
}

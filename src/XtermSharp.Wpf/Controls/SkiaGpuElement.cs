using System.Windows;
using System.Windows.Media;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Wpf;
using SkiaSharp;

namespace XtermSharp.Wpf.Controls;

internal sealed class SkiaGpuElement : GLWpfControl
{
    private readonly SkiaOpenGlSurface _surface = new();

    public SkiaGpuElement()
    {
        Focusable = false;
        IsHitTestVisible = false;
        Render += OnRenderFrame;
        Unloaded += OnUnloaded;
        Start(new GLWpfControlSettings
        {
            RenderContinuously = false,
            UseDeviceDpi = true
        });
    }

    public bool IsGpuActive { get; private set; }

    public event Action<SKCanvas>? PaintSurface;

    public event Action<Exception>? RenderingFailed;

    protected override void OnRender(DrawingContext drawingContext)
    {
        try
        {
            base.OnRender(drawingContext);
        }
        catch (Exception exception)
        {
            IsGpuActive = false;
            RenderingFailed?.Invoke(exception);
        }
    }

    private void OnRenderFrame(TimeSpan delta)
    {
        _ = delta;
        try
        {
            int width = Math.Max(1, FrameBufferWidth);
            int height = Math.Max(1, FrameBufferHeight);
            GL.GetInteger((GetPName)0x0D57, out int stencilBits);
            GL.GetInteger(GetPName.Samples, out int samples);
            GL.Viewport(0, 0, width, height);
            _surface.Render(
                width,
                height,
                (uint)Math.Max(0, Framebuffer),
                stencilBits,
                samples,
                canvas => PaintSurface?.Invoke(canvas));
            IsGpuActive = true;
        }
        catch (Exception exception)
        {
            IsGpuActive = false;
            RenderingFailed?.Invoke(exception);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        _surface.Dispose();
        IsGpuActive = false;
    }

    public new void Dispose()
    {
        Render -= OnRenderFrame;
        Unloaded -= OnUnloaded;
        _surface.Dispose();
        base.Dispose();
    }
}

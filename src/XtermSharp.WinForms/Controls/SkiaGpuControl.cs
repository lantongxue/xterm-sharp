using OpenTK.GLControl;
using OpenTK.Graphics.OpenGL4;
using SkiaSharp;

namespace XtermSharp.WinForms.Controls;

internal sealed class SkiaGpuControl : GLControl
{
    private readonly SkiaOpenGlSurface _surface = new();

    public SkiaGpuControl()
    {
        Dock = DockStyle.Fill;
        TabStop = false;
        Enabled = false;
        SetStyle(ControlStyles.Opaque | ControlStyles.UserPaint, true);
    }

    public bool IsGpuActive { get; private set; }

    public event Action<SKCanvas>? PaintSurface;

    public event Action<Exception>? RenderingFailed;

    protected override void OnPaint(PaintEventArgs e)
    {
        _ = e;
        try
        {
            MakeCurrent();
            GL.GetInteger(GetPName.FramebufferBinding, out int framebuffer);
            GL.GetInteger((GetPName)0x0D57, out int stencilBits);
            GL.GetInteger(GetPName.Samples, out int samples);
            GL.Viewport(0, 0, Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height));
            _surface.Render(
                Math.Max(1, ClientSize.Width),
                Math.Max(1, ClientSize.Height),
                (uint)Math.Max(0, framebuffer),
                stencilBits,
                samples,
                canvas => PaintSurface?.Invoke(canvas));
            SwapBuffers();
            IsGpuActive = true;
        }
        catch (Exception exception)
        {
            IsGpuActive = false;
            RenderingFailed?.Invoke(exception);
        }
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        _surface.Dispose();
        IsGpuActive = false;
        base.OnHandleDestroyed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _surface.Dispose();
        }
        base.Dispose(disposing);
    }
}

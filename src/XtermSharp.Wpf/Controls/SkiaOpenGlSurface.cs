using SkiaSharp;

namespace XtermSharp.Wpf.Controls;

internal sealed class SkiaOpenGlSurface : IDisposable
{
    private const uint Rgba8 = 0x8058;
    private GRGlInterface? _glInterface;
    private GRContext? _context;
    private GRBackendRenderTarget? _renderTarget;
    private SKSurface? _surface;
    private SKSizeI _size;
    private uint _framebuffer;

    public void Render(
        int width,
        int height,
        uint framebuffer,
        int stencilBits,
        int sampleCount,
        Action<SKCanvas> render)
    {
        ArgumentNullException.ThrowIfNull(render);
        _glInterface ??= GRGlInterface.Create() ??
            throw new InvalidOperationException("Skia could not create an OpenGL interface.");
        _context ??= GRContext.CreateGl(_glInterface) ??
            throw new InvalidOperationException("Skia could not create a GPU context.");

        var size = new SKSizeI(width, height);
        if (_renderTarget is null || !_renderTarget.IsValid || size != _size || framebuffer != _framebuffer)
        {
            _surface?.Dispose();
            _surface = null;
            _renderTarget?.Dispose();
            _size = size;
            _framebuffer = framebuffer;
            int samples = Math.Min(sampleCount, _context.GetMaxSurfaceSampleCount(SKColorType.Rgba8888));
            var info = new GRGlFramebufferInfo(framebuffer, Rgba8);
            _renderTarget = new GRBackendRenderTarget(
                width,
                height,
                Math.Max(0, samples),
                Math.Max(0, stencilBits),
                info);
        }

        _surface ??= SKSurface.Create(
            _context,
            _renderTarget,
            GRSurfaceOrigin.BottomLeft,
            SKColorType.Rgba8888) ?? throw new InvalidOperationException("Skia could not create a GPU surface.");
        using var restore = new SKAutoCanvasRestore(_surface.Canvas, true);
        render(_surface.Canvas);
        _surface.Canvas.Flush();
    }

    public void Dispose()
    {
        _context?.AbandonContext(false);
        _surface?.Dispose();
        _renderTarget?.Dispose();
        _context?.Dispose();
        _glInterface?.Dispose();
        _surface = null;
        _renderTarget = null;
        _context = null;
        _glInterface = null;
    }
}

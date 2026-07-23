using System.Collections.Immutable;
using SkiaSharp;

namespace XtermSharp.Rendering.Skia.Tests;

public sealed class SkiaTerminalRenderBackendTests
{
    [Fact]
    public void FontListSkipsMissingFamiliesAndSelectsMonospaceTypeface()
    {
        const string families = "Definitely Missing Font, Menlo, DejaVu Sans Mono, Courier New, monospace";
        using var backend = new SkiaTerminalRenderBackend();

        string family = backend.ResolvePrimaryFontFamily(families);
        TerminalFontMetrics metrics = backend.MeasureFont(new TerminalRenderOptions
        {
            FontFamily = families,
            FontSize = 15
        }.Resolve(new TerminalOptions()));
        using SKTypeface typeface = SKFontManager.Default.MatchFamily(family);
        using var font = new SKFont(typeface, 15);
        using var paint = new SKPaint { IsAntialias = true };
        double expectedWidth = font.MeasureText("W", paint);

        Assert.False(string.IsNullOrWhiteSpace(family));
        Assert.True(backend.IsFixedPitch(family));
        Assert.Equal(expectedWidth, metrics.CellWidth, 3);
    }

    [Fact]
    public void RendersDisplayListToSkiaSurface()
    {
        using var backend = new SkiaTerminalRenderBackend();
        TerminalRenderConfiguration configuration = new TerminalRenderOptions().Resolve(new TerminalOptions());
        TerminalFontMetrics metrics = backend.MeasureFont(configuration);
        var row = new TerminalDisplayRow(0,
        [
            new TerminalFillRectangleCommand(new TerminalRect(0, 0, 40, 20), new TerminalRgbaColor(1, 2, 3)),
            new TerminalTextCommand(new TerminalRect(0, 0, 20, 20), "A", new TerminalRgbaColor(255, 255, 255), false, false, true)
        ]);
        var frame = new TerminalRenderFrame(
            1,
            new TerminalViewport(40, 20),
            metrics,
            2,
            1,
            0,
            0,
            new TerminalDisplayList(ImmutableArray.Create(row)),
            new TerminalDamage(0, 0));
        using var bitmap = new SKBitmap(40, 20);
        using var canvas = new SKCanvas(bitmap);

        backend.Render(canvas, frame);
        canvas.Flush();

        Assert.Equal(new SKColor(1, 2, 3), bitmap.GetPixel(39, 19));
        Assert.Contains(Enumerable.Range(0, bitmap.Width), x => bitmap.GetPixel(x, 10) != new SKColor(1, 2, 3));

        var debugMetrics = new RenderingDebugMetrics();
        RenderingDebugSnapshot snapshot = debugMetrics.RecordFrameTime(10);
        snapshot = debugMetrics.RecordFrameTime(20);
        snapshot = debugMetrics.RecordFrameTime(30);
        Assert.Equal(3, snapshot.SampleCount);
        Assert.Equal(50, snapshot.FramesPerSecond, 3);
        Assert.Equal(20, snapshot.AverageFrameTimeMilliseconds, 3);
        Assert.Equal(30, snapshot.MaximumFrameTimeMilliseconds, 3);
        Assert.Equal(10, snapshot.MinimumFrameTimeMilliseconds, 3);

        var debugFrame = frame with { Viewport = new TerminalViewport(300, 150) };
        using var debugBitmap = new SKBitmap(300, 150);
        using var debugCanvas = new SKCanvas(debugBitmap);
        debugCanvas.Clear(SKColors.Transparent);
        Assert.False(backend.ShowRenderingDebugOverlay);
        backend.ShowRenderingDebugOverlay = true;
        backend.Render(debugCanvas, debugFrame, SkiaRenderMode.Gpu);
        debugCanvas.Flush();

        Assert.True(debugBitmap.GetPixel(290, 10).Alpha > 0);
        Assert.Equal(0, debugBitmap.GetPixel(10, 140).Alpha);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            backend.Render(debugCanvas, debugFrame, (SkiaRenderMode)int.MaxValue));
    }

    [Fact]
    public void PreparesAndReusesRetainedRowPictures()
    {
        using var backend = new SkiaTerminalRenderBackend();
        TerminalRenderConfiguration configuration = new TerminalRenderOptions().Resolve(new TerminalOptions());
        TerminalFontMetrics metrics = backend.MeasureFont(configuration);
        var row = new TerminalDisplayRow(0,
        [
            new TerminalTextCommand(
                new TerminalRect(0, 0, metrics.CellWidth * 10, metrics.CellHeight),
                "0123456789",
                new TerminalRgbaColor(255, 255, 255),
                false,
                false,
                true)
            {
                CellCount = 10
            }
        ]);
        var frame = new TerminalRenderFrame(
            1,
            new TerminalViewport(metrics.CellWidth * 10, metrics.CellHeight),
            metrics,
            10,
            1,
            0,
            0,
            new TerminalDisplayList(ImmutableArray.Create(row)),
            new TerminalDamage(0, 0));

        backend.PrepareFrame(frame);
        backend.PrepareFrame(frame);

        Assert.Equal(1, backend.CachedRowPictureCount);
    }

}

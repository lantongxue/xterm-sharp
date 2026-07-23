using System.Globalization;
using SkiaSharp;

namespace XtermSharp.Rendering.Skia.Diagnostics;

internal static class RenderingDebugOverlay
{
    public static void Draw(
        SKCanvas canvas,
        SKRect bounds,
        RenderingDebugSnapshot snapshot,
        SkiaRenderMode renderMode)
    {
        string mode = renderMode switch
        {
            SkiaRenderMode.Gpu => "RENDER GPU",
            SkiaRenderMode.Software => "RENDER CPU",
            _ => "RENDER --"
        };
        string[] lines = snapshot.SampleCount == 0
            ? [mode, "FPS     --", "AVG     -- ms", "MAX     -- ms", "MIN     -- ms"]
            :
            [
                mode,
                string.Create(CultureInfo.InvariantCulture, $"FPS {snapshot.FramesPerSecond,6:F1}"),
                string.Create(CultureInfo.InvariantCulture, $"AVG {snapshot.AverageFrameTimeMilliseconds,6:F2} ms"),
                string.Create(CultureInfo.InvariantCulture, $"MAX {snapshot.MaximumFrameTimeMilliseconds,6:F2} ms"),
                string.Create(CultureInfo.InvariantCulture, $"MIN {snapshot.MinimumFrameTimeMilliseconds,6:F2} ms")
            ];
        using SKTypeface typeface = SKFontManager.Default.MatchFamily("monospace", SKFontStyle.Normal);
        using var font = new SKFont(typeface, 12)
        {
            Edging = SKFontEdging.Antialias,
            Hinting = SKFontHinting.Normal,
            Subpixel = true
        };
        using var textPaint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(235, 245, 255),
            Style = SKPaintStyle.Fill
        };
        font.GetFontMetrics(out SKFontMetrics metrics);
        float lineHeight = Math.Max(14, metrics.Descent - metrics.Ascent + metrics.Leading + 2);
        float textWidth = 0;
        foreach (string line in lines)
        {
            textWidth = Math.Max(textWidth, font.MeasureText(line, textPaint));
        }
        const float padding = 6;
        const float margin = 8;
        float width = textWidth + padding * 2;
        float height = lineHeight * lines.Length + padding * 2;
        float left = Math.Max(bounds.Left, bounds.Right - width - margin);
        float top = bounds.Top + margin;
        var rectangle = new SKRect(left, top, left + width, top + height);
        using var backgroundPaint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(8, 12, 18, 220),
            Style = SKPaintStyle.Fill
        };

        canvas.Save();
        canvas.ClipRect(bounds);
        canvas.DrawRoundRect(rectangle, 4, 4, backgroundPaint);
        float baseline = top + padding - metrics.Ascent;
        foreach (string line in lines)
        {
            canvas.DrawText(line, left + padding, baseline, font, textPaint);
            baseline += lineHeight;
        }
        canvas.Restore();
    }
}

using System.Runtime.CompilerServices;
using System.Text;
using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace XtermSharp.Rendering.Skia;

[System.Diagnostics.CodeAnalysis.Experimental("XTSR0001")]
public sealed class SkiaTerminalRenderBackend : ITerminalRenderBackend<SKCanvas>, ITerminalFramePreparer
{
    private readonly object _gate = new();
    private readonly Dictionary<TypefaceKey, SKTypeface> _typefaces = [];
    private readonly Dictionary<FontKey, SKFont> _fonts = [];
    private readonly Dictionary<SKColor, SKPaint> _fillPaints = [];
    private readonly Dictionary<string, string> _resolvedFamilies = new(StringComparer.Ordinal);
    private readonly Dictionary<TerminalDisplayRow, SKPicture> _rowPictures =
        new(ReferenceEqualityComparer<TerminalDisplayRow>.Instance);
    private TerminalRenderConfiguration? _lastConfiguration;
    private TerminalFontMetrics? _lastMetrics;
    private int _disposed;

    internal int CachedRowPictureCount
    {
        get
        {
            lock (_gate)
            {
                return _rowPictures.Count;
            }
        }
    }

    public TerminalFontMetrics MeasureFont(TerminalRenderConfiguration configuration)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(configuration);
        lock (_gate)
        {
            ThrowIfDisposed();
            if (Equals(_lastConfiguration, configuration) && _lastMetrics is TerminalFontMetrics cached)
            {
                return cached;
            }
            SKFont font = GetFont(configuration, bold: false, italic: false, 'W');
            using var paint = new SKPaint { IsAntialias = true };
            float width = font.MeasureText("W", paint);
            font.GetFontMetrics(out SKFontMetrics metrics);
            double rawHeight = Math.Max(1, metrics.Descent - metrics.Ascent + metrics.Leading);
            double cellHeight = Math.Max(1, rawHeight * configuration.LineHeight);
            double topPadding = Math.Max(0, (cellHeight - rawHeight) / 2);
            double baseline = topPadding - metrics.Ascent;
            double underlinePosition = baseline + Math.Max(1, metrics.UnderlinePosition ?? 1);
            double underlineThickness = Math.Max(1, metrics.UnderlineThickness ?? configuration.FontSize / 14);
            double strikePosition = baseline + (metrics.StrikeoutPosition ?? metrics.Ascent * 0.45f);
            _lastConfiguration = configuration;
            _lastMetrics = new TerminalFontMetrics(
                Math.Max(1, width + configuration.LetterSpacing),
                cellHeight,
                baseline,
                underlinePosition,
                underlineThickness,
                strikePosition);
            return _lastMetrics.Value;
        }
    }

    public void PrepareFrame(TerminalRenderFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        lock (_gate)
        {
            ThrowIfDisposed();
            EnsureConfigurationMeasured();
            PrepareFrameLocked(frame);
        }
    }

    public void Render(SKCanvas surface, TerminalRenderFrame frame)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(frame);
        lock (_gate)
        {
            ThrowIfDisposed();
            EnsureConfigurationMeasured();
            PrepareFrameLocked(frame);
            foreach (TerminalDisplayRow row in frame.DisplayList.Rows)
            {
                SKPicture picture = _rowPictures[row];
                surface.DrawPicture(picture);
            }
        }
    }

    public void ClearCaches()
    {
        lock (_gate)
        {
            ClearRowPictures();
            foreach (SKPaint paint in _fillPaints.Values)
            {
                paint.Dispose();
            }
            _fillPaints.Clear();
            foreach (SKFont font in _fonts.Values)
            {
                font.Dispose();
            }
            _fonts.Clear();
            foreach (SKTypeface typeface in _typefaces.Values)
            {
                typeface.Dispose();
            }
            _typefaces.Clear();
            _lastConfiguration = null;
            _lastMetrics = null;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }
        ClearCaches();
    }

    private void EnsureConfigurationMeasured()
    {
        if (_lastConfiguration is null)
        {
            throw new InvalidOperationException("MeasureFont must be called before rendering.");
        }
    }

    private void PrepareFrameLocked(TerminalRenderFrame frame)
    {
        foreach (TerminalDisplayRow row in frame.DisplayList.Rows)
        {
            if (!_rowPictures.ContainsKey(row))
            {
                _rowPictures.Add(row, RecordRow(row, frame));
            }
        }
        int maximumPictures = Math.Max(64, frame.Rows * 2);
        if (_rowPictures.Count <= maximumPictures)
        {
            return;
        }
        var currentRows = new HashSet<TerminalDisplayRow>(
            frame.DisplayList.Rows,
            ReferenceEqualityComparer<TerminalDisplayRow>.Instance);
        foreach (TerminalDisplayRow row in _rowPictures.Keys.ToArray())
        {
            if (currentRows.Contains(row))
            {
                continue;
            }
            _rowPictures.Remove(row, out SKPicture? picture);
            picture?.Dispose();
        }
    }

    private SKPicture RecordRow(TerminalDisplayRow row, TerminalRenderFrame frame)
    {
        using var recorder = new SKPictureRecorder();
        SKCanvas canvas = recorder.BeginRecording(new SKRect(
            0,
            (float)(frame.Viewport.Padding.Top + row.Row * frame.Metrics.CellHeight),
            (float)frame.Viewport.Width,
            (float)(frame.Viewport.Padding.Top + (row.Row + 1) * frame.Metrics.CellHeight)));
        foreach (TerminalDrawCommand command in row.Commands)
        {
            DrawCommand(canvas, command, frame);
        }
        return recorder.EndRecording();
    }

    private void DrawCommand(SKCanvas canvas, TerminalDrawCommand command, TerminalRenderFrame frame)
    {
        switch (command)
        {
            case TerminalFillRectangleCommand fill:
                canvas.DrawRect(ToRect(fill.Rectangle), GetFillPaint(fill.Color));
                break;
            case TerminalStrokeRectangleCommand stroke:
                using (var paint = Paint(stroke.Color, SKPaintStyle.Stroke, stroke.Thickness))
                {
                    canvas.DrawRect(ToRect(stroke.Rectangle), paint);
                }
                break;
            case TerminalTextCommand text:
                DrawText(canvas, text, frame);
                break;
            case TerminalLineCommand line:
                DrawLine(canvas, line);
                break;
        }
    }

    private void DrawText(SKCanvas canvas, TerminalTextCommand command, TerminalRenderFrame frame)
    {
        TerminalRenderConfiguration configuration = _lastConfiguration!;
        bool printableAscii = IsPrintableAscii(command.Text);
        int codePoint = printableAscii ? 'W' : FirstCodePoint(command.Text);
        SKFont font = GetFont(configuration, command.Bold, command.Italic, codePoint);
        SKPaint paint = GetFillPaint(command.Color);
        bool asciiCells = command.CellCount > 0 && command.Text.Length == command.CellCount &&
            printableAscii;
        if (asciiCells && command.CellCount > 1 &&
            (!font.Typeface.IsFixedPitch || configuration.LetterSpacing != 0))
        {
            DrawAsciiCells(canvas, command, frame, font, paint);
            return;
        }
        float available = (float)command.Rectangle.Width;
        bool fixedAscii = asciiCells && font.Typeface.IsFixedPitch && configuration.LetterSpacing == 0;
        float measured = fixedAscii ? available : font.MeasureText(command.Text, paint);
        bool scale = !fixedAscii && command.RescaleToFit && measured > available * 1.5f && measured > 0;
        canvas.Save();
        canvas.ClipRect(ToRect(command.Rectangle));
        float x = (float)command.Rectangle.X;
        float y = (float)(command.Rectangle.Y + frame.Metrics.Baseline);
        if (scale)
        {
            float factor = available / measured;
            canvas.Translate(x, y);
            canvas.Scale(factor, 1);
            canvas.DrawShapedText(command.Text, 0, 0, SKTextAlign.Left, font, paint);
        }
        else
        {
            if (asciiCells)
            {
                canvas.DrawText(command.Text, x, y, font, paint);
            }
            else
            {
                canvas.DrawShapedText(command.Text, x, y, SKTextAlign.Left, font, paint);
            }
        }
        canvas.Restore();
    }

    private static void DrawAsciiCells(
        SKCanvas canvas,
        TerminalTextCommand command,
        TerminalRenderFrame frame,
        SKFont font,
        SKPaint paint)
    {
        float cellWidth = (float)(command.Rectangle.Width / command.CellCount);
        float y = (float)(command.Rectangle.Y + frame.Metrics.Baseline);
        canvas.Save();
        canvas.ClipRect(ToRect(command.Rectangle));
        for (int index = 0; index < command.Text.Length; index++)
        {
            float x = (float)command.Rectangle.X + index * cellWidth;
            canvas.DrawText(command.Text[index].ToString(), x, y, font, paint);
        }
        canvas.Restore();
    }

    private static void DrawLine(SKCanvas canvas, TerminalLineCommand command)
    {
        using SKPaint paint = Paint(command.Color, SKPaintStyle.Stroke, command.Thickness);
        paint.StrokeCap = SKStrokeCap.Butt;
        if (command.Style == TerminalUnderlineStyle.Dotted)
        {
            paint.PathEffect = SKPathEffect.CreateDash([paint.StrokeWidth, paint.StrokeWidth * 2], 0);
        }
        else if (command.Style == TerminalUnderlineStyle.Dashed)
        {
            paint.PathEffect = SKPathEffect.CreateDash([paint.StrokeWidth * 4, paint.StrokeWidth * 3], 0);
        }
        if (command.Style == TerminalUnderlineStyle.Curly)
        {
            using var path = new SKPath();
            float startX = (float)command.Start.X;
            float endX = (float)command.End.X;
            float centerY = (float)command.Start.Y;
            float amplitude = Math.Max(1, paint.StrokeWidth);
            path.MoveTo(startX, centerY);
            for (float x = startX; x < endX; x += amplitude * 2)
            {
                path.QuadTo(x + amplitude / 2, centerY - amplitude, x + amplitude, centerY);
                path.QuadTo(x + amplitude * 1.5f, centerY + amplitude, x + amplitude * 2, centerY);
            }
            canvas.DrawPath(path, paint);
        }
        else
        {
            canvas.DrawLine(
                (float)command.Start.X,
                (float)command.Start.Y,
                (float)command.End.X,
                (float)command.End.Y,
                paint);
        }
    }

    private SKFont GetFont(
        TerminalRenderConfiguration configuration,
        bool bold,
        bool italic,
        int codePoint)
    {
        var typefaceKey = new TypefaceKey(
            ResolvePrimaryFontFamily(configuration.FontFamily),
            bold,
            italic,
            codePoint);
        var key = new FontKey(typefaceKey, (float)configuration.FontSize);
        if (_fonts.TryGetValue(key, out SKFont? cached))
        {
            return cached;
        }
        SKTypeface typeface = GetTypeface(typefaceKey);
        var font = new SKFont(typeface, key.Size)
        {
            Edging = SKFontEdging.SubpixelAntialias,
            Hinting = SKFontHinting.Normal,
            Subpixel = true
        };
        _fonts.Add(key, font);
        return font;
    }

    private SKTypeface GetTypeface(TypefaceKey key)
    {
        if (_typefaces.TryGetValue(key, out SKTypeface? cached))
        {
            return cached;
        }
        var style = new SKFontStyle(
            key.Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            key.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
        SKTypeface primary = SKFontManager.Default.MatchFamily(key.Family, style);
        SKTypeface typeface;
        if (primary.ContainsGlyph(key.CodePoint))
        {
            typeface = primary;
        }
        else
        {
            typeface = SKFontManager.Default.MatchCharacter(key.Family, style, null, key.CodePoint) ?? primary;
            if (!ReferenceEquals(typeface, primary))
            {
                primary.Dispose();
            }
        }
        _typefaces.Add(key, typeface);
        return typeface;
    }

    internal string ResolvePrimaryFontFamily(string families)
    {
        if (_resolvedFamilies.TryGetValue(families, out string? cached))
        {
            return cached;
        }
        var installed = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < SKFontManager.Default.FontFamilyCount; index++)
        {
            string name = SKFontManager.Default.GetFamilyName(index);
            installed.TryAdd(NormalizeFamilyName(name), name);
        }
        string[] candidates = families.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (string candidate in candidates)
        {
            string normalized = NormalizeFamilyName(candidate);
            if (normalized is "monospace" or "monospaced")
            {
                continue;
            }
            if (installed.TryGetValue(normalized, out string? match))
            {
                _resolvedFamilies.Add(families, match);
                return match;
            }
        }
        foreach (string name in installed.Values)
        {
            using SKTypeface typeface = SKFontManager.Default.MatchFamily(name);
            if (typeface.IsFixedPitch)
            {
                _resolvedFamilies.Add(families, name);
                return name;
            }
        }
        using SKTypeface fallback = SKTypeface.CreateDefault();
        string family = fallback.FamilyName;
        _resolvedFamilies.Add(families, family);
        return family;
    }

    internal bool IsFixedPitch(string families)
    {
        string family = ResolvePrimaryFontFamily(families);
        using SKTypeface typeface = SKFontManager.Default.MatchFamily(family);
        return typeface.IsFixedPitch;
    }

    private static string NormalizeFamilyName(string value)
    {
        ReadOnlySpan<char> span = value.AsSpan().Trim();
        if (span.Length >= 2 && (span[0] == '\'' && span[^1] == '\'' || span[0] == '"' && span[^1] == '"'))
        {
            span = span[1..^1];
        }
        Span<char> buffer = stackalloc char[span.Length];
        int length = 0;
        foreach (char character in span)
        {
            if (char.IsLetterOrDigit(character))
            {
                buffer[length++] = char.ToLowerInvariant(character);
            }
        }
        return new string(buffer[..length]);
    }

    private void ClearRowPictures()
    {
        foreach (SKPicture picture in _rowPictures.Values)
        {
            picture.Dispose();
        }
        _rowPictures.Clear();
    }

    private SKPaint GetFillPaint(TerminalRgbaColor color)
    {
        var skColor = new SKColor(color.Red, color.Green, color.Blue, color.Alpha);
        if (_fillPaints.TryGetValue(skColor, out SKPaint? cached))
        {
            return cached;
        }
        if (_fillPaints.Count >= 512)
        {
            foreach (SKPaint cachedPaint in _fillPaints.Values)
            {
                cachedPaint.Dispose();
            }
            _fillPaints.Clear();
        }
        var paint = new SKPaint
        {
            IsAntialias = true,
            Color = skColor,
            Style = SKPaintStyle.Fill
        };
        _fillPaints.Add(skColor, paint);
        return paint;
    }

    private static SKPaint Paint(
        TerminalRgbaColor color,
        SKPaintStyle style,
        double strokeWidth = 1) => new()
    {
        IsAntialias = true,
        Color = new SKColor(color.Red, color.Green, color.Blue, color.Alpha),
        Style = style,
        StrokeWidth = (float)Math.Max(1, strokeWidth)
    };

    private static SKRect ToRect(TerminalRect rectangle) => new(
        (float)rectangle.X,
        (float)rectangle.Y,
        (float)rectangle.Right,
        (float)rectangle.Bottom);

    private static int FirstCodePoint(string text)
    {
        foreach (Rune rune in text.EnumerateRunes())
        {
            return rune.Value;
        }
        return 'W';
    }

    private static bool IsPrintableAscii(string text)
    {
        foreach (char value in text)
        {
            if (value is < ' ' or > '~')
            {
                return false;
            }
        }
        return text.Length != 0;
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(SkiaTerminalRenderBackend));
        }
    }

    private readonly record struct TypefaceKey(string Family, bool Bold, bool Italic, int CodePoint);

    private readonly record struct FontKey(TypefaceKey Typeface, float Size);

    private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class
    {
        public static ReferenceEqualityComparer<T> Instance { get; } = new();

        public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

        public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
    }
}

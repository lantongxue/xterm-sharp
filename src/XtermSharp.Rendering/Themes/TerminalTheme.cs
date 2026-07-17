namespace XtermSharp.Rendering.Themes;

public sealed class TerminalTheme
{
    private readonly TerminalRgbaColor[] _palette;

    public TerminalTheme(
        TerminalRgbaColor foreground,
        TerminalRgbaColor background,
        TerminalRgbaColor cursor,
        TerminalRgbaColor cursorAccent,
        TerminalRgbaColor selectionBackground,
        TerminalRgbaColor selectionForeground,
        IReadOnlyList<TerminalRgbaColor> palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        if (palette.Count != 256)
        {
            throw new ArgumentException("A terminal palette must contain exactly 256 colors.", nameof(palette));
        }
        Foreground = foreground;
        Background = background;
        Cursor = cursor;
        CursorAccent = cursorAccent;
        SelectionBackground = selectionBackground;
        SelectionForeground = selectionForeground;
        _palette = palette.ToArray();
    }

    public TerminalRgbaColor Foreground { get; }
    public TerminalRgbaColor Background { get; }
    public TerminalRgbaColor Cursor { get; }
    public TerminalRgbaColor CursorAccent { get; }
    public TerminalRgbaColor SelectionBackground { get; }
    public TerminalRgbaColor SelectionForeground { get; }
    public IReadOnlyList<TerminalRgbaColor> Palette => _palette;

    public static TerminalTheme Default { get; } = CreateDefault();

    public TerminalRgbaColor Resolve(TerminalColor color, bool foreground) => color.Mode switch
    {
        TerminalColorMode.Default => foreground ? Foreground : Background,
        TerminalColorMode.Palette when (uint)color.Value < 256 => _palette[color.Value],
        TerminalColorMode.Rgb => TerminalRgbaColor.FromRgb(color.Value),
        _ => foreground ? Foreground : Background
    };

    public TerminalTheme WithColor(int index, TerminalRgbaColor color)
    {
        if (index is >= 0 and < 256)
        {
            TerminalRgbaColor[] palette = _palette.ToArray();
            palette[index] = color;
            return Copy(palette: palette);
        }
        return index switch
        {
            (int)TerminalSpecialColorIndex.Foreground => Copy(foreground: color),
            (int)TerminalSpecialColorIndex.Background => Copy(background: color),
            (int)TerminalSpecialColorIndex.Cursor => Copy(cursor: color),
            _ => this
        };
    }

    public TerminalTheme RestoreColor(int? index, TerminalTheme source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (index is null)
        {
            return Copy(palette: source._palette);
        }
        return WithColor(index.Value, source.GetIndexedColor(index.Value));
    }

    public TerminalRgbaColor GetIndexedColor(int index) => index switch
    {
        >= 0 and < 256 => _palette[index],
        (int)TerminalSpecialColorIndex.Foreground => Foreground,
        (int)TerminalSpecialColorIndex.Background => Background,
        (int)TerminalSpecialColorIndex.Cursor => Cursor,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    private TerminalTheme Copy(
        TerminalRgbaColor? foreground = null,
        TerminalRgbaColor? background = null,
        TerminalRgbaColor? cursor = null,
        TerminalRgbaColor[]? palette = null) => new(
            foreground ?? Foreground,
            background ?? Background,
            cursor ?? Cursor,
            CursorAccent,
            SelectionBackground,
            SelectionForeground,
            palette ?? _palette);

    private static TerminalTheme CreateDefault()
    {
        var palette = new TerminalRgbaColor[256];
        TerminalRgbaColor[] ansi =
        [
            new(0, 0, 0), new(205, 49, 49), new(13, 188, 121), new(229, 229, 16),
            new(36, 114, 200), new(188, 63, 188), new(17, 168, 205), new(229, 229, 229),
            new(102, 102, 102), new(241, 76, 76), new(35, 209, 139), new(245, 245, 67),
            new(59, 142, 234), new(214, 112, 214), new(41, 184, 219), new(255, 255, 255)
        ];
        ansi.CopyTo(palette, 0);
        int[] levels = [0, 95, 135, 175, 215, 255];
        int index = 16;
        foreach (int red in levels)
        {
            foreach (int green in levels)
            {
                foreach (int blue in levels)
                {
                    palette[index++] = new TerminalRgbaColor((byte)red, (byte)green, (byte)blue);
                }
            }
        }
        for (int gray = 0; gray < 24; gray++)
        {
            byte value = (byte)(8 + gray * 10);
            palette[232 + gray] = new TerminalRgbaColor(value, value, value);
        }
        return new TerminalTheme(
            new TerminalRgbaColor(229, 229, 229),
            new TerminalRgbaColor(0, 0, 0),
            new TerminalRgbaColor(229, 229, 229),
            new TerminalRgbaColor(0, 0, 0),
            new TerminalRgbaColor(255, 255, 255, 76),
            new TerminalRgbaColor(255, 255, 255),
            palette);
    }
}

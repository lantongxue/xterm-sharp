namespace XtermSharp.Options;

/// <summary>Configures a headless terminal instance.</summary>
public sealed class TerminalOptions
{
    public int Columns { get; init; } = 80;
    public int Rows { get; init; } = 24;
    public int Scrollback { get; init; } = 1000;
    public int TabStopWidth { get; init; } = 8;
    public double LineHeight { get; init; } = 1;
    public int CursorWidth { get; init; } = 1;
    public double FontSize { get; init; } = 15;
    public string FontFamily { get; init; } = "courier-new, courier, monospace";
    public bool AllowProposedApi { get; init; } = true;
    public bool ConvertEol { get; init; }
    public bool ScrollOnUserInput { get; init; } = true;
    public bool ScrollOnEraseInDisplay { get; init; }
    public bool ColorSchemeQuery { get; init; } = true;
    public bool MacOptionIsMeta { get; init; }
    public bool EnableKittyKeyboard { get; init; } = true;
    public bool EnableWin32InputMode { get; init; } = true;
    public TerminalWindowOptions WindowOptions { get; init; } = new();
    public long MaxPendingInputBytes { get; init; } = 50_000_000;
    public string UnicodeVersion { get; init; } = UnicodeV6Provider.VersionName;
    public ITerminalLogger? Logger { get; init; }
    internal bool ReflowCursorLine { get; init; }

    internal TerminalOptions ValidateAndClone()
    {
        if (Columns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Columns));
        }
        if (Rows <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Rows));
        }
        if (Scrollback < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Scrollback));
        }
        if (TabStopWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(TabStopWidth));
        }
        if (LineHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(LineHeight));
        }
        if (CursorWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(CursorWidth));
        }
        if (FontSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(FontSize));
        }
        if (string.IsNullOrWhiteSpace(FontFamily))
        {
            throw new ArgumentException("A font family is required.", nameof(FontFamily));
        }
        if (MaxPendingInputBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxPendingInputBytes));
        }
        if (string.IsNullOrWhiteSpace(UnicodeVersion))
        {
            throw new ArgumentException("A Unicode provider version is required.", nameof(UnicodeVersion));
        }

        return new TerminalOptions
        {
            Columns = Columns,
            Rows = Rows,
            Scrollback = Scrollback,
            TabStopWidth = TabStopWidth,
            LineHeight = LineHeight,
            CursorWidth = CursorWidth,
            FontSize = FontSize,
            FontFamily = FontFamily,
            AllowProposedApi = AllowProposedApi,
            ConvertEol = ConvertEol,
            ScrollOnUserInput = ScrollOnUserInput,
            ScrollOnEraseInDisplay = ScrollOnEraseInDisplay,
            ColorSchemeQuery = ColorSchemeQuery,
            MacOptionIsMeta = MacOptionIsMeta,
            EnableKittyKeyboard = EnableKittyKeyboard,
            EnableWin32InputMode = EnableWin32InputMode,
            WindowOptions = (WindowOptions ?? new TerminalWindowOptions()).Clone(),
            MaxPendingInputBytes = MaxPendingInputBytes,
            UnicodeVersion = UnicodeVersion,
            Logger = Logger,
            ReflowCursorLine = ReflowCursorLine
        };
    }

    internal TerminalOptions Apply(TerminalOptionsUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);
        return new TerminalOptions
        {
            Columns = Columns,
            Rows = Rows,
            Scrollback = update.Scrollback ?? Scrollback,
            TabStopWidth = update.TabStopWidth ?? TabStopWidth,
            LineHeight = update.LineHeight ?? LineHeight,
            CursorWidth = update.CursorWidth ?? CursorWidth,
            FontSize = update.FontSize ?? FontSize,
            FontFamily = update.FontFamily ?? FontFamily,
            AllowProposedApi = AllowProposedApi,
            ConvertEol = update.ConvertEol ?? ConvertEol,
            ScrollOnUserInput = update.ScrollOnUserInput ?? ScrollOnUserInput,
            ScrollOnEraseInDisplay = update.ScrollOnEraseInDisplay ?? ScrollOnEraseInDisplay,
            ColorSchemeQuery = update.ColorSchemeQuery ?? ColorSchemeQuery,
            MacOptionIsMeta = update.MacOptionIsMeta ?? MacOptionIsMeta,
            EnableKittyKeyboard = EnableKittyKeyboard,
            EnableWin32InputMode = EnableWin32InputMode,
            WindowOptions = (update.WindowOptions ?? WindowOptions).Clone(),
            MaxPendingInputBytes = MaxPendingInputBytes,
            UnicodeVersion = update.UnicodeVersion ?? UnicodeVersion,
            Logger = Logger,
            ReflowCursorLine = ReflowCursorLine
        }.ValidateAndClone();
    }
}

namespace XtermSharp.Options;

/// <summary>Runtime-changeable headless terminal options.</summary>
public sealed class TerminalOptionsUpdate
{
    public int? Scrollback { get; init; }
    public int? TabStopWidth { get; init; }
    public double? LineHeight { get; init; }
    public int? CursorWidth { get; init; }
    public double? FontSize { get; init; }
    public string? FontFamily { get; init; }
    public bool? ConvertEol { get; init; }
    public bool? ScrollOnUserInput { get; init; }
    public bool? ScrollOnEraseInDisplay { get; init; }
    public bool? ColorSchemeQuery { get; init; }
    public bool? MacOptionIsMeta { get; init; }
    public TerminalWindowOptions? WindowOptions { get; init; }
    public string? UnicodeVersion { get; init; }
}

namespace XtermSharp.Options;

/// <summary>
/// Security gates for host-window control and reporting sequences. Every operation is disabled by
/// default because several reports can reveal host information to applications running in the
/// terminal.
/// </summary>
public sealed class TerminalWindowOptions
{
    public bool RestoreWindow { get; init; }
    public bool MinimizeWindow { get; init; }
    public bool SetWindowPosition { get; init; }
    public bool SetWindowSizePixels { get; init; }
    public bool RaiseWindow { get; init; }
    public bool LowerWindow { get; init; }
    public bool RefreshWindow { get; init; }
    public bool SetWindowSizeCharacters { get; init; }
    public bool MaximizeWindow { get; init; }
    public bool FullscreenWindow { get; init; }
    public bool GetWindowState { get; init; }
    public bool GetWindowPosition { get; init; }
    public bool GetWindowSizePixels { get; init; }
    public bool GetScreenSizePixels { get; init; }
    public bool GetCellSizePixels { get; init; }
    public bool GetWindowSizeCharacters { get; init; }
    public bool GetScreenSizeCharacters { get; init; }
    public bool GetIconTitle { get; init; }
    public bool GetWindowTitle { get; init; }
    public bool PushTitle { get; init; }
    public bool PopTitle { get; init; }
    public bool SetWindowLines { get; init; }

    internal TerminalWindowOptions Clone() => new()
    {
        RestoreWindow = RestoreWindow,
        MinimizeWindow = MinimizeWindow,
        SetWindowPosition = SetWindowPosition,
        SetWindowSizePixels = SetWindowSizePixels,
        RaiseWindow = RaiseWindow,
        LowerWindow = LowerWindow,
        RefreshWindow = RefreshWindow,
        SetWindowSizeCharacters = SetWindowSizeCharacters,
        MaximizeWindow = MaximizeWindow,
        FullscreenWindow = FullscreenWindow,
        GetWindowState = GetWindowState,
        GetWindowPosition = GetWindowPosition,
        GetWindowSizePixels = GetWindowSizePixels,
        GetScreenSizePixels = GetScreenSizePixels,
        GetCellSizePixels = GetCellSizePixels,
        GetWindowSizeCharacters = GetWindowSizeCharacters,
        GetScreenSizeCharacters = GetScreenSizeCharacters,
        GetIconTitle = GetIconTitle,
        GetWindowTitle = GetWindowTitle,
        PushTitle = PushTitle,
        PopTitle = PopTitle,
        SetWindowLines = SetWindowLines
    };
}

namespace XtermSharp.Internal;

internal sealed class ModeState
{
    public bool ApplicationCursorKeys;
    public bool ApplicationKeypad;
    public bool BracketedPaste;
    public bool Insert;
    public bool Origin;
    public bool ReverseWraparound;
    public bool SendFocus;
    public bool ShowCursor = true;
    public bool SynchronizedOutput;
    public bool Wraparound = true;
    public TerminalMouseTrackingMode MouseTracking;
    public TerminalMouseEncodingMode MouseEncoding;
    public TerminalKittyKeyboardFlags KittyKeyboardFlags;
    public TerminalKittyKeyboardFlags KittyMainFlags;
    public TerminalKittyKeyboardFlags KittyAlternateFlags;
    public Stack<TerminalKittyKeyboardFlags> KittyMainStack { get; } = new();
    public Stack<TerminalKittyKeyboardFlags> KittyAlternateStack { get; } = new();
    public bool Win32InputMode;
    public TerminalCursorStyle? CursorStyle;
    public bool? CursorBlink;
    public bool ColorSchemeUpdates;

    public TerminalModes Snapshot() => new(
        ApplicationCursorKeys,
        ApplicationKeypad,
        BracketedPaste,
        Insert,
        Origin,
        ReverseWraparound,
        SendFocus,
        ShowCursor,
        SynchronizedOutput,
        Wraparound,
        MouseTracking,
        MouseEncoding,
        KittyKeyboardFlags,
        Win32InputMode,
        CursorStyle,
        CursorBlink,
        ColorSchemeUpdates);
}

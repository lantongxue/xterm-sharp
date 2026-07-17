namespace XtermSharp;

public sealed record TerminalModes(
    bool ApplicationCursorKeys,
    bool ApplicationKeypad,
    bool BracketedPaste,
    bool Insert,
    bool Origin,
    bool ReverseWraparound,
    bool SendFocus,
    bool ShowCursor,
    bool SynchronizedOutput,
    bool Wraparound,
    TerminalMouseTrackingMode MouseTracking,
    TerminalMouseEncodingMode MouseEncoding,
    TerminalKittyKeyboardFlags KittyKeyboardFlags,
    bool Win32InputMode,
    TerminalCursorStyle? CursorStyle,
    bool? CursorBlink,
    bool ColorSchemeUpdates);

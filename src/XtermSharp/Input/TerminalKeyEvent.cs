namespace XtermSharp.Input;

/// <summary>A platform-neutral keyboard event suitable for terminal protocol encoding.</summary>
public readonly record struct TerminalKeyEvent(
    string Key,
    string Code,
    int KeyCode = 0,
    TerminalModifiers Modifiers = TerminalModifiers.None,
    TerminalKeyEventType EventType = TerminalKeyEventType.Press,
    string? Text = null,
    bool CapsLock = false,
    bool NumLock = false);

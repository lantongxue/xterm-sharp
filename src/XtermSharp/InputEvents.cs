namespace XtermSharp;

[Flags]
public enum TerminalModifiers : byte
{
    None = 0,
    Shift = 1 << 0,
    Alt = 1 << 1,
    Control = 1 << 2,
    Meta = 1 << 3
}

public enum TerminalKeyEventType : byte
{
    Press,
    Repeat,
    Release
}

[Flags]
public enum TerminalKittyKeyboardFlags : byte
{
    None = 0,
    DisambiguateEscapeCodes = 1 << 0,
    ReportEventTypes = 1 << 1,
    ReportAlternateKeys = 1 << 2,
    ReportAllKeysAsEscapeCodes = 1 << 3,
    ReportAssociatedText = 1 << 4
}

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

public enum TerminalMouseButton : byte
{
    Left = 0,
    Middle = 1,
    Right = 2,
    None = 3,
    Wheel = 4,
    Auxiliary1 = 8,
    Auxiliary2 = 9,
    Auxiliary3 = 10,
    Auxiliary4 = 11,
    Auxiliary5 = 12,
    Auxiliary6 = 13,
    Auxiliary7 = 14,
    Auxiliary8 = 15
}

public enum TerminalMouseAction : byte
{
    Up = 0,
    Down = 1,
    Left = 2,
    Right = 3,
    Move = 32
}

/// <summary>
/// A terminal mouse event. Cell and pixel positions use the one-based coordinates emitted by
/// terminal mouse protocols.
/// </summary>
public readonly record struct TerminalMouseEvent(
    int Column,
    int Row,
    int PixelX,
    int PixelY,
    TerminalMouseButton Button,
    TerminalMouseAction Action,
    TerminalModifiers Modifiers = TerminalModifiers.None);

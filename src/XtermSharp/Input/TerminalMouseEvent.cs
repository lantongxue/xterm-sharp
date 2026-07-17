namespace XtermSharp;

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

namespace XtermSharp.Links;

/// <summary>Platform-neutral pointer data passed to terminal link callbacks.</summary>
public readonly record struct TerminalLinkEvent(
    int Column,
    int BufferLine,
    int PixelX,
    int PixelY,
    TerminalMouseButton Button,
    TerminalMouseAction Action,
    TerminalModifiers Modifiers = TerminalModifiers.None);

namespace XtermSharp.Rendering.Controllers;

internal sealed record CursorOverlayCache(
    TerminalDisplayRow ContentRow,
    int Row,
    int Column,
    bool Focused,
    bool CursorPhase,
    bool ShowCursor,
    bool? CursorBlink,
    TerminalCursorStyle? CursorStyle,
    string PreeditText,
    TerminalDisplayRow DisplayRow);

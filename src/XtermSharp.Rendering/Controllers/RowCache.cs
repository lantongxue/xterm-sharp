namespace XtermSharp.Rendering.Controllers;

internal sealed record RowCache(
    TerminalLineSnapshot Line,
    int ConfigurationVersion,
    int BlinkVersion,
    TerminalDisplayRow Row,
    bool HasBlinkingText);

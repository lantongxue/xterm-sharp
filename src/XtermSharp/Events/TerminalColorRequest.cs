namespace XtermSharp.Events;

/// <summary>A renderer-independent request to report, set or restore a terminal color.</summary>
public sealed record TerminalColorRequest(
    TerminalColorRequestType Type,
    int? Index = null,
    TerminalColor? Color = null);

namespace XtermSharp.Internal;

internal sealed record MouseProtocol(
    TerminalMouseEventTypes Events,
    Func<TerminalMouseEvent, (bool Allowed, TerminalMouseEvent Event)> Restrict);

namespace XtermSharp.Internal.Services.Mouse;

internal sealed record MouseProtocol(
    TerminalMouseEventTypes Events,
    Func<TerminalMouseEvent, (bool Allowed, TerminalMouseEvent Event)> Restrict);

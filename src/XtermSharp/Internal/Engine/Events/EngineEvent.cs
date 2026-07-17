using System.Collections.Immutable;
using System.Text;
using XtermSharp.Internal.Input;
using XtermSharp.Internal.Parser;

namespace XtermSharp.Internal.Engine.Events;

internal readonly record struct EngineEvent(
    EngineEventKind Kind,
    string? Text = null,
    int First = 0,
    int Second = 0,
    IReadOnlyList<TerminalColorRequest>? ColorRequests = null,
    TerminalOptions? PreviousOptions = null,
    TerminalOptions? CurrentOptions = null);

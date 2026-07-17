namespace XtermSharp.Internal.Input.Results;

internal readonly record struct KeyboardResult(
    KeyboardResultType Type,
    string? Key = null,
    bool Cancel = false);

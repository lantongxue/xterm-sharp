namespace XtermSharp.Internal.Input;

internal readonly record struct KeyboardResult(
    KeyboardResultType Type,
    string? Key = null,
    bool Cancel = false);

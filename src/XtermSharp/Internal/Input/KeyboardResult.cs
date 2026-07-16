namespace XtermSharp.Internal.Input;

internal enum KeyboardResultType : byte
{
    SendKey,
    PageUp,
    PageDown,
    SelectAll
}

internal readonly record struct KeyboardResult(
    KeyboardResultType Type,
    string? Key = null,
    bool Cancel = false);

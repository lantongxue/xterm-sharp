using XtermSharp.TestSupport;

namespace XtermSharp.Tests.Input.Support;

internal static class KeyEvent
{
    internal static TerminalKeyEvent Create(
        string key = "",
        string code = "",
        int keyCode = 0,
        TerminalModifiers modifiers = TerminalModifiers.None,
        TerminalKeyEventType eventType = TerminalKeyEventType.Press) =>
        new(key, code, keyCode, modifiers, eventType);
}

namespace XtermSharp.Internal.Input;

internal sealed class Win32InputMode
{
    private static readonly IReadOnlyDictionary<string, int> CodeToVirtualKey =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["KeyA"] = 0x41, ["KeyB"] = 0x42, ["KeyC"] = 0x43, ["KeyD"] = 0x44,
            ["KeyE"] = 0x45, ["KeyF"] = 0x46, ["KeyG"] = 0x47, ["KeyH"] = 0x48,
            ["KeyI"] = 0x49, ["KeyJ"] = 0x4A, ["KeyK"] = 0x4B, ["KeyL"] = 0x4C,
            ["KeyM"] = 0x4D, ["KeyN"] = 0x4E, ["KeyO"] = 0x4F, ["KeyP"] = 0x50,
            ["KeyQ"] = 0x51, ["KeyR"] = 0x52, ["KeyS"] = 0x53, ["KeyT"] = 0x54,
            ["KeyU"] = 0x55, ["KeyV"] = 0x56, ["KeyW"] = 0x57, ["KeyX"] = 0x58,
            ["KeyY"] = 0x59, ["KeyZ"] = 0x5A,
            ["Digit0"] = 0x30, ["Digit1"] = 0x31, ["Digit2"] = 0x32, ["Digit3"] = 0x33,
            ["Digit4"] = 0x34, ["Digit5"] = 0x35, ["Digit6"] = 0x36, ["Digit7"] = 0x37,
            ["Digit8"] = 0x38, ["Digit9"] = 0x39,
            ["F1"] = 0x70, ["F2"] = 0x71, ["F3"] = 0x72, ["F4"] = 0x73,
            ["F5"] = 0x74, ["F6"] = 0x75, ["F7"] = 0x76, ["F8"] = 0x77,
            ["F9"] = 0x78, ["F10"] = 0x79, ["F11"] = 0x7A, ["F12"] = 0x7B,
            ["F13"] = 0x7C, ["F14"] = 0x7D, ["F15"] = 0x7E, ["F16"] = 0x7F,
            ["F17"] = 0x80, ["F18"] = 0x81, ["F19"] = 0x82, ["F20"] = 0x83,
            ["F21"] = 0x84, ["F22"] = 0x85, ["F23"] = 0x86, ["F24"] = 0x87,
            ["Numpad0"] = 0x60, ["Numpad1"] = 0x61, ["Numpad2"] = 0x62,
            ["Numpad3"] = 0x63, ["Numpad4"] = 0x64, ["Numpad5"] = 0x65,
            ["Numpad6"] = 0x66, ["Numpad7"] = 0x67, ["Numpad8"] = 0x68,
            ["Numpad9"] = 0x69, ["NumpadMultiply"] = 0x6A, ["NumpadAdd"] = 0x6B,
            ["NumpadSeparator"] = 0x6C, ["NumpadSubtract"] = 0x6D,
            ["NumpadDecimal"] = 0x6E, ["NumpadDivide"] = 0x6F, ["NumpadEnter"] = 0x0D,
            ["NumLock"] = 0x90,
            ["ArrowUp"] = 0x26, ["ArrowDown"] = 0x28, ["ArrowLeft"] = 0x25,
            ["ArrowRight"] = 0x27, ["Home"] = 0x24, ["End"] = 0x23,
            ["PageUp"] = 0x21, ["PageDown"] = 0x22, ["Insert"] = 0x2D, ["Delete"] = 0x2E,
            ["ShiftLeft"] = 0x10, ["ShiftRight"] = 0x10, ["ControlLeft"] = 0x11,
            ["ControlRight"] = 0x11, ["AltLeft"] = 0x12, ["AltRight"] = 0x12,
            ["MetaLeft"] = 0x5B, ["MetaRight"] = 0x5C, ["CapsLock"] = 0x14,
            ["ScrollLock"] = 0x91,
            ["Escape"] = 0x1B, ["Enter"] = 0x0D, ["Tab"] = 0x09, ["Space"] = 0x20,
            ["Backspace"] = 0x08, ["Pause"] = 0x13, ["ContextMenu"] = 0x5D,
            ["PrintScreen"] = 0x2C,
            ["Semicolon"] = 0xBA, ["Equal"] = 0xBB, ["Comma"] = 0xBC,
            ["Minus"] = 0xBD, ["Period"] = 0xBE, ["Slash"] = 0xBF,
            ["Backquote"] = 0xC0, ["BracketLeft"] = 0xDB, ["Backslash"] = 0xDC,
            ["BracketRight"] = 0xDD, ["Quote"] = 0xDE, ["IntlBackslash"] = 0xE2
        };

    private static readonly IReadOnlyDictionary<string, int> CodeToScanCode =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["KeyQ"] = 0x10, ["KeyW"] = 0x11, ["KeyE"] = 0x12, ["KeyR"] = 0x13,
            ["KeyT"] = 0x14, ["KeyY"] = 0x15, ["KeyU"] = 0x16, ["KeyI"] = 0x17,
            ["KeyO"] = 0x18, ["KeyP"] = 0x19, ["KeyA"] = 0x1E, ["KeyS"] = 0x1F,
            ["KeyD"] = 0x20, ["KeyF"] = 0x21, ["KeyG"] = 0x22, ["KeyH"] = 0x23,
            ["KeyJ"] = 0x24, ["KeyK"] = 0x25, ["KeyL"] = 0x26, ["KeyZ"] = 0x2C,
            ["KeyX"] = 0x2D, ["KeyC"] = 0x2E, ["KeyV"] = 0x2F, ["KeyB"] = 0x30,
            ["KeyN"] = 0x31, ["KeyM"] = 0x32,
            ["Digit1"] = 0x02, ["Digit2"] = 0x03, ["Digit3"] = 0x04,
            ["Digit4"] = 0x05, ["Digit5"] = 0x06, ["Digit6"] = 0x07,
            ["Digit7"] = 0x08, ["Digit8"] = 0x09, ["Digit9"] = 0x0A, ["Digit0"] = 0x0B,
            ["F1"] = 0x3B, ["F2"] = 0x3C, ["F3"] = 0x3D, ["F4"] = 0x3E,
            ["F5"] = 0x3F, ["F6"] = 0x40, ["F7"] = 0x41, ["F8"] = 0x42,
            ["F9"] = 0x43, ["F10"] = 0x44, ["F11"] = 0x57, ["F12"] = 0x58,
            ["Numpad0"] = 0x52, ["Numpad1"] = 0x4F, ["Numpad2"] = 0x50,
            ["Numpad3"] = 0x51, ["Numpad4"] = 0x4B, ["Numpad5"] = 0x4C,
            ["Numpad6"] = 0x4D, ["Numpad7"] = 0x47, ["Numpad8"] = 0x48,
            ["Numpad9"] = 0x49, ["NumpadMultiply"] = 0x37, ["NumpadAdd"] = 0x4E,
            ["NumpadSubtract"] = 0x4A, ["NumpadDecimal"] = 0x53,
            ["NumpadDivide"] = 0x35, ["NumpadEnter"] = 0x1C, ["NumLock"] = 0x45,
            ["ArrowUp"] = 0x48, ["ArrowDown"] = 0x50, ["ArrowLeft"] = 0x4B,
            ["ArrowRight"] = 0x4D, ["Home"] = 0x47, ["End"] = 0x4F,
            ["PageUp"] = 0x49, ["PageDown"] = 0x51, ["Insert"] = 0x52, ["Delete"] = 0x53,
            ["ShiftLeft"] = 0x2A, ["ShiftRight"] = 0x36, ["ControlLeft"] = 0x1D,
            ["ControlRight"] = 0x1D, ["AltLeft"] = 0x38, ["AltRight"] = 0x38,
            ["CapsLock"] = 0x3A, ["ScrollLock"] = 0x46,
            ["Escape"] = 0x01, ["Enter"] = 0x1C, ["Tab"] = 0x0F, ["Space"] = 0x39,
            ["Backspace"] = 0x0E, ["Pause"] = 0x45,
            ["Semicolon"] = 0x27, ["Equal"] = 0x0D, ["Comma"] = 0x33,
            ["Minus"] = 0x0C, ["Period"] = 0x34, ["Slash"] = 0x35,
            ["Backquote"] = 0x29, ["BracketLeft"] = 0x1A, ["Backslash"] = 0x2B,
            ["BracketRight"] = 0x1B, ["Quote"] = 0x28
        };

    private static readonly HashSet<string> EnhancedKeyCodes = new(StringComparer.Ordinal)
    {
        "ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", "Home", "End", "PageUp", "PageDown",
        "Insert", "Delete", "NumpadEnter", "NumpadDivide", "ControlRight", "AltRight",
        "PrintScreen", "Pause", "ContextMenu", "MetaLeft", "MetaRight"
    };

    private static readonly IReadOnlyDictionary<string, int> KeyToControlCharacter =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Enter"] = 0x0D, ["Backspace"] = 0x08, ["Tab"] = 0x09, ["Escape"] = 0x1B
        };

    internal KeyboardResult Evaluate(TerminalKeyEvent keyEvent) =>
        Evaluate(keyEvent, keyEvent.EventType != TerminalKeyEventType.Release);

    internal KeyboardResult Evaluate(TerminalKeyEvent keyEvent, bool isKeyDown)
    {
        int virtualKey = CodeToVirtualKey.TryGetValue(keyEvent.Code, out int mappedVirtualKey)
            ? mappedVirtualKey
            : keyEvent.KeyCode;
        int scanCode = CodeToScanCode.TryGetValue(keyEvent.Code, out int mappedScanCode) ? mappedScanCode : 0;
        int unicodeCharacter = GetUnicodeCharacter(keyEvent);
        int keyDown = isKeyDown ? 1 : 0;
        int controlState = (int)GetControlKeyState(keyEvent);
        string sequence = $"\x1b[{virtualKey};{scanCode};{unicodeCharacter};{keyDown};{controlState};1_";
        return new KeyboardResult(KeyboardResultType.SendKey, sequence, true);
    }

    private static int GetUnicodeCharacter(TerminalKeyEvent keyEvent)
    {
        bool control = keyEvent.Modifiers.HasFlag(TerminalModifiers.Control);
        bool alt = keyEvent.Modifiers.HasFlag(TerminalModifiers.Alt);
        bool meta = keyEvent.Modifiers.HasFlag(TerminalModifiers.Meta);
        if (control && !alt && !meta)
        {
            if (keyEvent.Key == "Enter")
            {
                return 0x0A;
            }
            if (keyEvent.Key == "Backspace")
            {
                return 0x7F;
            }
        }

        if (KeyToControlCharacter.TryGetValue(keyEvent.Key, out int controlCharacter))
        {
            return controlCharacter;
        }

        if (keyEvent.Key.Length != 1)
        {
            return 0;
        }

        int codePoint = keyEvent.Key[0];
        if (control && !alt && !meta)
        {
            if (codePoint is >= 0x41 and <= 0x5A)
            {
                return codePoint - 0x40;
            }
            if (codePoint is >= 0x61 and <= 0x7A)
            {
                return codePoint - 0x60;
            }
        }
        return codePoint;
    }

    private static Win32ControlKeyState GetControlKeyState(TerminalKeyEvent keyEvent)
    {
        Win32ControlKeyState state = Win32ControlKeyState.None;
        if (keyEvent.Modifiers.HasFlag(TerminalModifiers.Shift))
        {
            state |= Win32ControlKeyState.ShiftPressed;
        }
        if (keyEvent.Modifiers.HasFlag(TerminalModifiers.Control))
        {
            state |= keyEvent.Code == "ControlRight"
                ? Win32ControlKeyState.RightControlPressed
                : Win32ControlKeyState.LeftControlPressed;
        }
        if (keyEvent.Modifiers.HasFlag(TerminalModifiers.Alt))
        {
            state |= keyEvent.Code == "AltRight"
                ? Win32ControlKeyState.RightAltPressed
                : Win32ControlKeyState.LeftAltPressed;
        }
        if (EnhancedKeyCodes.Contains(keyEvent.Code))
        {
            state |= Win32ControlKeyState.EnhancedKey;
        }
        return state;
    }
}

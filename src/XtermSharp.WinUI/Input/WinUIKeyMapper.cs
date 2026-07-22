using System.Runtime.InteropServices;
using System.Text;
using Windows.System;

namespace XtermSharp.WinUI.Input;

internal static class WinUIKeyMapper
{
    internal const string DeadKeyMarker = "\uffff";
    private const uint MapVirtualKeyToScanCode = 4;
    private const uint DoNotChangeKeyboardState = 4;

    public static TerminalKeyEvent Create(
        VirtualKey key,
        string? symbol,
        TerminalModifiers modifiers,
        TerminalKeyEventType eventType)
    {
        bool deadKey = symbol == DeadKeyMarker;
        return new TerminalKeyEvent(
            deadKey ? "Dead" : KeyName(key, symbol),
            CodeName(key),
            (int)key,
            modifiers,
            eventType,
            deadKey ? null : symbol,
            IsToggled(0x14),
            IsToggled(0x90));
    }

    public static TerminalModifiers GetModifiers()
    {
        TerminalModifiers result = TerminalModifiers.None;
        if (IsDown(0x10)) result |= TerminalModifiers.Shift;
        if (IsDown(0x12)) result |= TerminalModifiers.Alt;
        if (IsDown(0x11)) result |= TerminalModifiers.Control;
        if (IsDown(0x5B) || IsDown(0x5C)) result |= TerminalModifiers.Meta;
        return result;
    }

    public static bool ShouldUseTextInput(TerminalKeyEvent key, bool enhancedKeyboardMode)
    {
        bool alt = key.Modifiers.HasFlag(TerminalModifiers.Alt);
        bool control = key.Modifiers.HasFlag(TerminalModifiers.Control);
        bool thirdLevelShift = alt && control;
        if (key.Key is "Dead" or "Process")
        {
            return true;
        }
        if (thirdLevelShift && (key.KeyCode == 0 || key.KeyCode > 47))
        {
            return true;
        }
        if (enhancedKeyboardMode || string.IsNullOrEmpty(key.Text) || control || alt)
        {
            return false;
        }
        return key.Key is not (
            "Backspace" or "Delete" or "Enter" or "Escape" or "Tab" or
            "Insert" or "Home" or "End" or "PageUp" or "PageDown" or
            "ArrowLeft" or "ArrowRight" or "ArrowUp" or "ArrowDown");
    }

    public static bool ShouldCopy(VirtualKey key, TerminalModifiers modifiers, bool hasSelection) =>
        hasSelection &&
        ((int)key == 0x43 && HasShortcut(modifiers, TerminalModifiers.Control) ||
         (int)key == 0x2D && HasShortcut(modifiers, TerminalModifiers.Control));

    public static bool ShouldPaste(VirtualKey key, TerminalModifiers modifiers) =>
        (int)key == 0x56 && HasShortcut(modifiers, TerminalModifiers.Control) ||
        (int)key == 0x2D && HasShortcut(modifiers, TerminalModifiers.Shift, allowShift: false);

    public static bool ShouldSelectAll(VirtualKey key, TerminalModifiers modifiers) =>
        (int)key == 0x41 && HasShortcut(modifiers, TerminalModifiers.Control);

    public static string? GetText(VirtualKey key, TerminalModifiers modifiers)
    {
        int virtualKey = (int)key;
        if (virtualKey <= 0 || virtualKey >= 256)
        {
            return null;
        }
        byte[] keyboardState = new byte[256];
        if (!GetKeyboardState(keyboardState))
        {
            return null;
        }
        bool altGraph = modifiers.HasFlag(TerminalModifiers.Control) &&
            modifiers.HasFlag(TerminalModifiers.Alt);
        if (!altGraph)
        {
            keyboardState[0x11] = 0;
            keyboardState[0xA2] = 0;
            keyboardState[0xA3] = 0;
            keyboardState[0x12] = 0;
            keyboardState[0xA4] = 0;
            keyboardState[0xA5] = 0;
        }

        nint layout = GetKeyboardLayout(0);
        uint scanCode = MapVirtualKeyEx((uint)virtualKey, MapVirtualKeyToScanCode, layout);
        var buffer = new StringBuilder(8);
        int length = ToUnicodeEx(
            (uint)virtualKey,
            scanCode,
            keyboardState,
            buffer,
            buffer.Capacity,
            DoNotChangeKeyboardState,
            layout);
        return length switch
        {
            > 0 => buffer.ToString(0, length),
            < 0 => DeadKeyMarker,
            _ => null
        };
    }

    internal static string KeyName(VirtualKey key, string? symbol)
    {
        int value = (int)key;
        return value switch
        {
            0x25 => "ArrowLeft",
            0x27 => "ArrowRight",
            0x26 => "ArrowUp",
            0x28 => "ArrowDown",
            0x08 => "Backspace",
            0x0D => "Enter",
            0x1B => "Escape",
            0x09 => "Tab",
            0x2E => "Delete",
            0x2D => "Insert",
            0x24 => "Home",
            0x23 => "End",
            0x21 => "PageUp",
            0x22 => "PageDown",
            0x10 or 0xA0 or 0xA1 => "Shift",
            0x11 or 0xA2 or 0xA3 => "Control",
            0x12 or 0xA4 or 0xA5 => "Alt",
            0x5B or 0x5C => "Meta",
            0x5D => "ContextMenu",
            0x14 => "CapsLock",
            0x91 => "ScrollLock",
            0x2C => "PrintScreen",
            0x20 when string.IsNullOrEmpty(symbol) => " ",
            >= 0x60 and <= 0x69 when string.IsNullOrEmpty(symbol) =>
                ((char)('0' + value - 0x60)).ToString(),
            0x6A when string.IsNullOrEmpty(symbol) => "*",
            0x6B when string.IsNullOrEmpty(symbol) => "+",
            0x6D when string.IsNullOrEmpty(symbol) => "-",
            0x6E when string.IsNullOrEmpty(symbol) => ".",
            0x6F when string.IsNullOrEmpty(symbol) => "/",
            _ when !string.IsNullOrEmpty(symbol) => symbol,
            0 => "Unidentified",
            _ => key.ToString()
        };
    }

    internal static string CodeName(VirtualKey key)
    {
        int value = (int)key;
        if (value is >= 0x41 and <= 0x5A)
        {
            return $"Key{(char)value}";
        }
        if (value is >= 0x30 and <= 0x39)
        {
            return $"Digit{(char)value}";
        }
        if (value is >= 0x60 and <= 0x69)
        {
            return $"Numpad{(char)('0' + value - 0x60)}";
        }
        return value switch
        {
            0x25 => "ArrowLeft",
            0x27 => "ArrowRight",
            0x26 => "ArrowUp",
            0x28 => "ArrowDown",
            0x08 => "Backspace",
            0x0D => "Enter",
            0x1B => "Escape",
            0x09 => "Tab",
            0x2E => "Delete",
            0x2D => "Insert",
            0x24 => "Home",
            0x23 => "End",
            0x21 => "PageUp",
            0x22 => "PageDown",
            0xA0 => "ShiftLeft",
            0xA1 => "ShiftRight",
            0xA2 => "ControlLeft",
            0xA3 => "ControlRight",
            0xA4 => "AltLeft",
            0xA5 => "AltRight",
            0x5B => "MetaLeft",
            0x5C => "MetaRight",
            0x5D => "ContextMenu",
            0x14 => "CapsLock",
            0x90 => "NumLock",
            0x91 => "ScrollLock",
            0x2C => "PrintScreen",
            0xBA => "Semicolon",
            0xBB => "Equal",
            0xBC => "Comma",
            0xBD => "Minus",
            0xBE => "Period",
            0xBF => "Slash",
            0xC0 => "Backquote",
            0xDB => "BracketLeft",
            0xDC => "Backslash",
            0xDD => "BracketRight",
            0xDE => "Quote",
            0x6A => "NumpadMultiply",
            0x6B => "NumpadAdd",
            0x6D => "NumpadSubtract",
            0x6E => "NumpadDecimal",
            0x6F => "NumpadDivide",
            0 => "Unidentified",
            _ => key.ToString()
        };
    }

    private static bool HasShortcut(
        TerminalModifiers modifiers,
        TerminalModifiers primary,
        bool allowShift = true)
    {
        TerminalModifiers allowed = primary | (allowShift ? TerminalModifiers.Shift : TerminalModifiers.None);
        return modifiers.HasFlag(primary) && (modifiers & ~allowed) == TerminalModifiers.None;
    }

    private static bool IsDown(int virtualKey) => (GetKeyState(virtualKey) & 0x8000) != 0;

    private static bool IsToggled(int virtualKey) => (GetKeyState(virtualKey) & 1) != 0;

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int virtualKey);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetKeyboardState([Out] byte[] keyboardState);

    [DllImport("user32.dll")]
    private static extern nint GetKeyboardLayout(uint threadId);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKeyEx(uint code, uint mapType, nint keyboardLayout);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ToUnicodeEx(
        uint virtualKey,
        uint scanCode,
        byte[] keyboardState,
        StringBuilder buffer,
        int bufferSize,
        uint flags,
        nint keyboardLayout);
}

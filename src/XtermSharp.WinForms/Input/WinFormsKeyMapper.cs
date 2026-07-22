using System.Runtime.InteropServices;
using System.Text;

namespace XtermSharp.WinForms.Input;

internal static class WinFormsKeyMapper
{
    internal const string DeadKeyMarker = "\uffff";
    private const uint MapVirtualKeyToScanCode = 4;
    private const uint DoNotChangeKeyboardState = 4;

    public static TerminalKeyEvent Create(
        Keys keyCode,
        string? symbol,
        Keys modifiers,
        TerminalKeyEventType eventType)
    {
        string code = CodeName(keyCode);
        bool deadKey = symbol == DeadKeyMarker;
        return new TerminalKeyEvent(
            deadKey ? "Dead" : KeyName(keyCode, symbol),
            code,
            KeyCode(keyCode, code),
            MapModifiers(modifiers),
            eventType,
            deadKey ? null : symbol,
            Control.IsKeyLocked(Keys.CapsLock),
            Control.IsKeyLocked(Keys.NumLock));
    }

    public static TerminalModifiers MapModifiers(Keys modifiers)
    {
        TerminalModifiers result = TerminalModifiers.None;
        if ((modifiers & Keys.Shift) != 0) result |= TerminalModifiers.Shift;
        if ((modifiers & Keys.Alt) != 0) result |= TerminalModifiers.Alt;
        if ((modifiers & Keys.Control) != 0) result |= TerminalModifiers.Control;
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

    public static bool ShouldCopy(Keys keyCode, Keys modifiers, bool hasSelection) =>
        hasSelection &&
        (keyCode == Keys.C && HasModifierShortcut(modifiers, Keys.Control) ||
         keyCode == Keys.Insert && HasModifierShortcut(modifiers, Keys.Control));

    public static bool ShouldPaste(Keys keyCode, Keys modifiers) =>
        keyCode == Keys.V && HasModifierShortcut(modifiers, Keys.Control) ||
        keyCode == Keys.Insert && HasModifierShortcut(modifiers, Keys.Shift, allowShift: false);

    public static string? GetText(Keys keyCode, Keys modifiers)
    {
        if (keyCode is < Keys.Space or > Keys.OemClear)
        {
            return null;
        }

        byte[] keyboardState = new byte[256];
        if (!GetKeyboardState(keyboardState))
        {
            return null;
        }
        bool altGraph = (modifiers & Keys.Control) != 0 && (modifiers & Keys.Alt) != 0;
        if (!altGraph)
        {
            keyboardState[(int)Keys.ControlKey] = 0;
            keyboardState[(int)Keys.LControlKey] = 0;
            keyboardState[(int)Keys.RControlKey] = 0;
            keyboardState[(int)Keys.Menu] = 0;
            keyboardState[(int)Keys.LMenu] = 0;
            keyboardState[(int)Keys.RMenu] = 0;
        }

        nint layout = GetKeyboardLayout(0);
        uint virtualKey = (uint)keyCode;
        uint scanCode = MapVirtualKeyEx(virtualKey, MapVirtualKeyToScanCode, layout);
        var buffer = new StringBuilder(8);
        int length = ToUnicodeEx(
            virtualKey,
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

    internal static string KeyName(Keys keyCode, string? symbol) => keyCode switch
    {
        Keys.Left => "ArrowLeft",
        Keys.Right => "ArrowRight",
        Keys.Up => "ArrowUp",
        Keys.Down => "ArrowDown",
        Keys.Back => "Backspace",
        Keys.Return => "Enter",
        Keys.Escape => "Escape",
        Keys.Tab => "Tab",
        Keys.Delete => "Delete",
        Keys.Insert => "Insert",
        Keys.Home => "Home",
        Keys.End => "End",
        Keys.PageUp => "PageUp",
        Keys.PageDown => "PageDown",
        Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey => "Shift",
        Keys.ControlKey or Keys.LControlKey or Keys.RControlKey => "Control",
        Keys.Menu or Keys.LMenu or Keys.RMenu => "Alt",
        Keys.LWin or Keys.RWin => "Meta",
        Keys.Apps => "ContextMenu",
        Keys.CapsLock => "CapsLock",
        Keys.Scroll => "ScrollLock",
        Keys.PrintScreen => "PrintScreen",
        Keys.VolumeMute => "AudioVolumeMute",
        Keys.VolumeDown => "AudioVolumeDown",
        Keys.VolumeUp => "AudioVolumeUp",
        Keys.MediaNextTrack => "MediaTrackNext",
        Keys.MediaPreviousTrack => "MediaTrackPrevious",
        Keys.MediaStop => "MediaStop",
        Keys.MediaPlayPause => "MediaPlayPause",
        Keys.ProcessKey => "Process",
        Keys.Space when string.IsNullOrEmpty(symbol) => " ",
        >= Keys.NumPad0 and <= Keys.NumPad9 when string.IsNullOrEmpty(symbol) =>
            ((char)('0' + keyCode - Keys.NumPad0)).ToString(),
        Keys.Multiply when string.IsNullOrEmpty(symbol) => "*",
        Keys.Add when string.IsNullOrEmpty(symbol) => "+",
        Keys.Separator when string.IsNullOrEmpty(symbol) => ",",
        Keys.Subtract when string.IsNullOrEmpty(symbol) => "-",
        Keys.Decimal when string.IsNullOrEmpty(symbol) => ".",
        Keys.Divide when string.IsNullOrEmpty(symbol) => "/",
        _ when !string.IsNullOrEmpty(symbol) => symbol,
        Keys.None => "Unidentified",
        _ => keyCode.ToString()
    };

    internal static string CodeName(Keys keyCode)
    {
        if (keyCode is >= Keys.A and <= Keys.Z)
        {
            return $"Key{(char)('A' + keyCode - Keys.A)}";
        }
        if (keyCode is >= Keys.D0 and <= Keys.D9)
        {
            return $"Digit{(char)('0' + keyCode - Keys.D0)}";
        }
        if (keyCode is >= Keys.NumPad0 and <= Keys.NumPad9)
        {
            return $"Numpad{(char)('0' + keyCode - Keys.NumPad0)}";
        }
        return keyCode switch
        {
            Keys.Left => "ArrowLeft",
            Keys.Right => "ArrowRight",
            Keys.Up => "ArrowUp",
            Keys.Down => "ArrowDown",
            Keys.Back => "Backspace",
            Keys.Return => "Enter",
            Keys.Escape => "Escape",
            Keys.Tab => "Tab",
            Keys.Delete => "Delete",
            Keys.Insert => "Insert",
            Keys.Home => "Home",
            Keys.End => "End",
            Keys.PageUp => "PageUp",
            Keys.PageDown => "PageDown",
            Keys.LShiftKey => "ShiftLeft",
            Keys.RShiftKey => "ShiftRight",
            Keys.ShiftKey => "ShiftLeft",
            Keys.LControlKey => "ControlLeft",
            Keys.RControlKey => "ControlRight",
            Keys.ControlKey => "ControlLeft",
            Keys.LMenu => "AltLeft",
            Keys.RMenu => "AltRight",
            Keys.Menu => "AltLeft",
            Keys.LWin => "MetaLeft",
            Keys.RWin => "MetaRight",
            Keys.Apps => "ContextMenu",
            Keys.CapsLock => "CapsLock",
            Keys.NumLock => "NumLock",
            Keys.Scroll => "ScrollLock",
            Keys.PrintScreen => "PrintScreen",
            Keys.OemSemicolon => "Semicolon",
            Keys.Oemplus => "Equal",
            Keys.Oemcomma => "Comma",
            Keys.OemMinus => "Minus",
            Keys.OemPeriod => "Period",
            Keys.OemQuestion => "Slash",
            Keys.Oemtilde => "Backquote",
            Keys.OemOpenBrackets => "BracketLeft",
            Keys.OemPipe => "Backslash",
            Keys.OemCloseBrackets => "BracketRight",
            Keys.OemQuotes => "Quote",
            Keys.Multiply => "NumpadMultiply",
            Keys.Add => "NumpadAdd",
            Keys.Separator => "NumpadSeparator",
            Keys.Subtract => "NumpadSubtract",
            Keys.Decimal => "NumpadDecimal",
            Keys.Divide => "NumpadDivide",
            _ => keyCode.ToString()
        };
    }

    internal static int KeyCode(Keys keyCode, string code)
    {
        if (keyCode is >= Keys.A and <= Keys.Z)
        {
            return 65 + keyCode - Keys.A;
        }
        if (keyCode is >= Keys.D0 and <= Keys.D9)
        {
            return 48 + keyCode - Keys.D0;
        }
        if (keyCode is >= Keys.NumPad0 and <= Keys.NumPad9)
        {
            return 96 + keyCode - Keys.NumPad0;
        }
        if (keyCode is >= Keys.F1 and <= Keys.F24)
        {
            return 112 + keyCode - Keys.F1;
        }
        return code switch
        {
            "NumpadMultiply" => 106,
            "NumpadAdd" => 107,
            "NumpadSeparator" => 108,
            "NumpadSubtract" => 109,
            "NumpadDecimal" => 110,
            "NumpadDivide" => 111,
            _ => (int)keyCode
        };
    }

    private static bool HasModifierShortcut(Keys modifiers, Keys primary, bool allowShift = true)
    {
        Keys allowed = primary | (allowShift ? Keys.Shift : Keys.None);
        return (modifiers & primary) != 0 && (modifiers & ~allowed) == 0;
    }

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

using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Input;

namespace XtermSharp.Wpf.Input;

internal static class WpfKeyMapper
{
    internal const string DeadKeyMarker = "\uffff";
    private const uint MapVirtualKeyToScanCode = 4;
    private const uint DoNotChangeKeyboardState = 4;

    public static TerminalKeyEvent Create(
        Key key,
        string? symbol,
        ModifierKeys modifiers,
        TerminalKeyEventType eventType)
    {
        string code = CodeName(key);
        bool deadKey = symbol == DeadKeyMarker || key == Key.DeadCharProcessed;
        return new TerminalKeyEvent(
            deadKey ? "Dead" : KeyName(key, symbol),
            code,
            KeyCode(key),
            MapModifiers(modifiers),
            eventType,
            deadKey ? null : symbol,
            Keyboard.IsKeyToggled(Key.CapsLock),
            Keyboard.IsKeyToggled(Key.NumLock));
    }

    public static Key EffectiveKey(KeyEventArgs args) => args.Key switch
    {
        Key.System => args.SystemKey,
        Key.ImeProcessed => Key.ImeProcessed,
        Key.DeadCharProcessed => Key.DeadCharProcessed,
        _ => args.Key
    };

    public static TerminalModifiers MapModifiers(ModifierKeys modifiers)
    {
        TerminalModifiers result = TerminalModifiers.None;
        if (modifiers.HasFlag(ModifierKeys.Shift)) result |= TerminalModifiers.Shift;
        if (modifiers.HasFlag(ModifierKeys.Alt)) result |= TerminalModifiers.Alt;
        if (modifiers.HasFlag(ModifierKeys.Control)) result |= TerminalModifiers.Control;
        if (modifiers.HasFlag(ModifierKeys.Windows)) result |= TerminalModifiers.Meta;
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

    public static bool ShouldCopy(Key key, ModifierKeys modifiers, bool hasSelection) =>
        hasSelection &&
        (key == Key.C && HasModifierShortcut(modifiers, ModifierKeys.Control) ||
         key == Key.Insert && HasModifierShortcut(modifiers, ModifierKeys.Control));

    public static bool ShouldPaste(Key key, ModifierKeys modifiers) =>
        key == Key.V && HasModifierShortcut(modifiers, ModifierKeys.Control) ||
        key == Key.Insert && HasModifierShortcut(modifiers, ModifierKeys.Shift, allowShift: false);

    public static bool ShouldSelectAll(Key key, ModifierKeys modifiers) =>
        key == Key.A && HasModifierShortcut(modifiers, ModifierKeys.Control);

    public static string? GetText(Key key, ModifierKeys modifiers)
    {
        int virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey <= 0 || virtualKey >= 256)
        {
            return null;
        }
        byte[] keyboardState = new byte[256];
        if (!GetKeyboardState(keyboardState))
        {
            return null;
        }
        bool altGraph = modifiers.HasFlag(ModifierKeys.Control) && modifiers.HasFlag(ModifierKeys.Alt);
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

    internal static string KeyName(Key key, string? symbol) => key switch
    {
        Key.Left => "ArrowLeft",
        Key.Right => "ArrowRight",
        Key.Up => "ArrowUp",
        Key.Down => "ArrowDown",
        Key.Back => "Backspace",
        Key.Return => "Enter",
        Key.Escape => "Escape",
        Key.Tab => "Tab",
        Key.Delete => "Delete",
        Key.Insert => "Insert",
        Key.Home => "Home",
        Key.End => "End",
        Key.PageUp => "PageUp",
        Key.PageDown => "PageDown",
        Key.LeftShift or Key.RightShift => "Shift",
        Key.LeftCtrl or Key.RightCtrl => "Control",
        Key.LeftAlt or Key.RightAlt => "Alt",
        Key.LWin or Key.RWin => "Meta",
        Key.Apps => "ContextMenu",
        Key.CapsLock => "CapsLock",
        Key.Scroll => "ScrollLock",
        Key.PrintScreen => "PrintScreen",
        Key.VolumeMute => "AudioVolumeMute",
        Key.VolumeDown => "AudioVolumeDown",
        Key.VolumeUp => "AudioVolumeUp",
        Key.MediaNextTrack => "MediaTrackNext",
        Key.MediaPreviousTrack => "MediaTrackPrevious",
        Key.MediaStop => "MediaStop",
        Key.MediaPlayPause => "MediaPlayPause",
        Key.ImeProcessed => "Process",
        Key.DeadCharProcessed => "Dead",
        Key.Space when string.IsNullOrEmpty(symbol) => " ",
        >= Key.NumPad0 and <= Key.NumPad9 when string.IsNullOrEmpty(symbol) =>
            ((char)('0' + key - Key.NumPad0)).ToString(),
        Key.Multiply when string.IsNullOrEmpty(symbol) => "*",
        Key.Add when string.IsNullOrEmpty(symbol) => "+",
        Key.Subtract when string.IsNullOrEmpty(symbol) => "-",
        Key.Decimal when string.IsNullOrEmpty(symbol) => ".",
        Key.Divide when string.IsNullOrEmpty(symbol) => "/",
        _ when !string.IsNullOrEmpty(symbol) => symbol,
        Key.None => "Unidentified",
        _ => key.ToString()
    };

    internal static string CodeName(Key key)
    {
        if (key is >= Key.A and <= Key.Z)
        {
            return $"Key{(char)('A' + key - Key.A)}";
        }
        if (key is >= Key.D0 and <= Key.D9)
        {
            return $"Digit{(char)('0' + key - Key.D0)}";
        }
        if (key is >= Key.NumPad0 and <= Key.NumPad9)
        {
            return $"Numpad{(char)('0' + key - Key.NumPad0)}";
        }
        return key switch
        {
            Key.Left => "ArrowLeft",
            Key.Right => "ArrowRight",
            Key.Up => "ArrowUp",
            Key.Down => "ArrowDown",
            Key.Back => "Backspace",
            Key.Return => "Enter",
            Key.Escape => "Escape",
            Key.Tab => "Tab",
            Key.Delete => "Delete",
            Key.Insert => "Insert",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.LeftShift => "ShiftLeft",
            Key.RightShift => "ShiftRight",
            Key.LeftCtrl => "ControlLeft",
            Key.RightCtrl => "ControlRight",
            Key.LeftAlt => "AltLeft",
            Key.RightAlt => "AltRight",
            Key.LWin => "MetaLeft",
            Key.RWin => "MetaRight",
            Key.Apps => "ContextMenu",
            Key.CapsLock => "CapsLock",
            Key.NumLock => "NumLock",
            Key.Scroll => "ScrollLock",
            Key.PrintScreen => "PrintScreen",
            Key.Oem1 => "Semicolon",
            Key.OemPlus => "Equal",
            Key.OemComma => "Comma",
            Key.OemMinus => "Minus",
            Key.OemPeriod => "Period",
            Key.Oem2 => "Slash",
            Key.Oem3 => "Backquote",
            Key.Oem4 => "BracketLeft",
            Key.Oem5 => "Backslash",
            Key.Oem6 => "BracketRight",
            Key.Oem7 => "Quote",
            Key.Multiply => "NumpadMultiply",
            Key.Add => "NumpadAdd",
            Key.Subtract => "NumpadSubtract",
            Key.Decimal => "NumpadDecimal",
            Key.Divide => "NumpadDivide",
            Key.ImeProcessed or Key.DeadCharProcessed => "Unidentified",
            _ => key.ToString()
        };
    }

    internal static int KeyCode(Key key) => KeyInterop.VirtualKeyFromKey(key);

    private static bool HasModifierShortcut(
        ModifierKeys modifiers,
        ModifierKeys primary,
        bool allowShift = true)
    {
        ModifierKeys allowed = primary | (allowShift ? ModifierKeys.Shift : ModifierKeys.None);
        return modifiers.HasFlag(primary) && (modifiers & ~allowed) == ModifierKeys.None;
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

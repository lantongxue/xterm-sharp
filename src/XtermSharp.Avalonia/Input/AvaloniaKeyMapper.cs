using Avalonia.Input;

namespace XtermSharp.Avalonia.Input;

internal static class AvaloniaKeyMapper
{
    public static TerminalKeyEvent Create(
        Key key,
        PhysicalKey physicalKey,
        string? symbol,
        KeyModifiers modifiers,
        TerminalKeyEventType eventType)
    {
        string code = CodeName(physicalKey);
        string keyName = KeyName(key, symbol);
        return new TerminalKeyEvent(
            keyName,
            code,
            KeyCode(key, code),
            MapModifiers(modifiers),
            eventType,
            symbol);
    }

    public static TerminalModifiers MapModifiers(KeyModifiers modifiers)
    {
        TerminalModifiers result = TerminalModifiers.None;
        if (modifiers.HasFlag(KeyModifiers.Shift)) result |= TerminalModifiers.Shift;
        if (modifiers.HasFlag(KeyModifiers.Alt)) result |= TerminalModifiers.Alt;
        if (modifiers.HasFlag(KeyModifiers.Control)) result |= TerminalModifiers.Control;
        if (modifiers.HasFlag(KeyModifiers.Meta)) result |= TerminalModifiers.Meta;
        return result;
    }

    public static bool ShouldUseTextInput(
        TerminalKeyEvent key,
        bool enhancedKeyboardMode,
        bool isMac,
        bool isWindows,
        bool macOptionIsMeta)
    {
        bool alt = key.Modifiers.HasFlag(TerminalModifiers.Alt);
        bool control = key.Modifiers.HasFlag(TerminalModifiers.Control);
        bool meta = key.Modifiers.HasFlag(TerminalModifiers.Meta);
        bool macOptionAsMeta = isMac && macOptionIsMeta && alt;
        if (!macOptionAsMeta && key.Key is "Dead" or "AltGraph")
        {
            return true;
        }

        bool thirdLevelShift =
            isMac && !macOptionIsMeta && alt && !control && !meta ||
            isWindows && alt && control && !meta;
        if (thirdLevelShift && (key.KeyCode == 0 || key.KeyCode > 47))
        {
            return true;
        }

        if (enhancedKeyboardMode || string.IsNullOrEmpty(key.Text) || control || alt || meta)
        {
            return false;
        }

        return key.Key is not (
            "Backspace" or "Delete" or "Enter" or "Escape" or "Tab" or
            "Insert" or "Home" or "End" or "PageUp" or "PageDown" or
            "ArrowLeft" or "ArrowRight" or "ArrowUp" or "ArrowDown");
    }

    public static bool ShouldCopy(Key key, KeyModifiers modifiers, bool isMac, bool hasSelection) =>
        hasSelection &&
        (key == Key.C && HasPrimaryShortcutModifier(modifiers, isMac) ||
         key == Key.Insert && HasModifierShortcut(modifiers, KeyModifiers.Control));

    public static bool ShouldPaste(Key key, KeyModifiers modifiers, bool isMac) =>
        key == Key.V && HasPrimaryShortcutModifier(modifiers, isMac) ||
        key == Key.Insert && HasModifierShortcut(modifiers, KeyModifiers.Shift, allowShift: false);

    public static bool ShouldSelectAll(Key key, KeyModifiers modifiers, bool isMac) =>
        isMac && key == Key.A && modifiers == KeyModifiers.Meta;

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
        Key.Snapshot => "PrintScreen",
        Key.VolumeMute => "AudioVolumeMute",
        Key.VolumeDown => "AudioVolumeDown",
        Key.VolumeUp => "AudioVolumeUp",
        Key.MediaNextTrack => "MediaTrackNext",
        Key.MediaPreviousTrack => "MediaTrackPrevious",
        Key.SelectMedia => "MediaSelect",
        Key.DeadCharProcessed => "Dead",
        Key.Space when string.IsNullOrEmpty(symbol) => " ",
        Key.NumPad0 when string.IsNullOrEmpty(symbol) => "0",
        Key.NumPad1 when string.IsNullOrEmpty(symbol) => "1",
        Key.NumPad2 when string.IsNullOrEmpty(symbol) => "2",
        Key.NumPad3 when string.IsNullOrEmpty(symbol) => "3",
        Key.NumPad4 when string.IsNullOrEmpty(symbol) => "4",
        Key.NumPad5 when string.IsNullOrEmpty(symbol) => "5",
        Key.NumPad6 when string.IsNullOrEmpty(symbol) => "6",
        Key.NumPad7 when string.IsNullOrEmpty(symbol) => "7",
        Key.NumPad8 when string.IsNullOrEmpty(symbol) => "8",
        Key.NumPad9 when string.IsNullOrEmpty(symbol) => "9",
        Key.Multiply when string.IsNullOrEmpty(symbol) => "*",
        Key.Add when string.IsNullOrEmpty(symbol) => "+",
        Key.Separator when string.IsNullOrEmpty(symbol) => ",",
        Key.Subtract when string.IsNullOrEmpty(symbol) => "-",
        Key.Decimal when string.IsNullOrEmpty(symbol) => ".",
        Key.Divide when string.IsNullOrEmpty(symbol) => "/",
        _ when !string.IsNullOrEmpty(symbol) => symbol,
        Key.None => "Unidentified",
        _ => key.ToString()
    };

    internal static int KeyCode(Key key, string code)
    {
        string virtualKey = key.ToString();
        if (virtualKey.Length == 1 && char.IsAsciiLetter(virtualKey[0]))
        {
            return char.ToUpperInvariant(virtualKey[0]);
        }
        if (virtualKey.Length == 2 && virtualKey[0] == 'D' && char.IsAsciiDigit(virtualKey[1]))
        {
            return virtualKey[1];
        }
        if (code.Length == 4 && code.StartsWith("Key", StringComparison.Ordinal) && char.IsAsciiLetter(code[3]))
        {
            return char.ToUpperInvariant(code[3]);
        }
        if (code.Length == 6 && code.StartsWith("Digit", StringComparison.Ordinal) && char.IsAsciiDigit(code[5]))
        {
            return code[5];
        }
        if (code.StartsWith('F') && int.TryParse(code.AsSpan(1), out int function) && function is >= 1 and <= 24)
        {
            return 111 + function;
        }
        if (code.StartsWith("Numpad", StringComparison.Ordinal))
        {
            ReadOnlySpan<char> suffix = code.AsSpan(6);
            if (suffix.Length == 1 && suffix[0] is >= '0' and <= '9')
            {
                return 96 + suffix[0] - '0';
            }
            return suffix switch
            {
                "Multiply" => 106,
                "Add" => 107,
                "Separator" or "Comma" => 108,
                "Subtract" => 109,
                "Decimal" => 110,
                "Divide" => 111,
                "Enter" => 13,
                "Equal" => 187,
                _ => 0
            };
        }
        return key switch
        {
            Key.Back => 8,
            Key.Tab => 9,
            Key.Return => 13,
            Key.Pause => 19,
            Key.CapsLock => 20,
            Key.Escape => 27,
            Key.PageUp => 33,
            Key.PageDown => 34,
            Key.End => 35,
            Key.Home => 36,
            Key.Left => 37,
            Key.Up => 38,
            Key.Right => 39,
            Key.Down => 40,
            Key.Insert => 45,
            Key.Delete => 46,
            Key.Space => 32,
            Key.LWin => 91,
            Key.RWin => 92,
            Key.Apps => 93,
            Key.NumLock => 144,
            Key.Scroll => 145,
            _ => PhysicalKeyCode(code)
        };
    }

    private static int PhysicalKeyCode(string code) => code switch
    {
        "Semicolon" => 186,
        "Equal" => 187,
        "Comma" => 188,
        "Minus" => 189,
        "Period" => 190,
        "Slash" => 191,
        "Backquote" => 192,
        "BracketLeft" => 219,
        "Backslash" => 220,
        "BracketRight" => 221,
        "Quote" => 222,
        "IntlBackslash" => 226,
        "ShiftLeft" or "ShiftRight" => 16,
        "ControlLeft" or "ControlRight" => 17,
        "AltLeft" or "AltRight" => 18,
        "Pause" => 19,
        "CapsLock" => 20,
        "MetaLeft" => 91,
        "MetaRight" => 92,
        "ContextMenu" => 93,
        "NumLock" => 144,
        "ScrollLock" => 145,
        "BrowserBack" => 166,
        "BrowserForward" => 167,
        "BrowserRefresh" => 168,
        "BrowserStop" => 169,
        "BrowserSearch" => 170,
        "BrowserFavorites" => 171,
        "BrowserHome" => 172,
        "AudioVolumeMute" => 173,
        "AudioVolumeDown" => 174,
        "AudioVolumeUp" => 175,
        "MediaTrackNext" => 176,
        "MediaTrackPrevious" => 177,
        "MediaStop" => 178,
        "MediaPlayPause" => 179,
        "LaunchMail" => 180,
        "MediaSelect" => 181,
        "LaunchApp1" => 182,
        "LaunchApp2" => 183,
        _ => 0
    };

    internal static string CodeName(PhysicalKey physicalKey)
    {
        string code = physicalKey.ToString();
        if (code.Length == 1 && char.IsAsciiLetter(code[0]))
        {
            return $"Key{code}";
        }
        return code.StartsWith("NumPad", StringComparison.Ordinal)
            ? $"Numpad{code[6..]}"
            : code;
    }

    private static bool HasPrimaryShortcutModifier(KeyModifiers modifiers, bool isMac) =>
        HasModifierShortcut(modifiers, isMac ? KeyModifiers.Meta : KeyModifiers.Control);

    private static bool HasModifierShortcut(
        KeyModifiers modifiers,
        KeyModifiers primary,
        bool allowShift = true)
    {
        KeyModifiers allowed = primary | (allowShift ? KeyModifiers.Shift : KeyModifiers.None);
        return modifiers.HasFlag(primary) && (modifiers & ~allowed) == KeyModifiers.None;
    }
}

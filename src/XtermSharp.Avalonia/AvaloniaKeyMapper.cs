using Avalonia.Input;

namespace XtermSharp.Avalonia;

internal static class AvaloniaKeyMapper
{
    public static TerminalKeyEvent Create(
        Key key,
        PhysicalKey physicalKey,
        string? symbol,
        KeyModifiers modifiers,
        TerminalKeyEventType eventType)
    {
        string code = physicalKey.ToString();
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

    public static bool ShouldUseTextInput(Key key, string? symbol, KeyModifiers modifiers)
    {
        if (string.IsNullOrEmpty(symbol) ||
            modifiers.HasFlag(KeyModifiers.Control) ||
            modifiers.HasFlag(KeyModifiers.Alt) ||
            modifiers.HasFlag(KeyModifiers.Meta))
        {
            return false;
        }
        return key is not (
            Key.Back or Key.Delete or Key.Return or Key.Escape or Key.Tab or
            Key.Insert or Key.Home or Key.End or Key.PageUp or Key.PageDown or
            Key.Left or Key.Right or Key.Up or Key.Down);
    }

    private static string KeyName(Key key, string? symbol) => key switch
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
        _ when !string.IsNullOrEmpty(symbol) => symbol,
        _ => key.ToString()
    };

    private static int KeyCode(Key key, string code)
    {
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
        return key switch
        {
            Key.Back => 8,
            Key.Tab => 9,
            Key.Return => 13,
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
        _ => 0
    };
}

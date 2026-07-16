namespace XtermSharp.Internal.Input;

internal static class Keyboard
{
    private const char Escape = '\x1b';

    private static readonly IReadOnlyDictionary<int, (char Unshifted, char Shifted)> KeyMappings =
        new Dictionary<int, (char, char)>
        {
            [48] = ('0', ')'), [49] = ('1', '!'), [50] = ('2', '@'), [51] = ('3', '#'),
            [52] = ('4', '$'), [53] = ('5', '%'), [54] = ('6', '^'), [55] = ('7', '&'),
            [56] = ('8', '*'), [57] = ('9', '('), [186] = (';', ':'), [187] = ('=', '+'),
            [188] = (',', '<'), [189] = ('-', '_'), [190] = ('.', '>'), [191] = ('/', '?'),
            [192] = ('`', '~'), [219] = ('[', '{'), [220] = ('\\', '|'), [221] = (']', '}'),
            [222] = ('\'', '"')
        };

    internal static KeyboardResult Evaluate(
        TerminalKeyEvent keyEvent,
        bool applicationCursorMode = false,
        bool isMac = false,
        bool macOptionIsMeta = false)
    {
        bool shift = keyEvent.Modifiers.HasFlag(TerminalModifiers.Shift);
        bool alt = keyEvent.Modifiers.HasFlag(TerminalModifiers.Alt);
        bool control = keyEvent.Modifiers.HasFlag(TerminalModifiers.Control);
        bool meta = keyEvent.Modifiers.HasFlag(TerminalModifiers.Meta);
        int modifiers = (shift ? 1 : 0) | (alt ? 2 : 0) | (control ? 4 : 0) | (meta ? 8 : 0);
        string? key = null;
        bool cancel = false;
        KeyboardResultType type = KeyboardResultType.SendKey;

        switch (keyEvent.KeyCode)
        {
            case 0:
                key = keyEvent.Key switch
                {
                    "UIKeyInputUpArrow" => applicationCursorMode ? "\x1bOA" : "\x1b[A",
                    "UIKeyInputLeftArrow" => applicationCursorMode ? "\x1bOD" : "\x1b[D",
                    "UIKeyInputRightArrow" => applicationCursorMode ? "\x1bOC" : "\x1b[C",
                    "UIKeyInputDownArrow" => applicationCursorMode ? "\x1bOB" : "\x1b[B",
                    _ => null
                };
                break;
            case 8:
                key = control ? "\b" : "\x7f";
                if (alt)
                {
                    key = Escape + key;
                }
                break;
            case 9:
                if (shift)
                {
                    key = "\x1b[Z";
                }
                else
                {
                    key = "\t";
                    cancel = true;
                }
                break;
            case 13:
                key = keyEvent.Key == "c" && control ? "\x03" : alt ? "\x1b\r" : "\r";
                cancel = true;
                break;
            case 27:
                key = alt ? "\x1b\x1b" : "\x1b";
                cancel = true;
                break;
            case 37:
                if (!meta)
                {
                    key = modifiers != 0 ? $"\x1b[1;{modifiers + 1}D" : applicationCursorMode ? "\x1bOD" : "\x1b[D";
                }
                break;
            case 39:
                if (!meta)
                {
                    key = modifiers != 0 ? $"\x1b[1;{modifiers + 1}C" : applicationCursorMode ? "\x1bOC" : "\x1b[C";
                }
                break;
            case 38:
                if (!meta)
                {
                    key = modifiers != 0 ? $"\x1b[1;{modifiers + 1}A" : applicationCursorMode ? "\x1bOA" : "\x1b[A";
                }
                break;
            case 40:
                if (!meta)
                {
                    key = modifiers != 0 ? $"\x1b[1;{modifiers + 1}B" : applicationCursorMode ? "\x1bOB" : "\x1b[B";
                }
                break;
            case 45:
                if (!shift && !control)
                {
                    key = "\x1b[2~";
                }
                break;
            case 46:
                key = modifiers != 0 ? $"\x1b[3;{modifiers + 1}~" : "\x1b[3~";
                break;
            case 36:
                key = modifiers != 0 ? $"\x1b[1;{modifiers + 1}H" : applicationCursorMode ? "\x1bOH" : "\x1b[H";
                break;
            case 35:
                key = modifiers != 0 ? $"\x1b[1;{modifiers + 1}F" : applicationCursorMode ? "\x1bOF" : "\x1b[F";
                break;
            case 33:
                if (shift)
                {
                    type = KeyboardResultType.PageUp;
                }
                else
                {
                    key = control ? $"\x1b[5;{modifiers + 1}~" : "\x1b[5~";
                }
                break;
            case 34:
                if (shift)
                {
                    type = KeyboardResultType.PageDown;
                }
                else
                {
                    key = control ? $"\x1b[6;{modifiers + 1}~" : "\x1b[6~";
                }
                break;
            case >= 112 and <= 123:
                key = FunctionKey(keyEvent.KeyCode, modifiers);
                break;
            default:
                if (control && !shift && !alt && !meta)
                {
                    if (keyEvent.KeyCode is >= 65 and <= 90)
                    {
                        key = ((char)(keyEvent.KeyCode - 64)).ToString();
                    }
                    else if (keyEvent.KeyCode == 32)
                    {
                        key = "\0";
                    }
                    else if (keyEvent.KeyCode is >= 51 and <= 55)
                    {
                        key = ((char)(keyEvent.KeyCode - 51 + 27)).ToString();
                    }
                    else if (keyEvent.KeyCode == 56)
                    {
                        key = "\x7f";
                    }
                    else if (keyEvent.Key == "/")
                    {
                        key = "\x1f";
                    }
                    else if (keyEvent.KeyCode == 219)
                    {
                        key = "\x1b";
                    }
                    else if (keyEvent.KeyCode == 220)
                    {
                        key = "\x1c";
                    }
                    else if (keyEvent.KeyCode == 221)
                    {
                        key = "\x1d";
                    }
                }
                else if ((!isMac || macOptionIsMeta) && alt && !meta)
                {
                    if (KeyMappings.TryGetValue(keyEvent.KeyCode, out (char Unshifted, char Shifted) mapping))
                    {
                        key = Escape + (shift ? mapping.Shifted : mapping.Unshifted).ToString();
                    }
                    else if (keyEvent.KeyCode is >= 65 and <= 90)
                    {
                        char character = (char)(control ? keyEvent.KeyCode - 64 : keyEvent.KeyCode + 32);
                        key = Escape + (shift ? char.ToUpperInvariant(character) : character).ToString();
                    }
                    else if (keyEvent.KeyCode == 32)
                    {
                        key = Escape + (control ? "\0" : " ");
                    }
                    else if (keyEvent.Key == "Dead" && keyEvent.Code.StartsWith("Key", StringComparison.Ordinal))
                    {
                        string character = keyEvent.Code.Substring(3, 1);
                        key = Escape + (shift ? character : character.ToLowerInvariant());
                        cancel = true;
                    }
                }
                else if (isMac && !alt && !control && !shift && meta && keyEvent.KeyCode == 65)
                {
                    type = KeyboardResultType.SelectAll;
                }
                else if (!string.IsNullOrEmpty(keyEvent.Key) && !control && !alt && !meta && keyEvent.KeyCode >= 48 && keyEvent.Key.Length == 1)
                {
                    key = keyEvent.Key;
                }
                else if (!string.IsNullOrEmpty(keyEvent.Key) && control && shift)
                {
                    key = keyEvent.Code switch
                    {
                        "Minus" => "\x1f",
                        "Digit2" => "\0",
                        "Digit6" => "\x1e",
                        _ => null
                    };
                }
                break;
        }

        return new KeyboardResult(type, key, cancel);
    }

    private static string FunctionKey(int keyCode, int modifiers)
    {
        string suffix = keyCode switch
        {
            112 => "P",
            113 => "Q",
            114 => "R",
            115 => "S",
            116 => "15~",
            117 => "17~",
            118 => "18~",
            119 => "19~",
            120 => "20~",
            121 => "21~",
            122 => "23~",
            123 => "24~",
            _ => throw new ArgumentOutOfRangeException(nameof(keyCode))
        };

        if (keyCode <= 115)
        {
            return modifiers == 0 ? $"\x1bO{suffix}" : $"\x1b[1;{modifiers + 1}{suffix}";
        }

        return modifiers == 0 ? $"\x1b[{suffix}" : $"\x1b[{suffix[..^1]};{modifiers + 1}~";
    }
}

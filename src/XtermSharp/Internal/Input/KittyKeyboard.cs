namespace XtermSharp.Internal.Input;

[Flags]
internal enum KittyKeyboardFlags : byte
{
    None = 0,
    DisambiguateEscapeCodes = 1 << 0,
    ReportEventTypes = 1 << 1,
    ReportAlternateKeys = 1 << 2,
    ReportAllKeysAsEscapeCodes = 1 << 3,
    ReportAssociatedText = 1 << 4
}

internal sealed class KittyKeyboard
{
    private const char Escape = '\x1b';

    private static readonly IReadOnlyDictionary<string, int> FunctionalKeyCodes =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Escape"] = 27, ["Enter"] = 13, ["Tab"] = 9, ["Backspace"] = 127,
            ["CapsLock"] = 57358, ["ScrollLock"] = 57359, ["NumLock"] = 57360,
            ["PrintScreen"] = 57361, ["Pause"] = 57362, ["ContextMenu"] = 57363,
            ["F13"] = 57376, ["F14"] = 57377, ["F15"] = 57378, ["F16"] = 57379,
            ["F17"] = 57380, ["F18"] = 57381, ["F19"] = 57382, ["F20"] = 57383,
            ["F21"] = 57384, ["F22"] = 57385, ["F23"] = 57386, ["F24"] = 57387,
            ["F25"] = 57388,
            ["MediaPlayPause"] = 57430, ["MediaStop"] = 57432, ["MediaTrackNext"] = 57435,
            ["MediaTrackPrevious"] = 57436, ["AudioVolumeDown"] = 57438,
            ["AudioVolumeUp"] = 57439, ["AudioVolumeMute"] = 57440
        };

    private static readonly IReadOnlyDictionary<string, int> CsiTildeKeys =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Insert"] = 2, ["Delete"] = 3, ["PageUp"] = 5, ["PageDown"] = 6,
            ["F5"] = 15, ["F6"] = 17, ["F7"] = 18, ["F8"] = 19, ["F9"] = 20,
            ["F10"] = 21, ["F11"] = 23, ["F12"] = 24
        };

    private static readonly IReadOnlyDictionary<string, char> CsiLetterKeys =
        new Dictionary<string, char>(StringComparer.Ordinal)
        {
            ["ArrowUp"] = 'A', ["ArrowDown"] = 'B', ["ArrowRight"] = 'C',
            ["ArrowLeft"] = 'D', ["Home"] = 'H', ["End"] = 'F'
        };

    private static readonly IReadOnlyDictionary<string, char> Ss3FunctionKeys =
        new Dictionary<string, char>(StringComparer.Ordinal)
        {
            ["F1"] = 'P', ["F2"] = 'Q', ["F3"] = 'R', ["F4"] = 'S'
        };

    internal static bool ShouldUseProtocol(KittyKeyboardFlags flags) => flags != KittyKeyboardFlags.None;

    internal KeyboardResult Evaluate(
        TerminalKeyEvent keyEvent,
        KittyKeyboardFlags flags,
        bool macOptionAsAlt = false)
    {
        var result = new KeyboardResult(KeyboardResultType.SendKey);
        int modifiers = EncodeModifiers(keyEvent);
        bool isModifier = IsModifierKey(keyEvent);
        bool reportEventTypes = flags.HasFlag(KittyKeyboardFlags.ReportEventTypes);
        int eventType = keyEvent.EventType switch
        {
            TerminalKeyEventType.Press => 1,
            TerminalKeyEventType.Repeat => 2,
            TerminalKeyEventType.Release => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(keyEvent))
        };

        if (!reportEventTypes && keyEvent.EventType == TerminalKeyEventType.Release)
        {
            return result;
        }

        if (isModifier && !flags.HasFlag(KittyKeyboardFlags.ReportAllKeysAsEscapeCodes))
        {
            return result;
        }

        if (IsLockKey(keyEvent) && !flags.HasFlag(KittyKeyboardFlags.ReportAllKeysAsEscapeCodes))
        {
            return result;
        }

        if (CsiLetterKeys.TryGetValue(keyEvent.Key, out char csiLetter))
        {
            return new KeyboardResult(
                KeyboardResultType.SendKey,
                BuildCsiLetterSequence(csiLetter, modifiers, eventType, reportEventTypes),
                true);
        }

        if (Ss3FunctionKeys.TryGetValue(keyEvent.Key, out char ss3Letter))
        {
            return new KeyboardResult(
                KeyboardResultType.SendKey,
                BuildSs3Sequence(ss3Letter, modifiers, eventType, reportEventTypes),
                true);
        }

        if (CsiTildeKeys.TryGetValue(keyEvent.Key, out int tildeCode))
        {
            return new KeyboardResult(
                KeyboardResultType.SendKey,
                BuildCsiTildeSequence(tildeCode, modifiers, eventType, reportEventTypes),
                true);
        }

        int? keyCode = GetKeyCode(keyEvent, macOptionAsAlt);
        if (keyCode is null)
        {
            return result;
        }

        bool specialKey = keyCode is 13 or 9 or 127;
        if (specialKey &&
            keyEvent.EventType == TerminalKeyEventType.Release &&
            !flags.HasFlag(KittyKeyboardFlags.ReportAllKeysAsEscapeCodes))
        {
            return result;
        }

        bool isFunctional = FunctionalKeyCodes.ContainsKey(keyEvent.Key) || GetNumpadKeyCode(keyEvent) is not null;
        bool useCsiU =
            flags.HasFlag(KittyKeyboardFlags.ReportAllKeysAsEscapeCodes) ||
            (reportEventTypes && keyEvent.EventType == TerminalKeyEventType.Release) ||
            ((flags.HasFlag(KittyKeyboardFlags.DisambiguateEscapeCodes) || reportEventTypes) &&
             ((isFunctional && !specialKey) ||
              ((modifiers > 0 && keyEvent.Key.Length != 1) || modifiers - 1 > 1)));

        if (useCsiU)
        {
            return new KeyboardResult(
                KeyboardResultType.SendKey,
                BuildCsiUSequence(keyEvent, keyCode.Value, modifiers, eventType, flags, isFunctional, isModifier),
                true);
        }

        string? legacyByte = keyCode switch
        {
            13 => "\r",
            9 => "\t",
            127 => "\x7f",
            _ => null
        };
        if (legacyByte is not null)
        {
            return result with { Key = legacyByte };
        }

        if (keyEvent.Key.Length == 1 &&
            !keyEvent.Modifiers.HasFlag(TerminalModifiers.Control) &&
            !keyEvent.Modifiers.HasFlag(TerminalModifiers.Alt) &&
            !keyEvent.Modifiers.HasFlag(TerminalModifiers.Meta))
        {
            return result with { Key = keyEvent.Key };
        }

        return result;
    }

    private static int? GetNumpadKeyCode(TerminalKeyEvent keyEvent)
    {
        if (!keyEvent.Code.StartsWith("Numpad", StringComparison.Ordinal))
        {
            return null;
        }

        string suffix = keyEvent.Code[6..];
        if (suffix.Length == 1 && suffix[0] is >= '0' and <= '9')
        {
            return 57399 + suffix[0] - '0';
        }

        return suffix switch
        {
            "Decimal" => 57409,
            "Divide" => 57410,
            "Multiply" => 57411,
            "Subtract" => 57412,
            "Add" => 57413,
            "Enter" => 57414,
            "Equal" => 57415,
            _ => null
        };
    }

    private static int? GetModifierKeyCode(string code) => code switch
    {
        "ShiftLeft" => 57441,
        "ShiftRight" => 57447,
        "ControlLeft" => 57442,
        "ControlRight" => 57448,
        "AltLeft" => 57443,
        "AltRight" => 57449,
        "MetaLeft" => 57444,
        "MetaRight" => 57450,
        _ => null
    };

    private static int EncodeModifiers(TerminalKeyEvent keyEvent)
    {
        int modifiers = 0;
        if (keyEvent.Modifiers.HasFlag(TerminalModifiers.Shift))
        {
            modifiers |= 1;
        }
        if (keyEvent.Modifiers.HasFlag(TerminalModifiers.Alt))
        {
            modifiers |= 2;
        }
        if (keyEvent.Modifiers.HasFlag(TerminalModifiers.Control))
        {
            modifiers |= 4;
        }
        if (keyEvent.Modifiers.HasFlag(TerminalModifiers.Meta))
        {
            modifiers |= 8;
        }
        return modifiers > 0 ? modifiers + 1 : 0;
    }

    private static int? GetKeyCode(TerminalKeyEvent keyEvent, bool macOptionAsAlt)
    {
        int? numpadCode = GetNumpadKeyCode(keyEvent);
        if (numpadCode is not null)
        {
            return numpadCode;
        }

        int? modifierCode = GetModifierKeyCode(keyEvent.Code);
        if (modifierCode is not null)
        {
            return modifierCode;
        }

        if (FunctionalKeyCodes.TryGetValue(keyEvent.Key, out int functionalCode))
        {
            return functionalCode;
        }

        bool shift = keyEvent.Modifiers.HasFlag(TerminalModifiers.Shift);
        bool alt = keyEvent.Modifiers.HasFlag(TerminalModifiers.Alt);
        if ((shift || (macOptionAsAlt && alt)) && keyEvent.Code.Length > 0)
        {
            if (keyEvent.Code.StartsWith("Digit", StringComparison.Ordinal) &&
                keyEvent.Code.Length == 6 && keyEvent.Code[5] is >= '0' and <= '9')
            {
                return keyEvent.Code[5];
            }

            if (keyEvent.Code.StartsWith("Key", StringComparison.Ordinal) && keyEvent.Code.Length == 4)
            {
                return char.ToLowerInvariant(keyEvent.Code[3]);
            }
        }

        if (keyEvent.Key.Length == 1)
        {
            int codePoint = keyEvent.Key[0];
            return codePoint is >= 65 and <= 90 ? codePoint + 32 : codePoint;
        }

        return null;
    }

    private static bool IsModifierKey(TerminalKeyEvent keyEvent) =>
        keyEvent.Key is "Shift" or "Control" or "Alt" or "Meta";

    private static bool IsLockKey(TerminalKeyEvent keyEvent) =>
        keyEvent.Key is "CapsLock" or "NumLock" or "ScrollLock";

    private static string BuildCsiLetterSequence(char letter, int modifiers, int eventType, bool reportEventTypes)
    {
        bool needsEventType = reportEventTypes && eventType != 1;
        if (modifiers > 0 || needsEventType)
        {
            return $"{Escape}[1;{(modifiers > 0 ? modifiers : 1)}{(needsEventType ? $":{eventType}" : string.Empty)}{letter}";
        }
        return $"{Escape}[{letter}";
    }

    private static string BuildSs3Sequence(char letter, int modifiers, int eventType, bool reportEventTypes)
    {
        bool needsEventType = reportEventTypes && eventType != 1;
        if (modifiers > 0 || needsEventType)
        {
            return $"{Escape}[1;{(modifiers > 0 ? modifiers : 1)}{(needsEventType ? $":{eventType}" : string.Empty)}{letter}";
        }
        return $"{Escape}O{letter}";
    }

    private static string BuildCsiTildeSequence(int number, int modifiers, int eventType, bool reportEventTypes)
    {
        bool needsEventType = reportEventTypes && eventType != 1;
        string suffix = modifiers > 0 || needsEventType
            ? $";{(modifiers > 0 ? modifiers : 1)}{(needsEventType ? $":{eventType}" : string.Empty)}"
            : string.Empty;
        return $"{Escape}[{number}{suffix}~";
    }

    private static string BuildCsiUSequence(
        TerminalKeyEvent keyEvent,
        int keyCode,
        int modifiers,
        int eventType,
        KittyKeyboardFlags flags,
        bool isFunctional,
        bool isModifier)
    {
        bool reportEventTypes = flags.HasFlag(KittyKeyboardFlags.ReportEventTypes);
        bool reportAlternateKeys = flags.HasFlag(KittyKeyboardFlags.ReportAlternateKeys);
        string sequence = $"{Escape}[{keyCode}";

        if (reportAlternateKeys &&
            keyEvent.Modifiers.HasFlag(TerminalModifiers.Shift) &&
            keyEvent.Key.Length == 1 &&
            !isFunctional &&
            !isModifier)
        {
            sequence += $":{(int)keyEvent.Key[0]}";
        }

        bool reportAssociatedText =
            flags.HasFlag(KittyKeyboardFlags.ReportAssociatedText) &&
            eventType != 3 &&
            keyEvent.Key.Length == 1 &&
            !isFunctional &&
            !isModifier &&
            !keyEvent.Modifiers.HasFlag(TerminalModifiers.Control);
        int? textCode = reportAssociatedText ? keyEvent.Key[0] : null;
        bool needsEventType = reportEventTypes && eventType != 1 && (eventType == 3 || textCode is null);

        if (modifiers > 0 || needsEventType || textCode is not null)
        {
            sequence += ";";
            if (modifiers > 0)
            {
                sequence += modifiers;
            }
            else if (needsEventType)
            {
                sequence += "1";
            }
            if (needsEventType)
            {
                sequence += $":{eventType}";
            }
        }

        if (textCode is not null)
        {
            sequence += $";{textCode}";
        }

        return sequence + "u";
    }
}

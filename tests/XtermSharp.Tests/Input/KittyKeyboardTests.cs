using XtermSharp.Internal.Input;

namespace XtermSharp.Tests.Input;

public sealed class KittyKeyboardTests
{
    private const KittyKeyboardFlags D = KittyKeyboardFlags.DisambiguateEscapeCodes;
    private const KittyKeyboardFlags E = KittyKeyboardFlags.ReportEventTypes;
    private const KittyKeyboardFlags L = KittyKeyboardFlags.ReportAlternateKeys;
    private const KittyKeyboardFlags A = KittyKeyboardFlags.ReportAllKeysAsEscapeCodes;
    private const KittyKeyboardFlags T = KittyKeyboardFlags.ReportAssociatedText;

    private static readonly IReadOnlyDictionary<string, Action> Assertions = CreateAssertions();

    public static TheoryData<string> Cases { get; } =
        UpstreamInputRows.ForFile("src/common/input/KittyKeyboard.test.ts");

    [Theory]
    [MemberData(nameof(Cases))]
    public void Matches_upstream_kitty_keyboard_cases(string upstreamId)
    {
        Assert.True(Assertions.TryGetValue(upstreamId, out Action? assertion), $"Missing assertion for {upstreamId}.");
        assertion!();
    }

    private static IReadOnlyDictionary<string, Action> CreateAssertions()
    {
        var assertions = new Dictionary<string, Action>(StringComparer.Ordinal)
        {
            [Id(255)] = () => Assert.False(KittyKeyboard.ShouldUseProtocol(KittyKeyboardFlags.None)),
            [Id(256)] = () =>
            {
                Assert.True(KittyKeyboard.ShouldUseProtocol(D));
                Assert.True(KittyKeyboard.ShouldUseProtocol(E));
                Assert.True(KittyKeyboard.ShouldUseProtocol((KittyKeyboardFlags)0b11111));
            }
        };

        Add(assertions, 257, "A", "A", modifiers: TerminalModifiers.Shift);
        Add(assertions, 258, "a", "\x1b[97;3u", modifiers: TerminalModifiers.Alt);
        Add(assertions, 259, "a", "\x1b[97;5u", modifiers: TerminalModifiers.Control);
        Add(assertions, 260, "a", "\x1b[97;9u", modifiers: TerminalModifiers.Meta);
        Add(assertions, 261, "a", "\x1b[97;6u", modifiers: TerminalModifiers.Control | TerminalModifiers.Shift);
        Add(assertions, 262, "a", "\x1b[97;7u", modifiers: TerminalModifiers.Control | TerminalModifiers.Alt);
        Add(assertions, 263, "a", "\x1b[97;8u", modifiers: TerminalModifiers.Control | TerminalModifiers.Alt | TerminalModifiers.Shift);
        Add(assertions, 264, "a", "\x1b[97;13u", modifiers: TerminalModifiers.Control | TerminalModifiers.Meta);
        Add(assertions, 265, "a", "\x1b[97;16u", modifiers: TerminalModifiers.Shift | TerminalModifiers.Alt | TerminalModifiers.Control | TerminalModifiers.Meta);
        Add(assertions, 266, "Escape", "\x1b[27u");

        Add(assertions, 267, "Escape", "\x1b[27u");
        Add(assertions, 268, "Enter", "\r");
        Add(assertions, 269, "Tab", "\t");
        Add(assertions, 270, "Backspace", "\x7f");
        Add(assertions, 271, " ", " ");
        Add(assertions, 272, "Tab", "\x1b[9;2u", modifiers: TerminalModifiers.Shift);
        Add(assertions, 273, "Enter", "\x1b[13;5u", modifiers: TerminalModifiers.Control);
        Add(assertions, 274, "Escape", "\x1b[27;3u", modifiers: TerminalModifiers.Alt);
        Add(assertions, 275, "Backspace", "\x1b[127;5u", modifiers: TerminalModifiers.Control);
        Add(assertions, 276, " ", "\x1b[32;5u", modifiers: TerminalModifiers.Control);
        Add(assertions, 277, " ", "\x1b[32;3u", modifiers: TerminalModifiers.Alt);

        Add(assertions, 278, "Insert", "\x1b[2~");
        Add(assertions, 279, "Delete", "\x1b[3~");
        Add(assertions, 280, "PageUp", "\x1b[5~");
        Add(assertions, 281, "PageDown", "\x1b[6~");
        Add(assertions, 282, "Home", "\x1b[H");
        Add(assertions, 283, "End", "\x1b[F");
        Add(assertions, 284, "PageUp", "\x1b[5;2~", modifiers: TerminalModifiers.Shift);
        Add(assertions, 285, "Home", "\x1b[1;5H", modifiers: TerminalModifiers.Control);
        Add(assertions, 286, "ArrowUp", "\x1b[A");
        Add(assertions, 287, "ArrowDown", "\x1b[B");
        Add(assertions, 288, "ArrowRight", "\x1b[C");
        Add(assertions, 289, "ArrowLeft", "\x1b[D");
        Add(assertions, 290, "ArrowUp", "\x1b[1;2A", modifiers: TerminalModifiers.Shift);
        Add(assertions, 291, "ArrowLeft", "\x1b[1;5D", modifiers: TerminalModifiers.Control);
        Add(assertions, 292, "ArrowRight", "\x1b[1;6C", modifiers: TerminalModifiers.Control | TerminalModifiers.Shift);

        string[] functionKeys = ["F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12"];
        string[] functionSequences = ["\x1bOP", "\x1bOQ", "\x1bOR", "\x1bOS", "\x1b[15~", "\x1b[17~", "\x1b[18~", "\x1b[19~", "\x1b[20~", "\x1b[21~", "\x1b[23~", "\x1b[24~"];
        for (int index = 0; index < functionKeys.Length; index++)
        {
            Add(assertions, 293 + index, functionKeys[index], functionSequences[index]);
        }
        Add(assertions, 305, "F1", "\x1b[1;2P", modifiers: TerminalModifiers.Shift);
        Add(assertions, 306, "F5", "\x1b[15;5~", modifiers: TerminalModifiers.Control);

        Add(assertions, 307, "F13", "\x1b[57376u");
        Add(assertions, 308, "F14", "\x1b[57377u");
        Add(assertions, 309, "F20", "\x1b[57383u");
        Add(assertions, 310, "F24", "\x1b[57387u");

        Add(assertions, 311, "0", "\x1b[57399u", "Numpad0");
        Add(assertions, 312, "1", "\x1b[57400u", "Numpad1");
        Add(assertions, 313, "9", "\x1b[57408u", "Numpad9");
        Add(assertions, 314, ".", "\x1b[57409u", "NumpadDecimal");
        Add(assertions, 315, "/", "\x1b[57410u", "NumpadDivide");
        Add(assertions, 316, "*", "\x1b[57411u", "NumpadMultiply");
        Add(assertions, 317, "-", "\x1b[57412u", "NumpadSubtract");
        Add(assertions, 318, "+", "\x1b[57413u", "NumpadAdd");
        Add(assertions, 319, "Enter", "\x1b[57414u", "NumpadEnter");
        Add(assertions, 320, "=", "\x1b[57415u", "NumpadEqual");
        Add(assertions, 321, "5", "\x1b[57404;5u", "Numpad5", TerminalModifiers.Control);

        Add(assertions, 322, "Shift", "\x1b[57441;2u", "ShiftLeft", TerminalModifiers.Shift, A);
        Add(assertions, 323, "Shift", "\x1b[57447;2u", "ShiftRight", TerminalModifiers.Shift, A);
        Add(assertions, 324, "Control", "\x1b[57442;5u", "ControlLeft", TerminalModifiers.Control, A);
        Add(assertions, 325, "Control", "\x1b[57448;5u", "ControlRight", TerminalModifiers.Control, A);
        Add(assertions, 326, "Alt", "\x1b[57443;3u", "AltLeft", TerminalModifiers.Alt, A);
        Add(assertions, 327, "Alt", "\x1b[57449;3u", "AltRight", TerminalModifiers.Alt, A);
        Add(assertions, 328, "Meta", "\x1b[57444;9u", "MetaLeft", TerminalModifiers.Meta, A);
        Add(assertions, 329, "Meta", "\x1b[57450;9u", "MetaRight", TerminalModifiers.Meta, A);
        Add(assertions, 330, "CapsLock", "\x1b[57358u", "CapsLock", flags: A);
        Add(assertions, 331, "NumLock", "\x1b[57360u", "NumLock", flags: A);
        Add(assertions, 332, "ScrollLock", "\x1b[57359u", "ScrollLock", flags: A);

        KittyKeyboardFlags eventFlags = D | E;
        Add(assertions, 333, "a", "a", flags: eventFlags);
        Add(assertions, 334, "Escape", "\x1b[27u", flags: eventFlags);
        Add(assertions, 335, "Enter", "\r", flags: eventFlags);
        Add(assertions, 336, "Tab", "\t", flags: eventFlags);
        Add(assertions, 337, "Backspace", "\x7f", flags: eventFlags);
        Add(assertions, 338, "a", "\x1b[97;5u", modifiers: TerminalModifiers.Control, flags: eventFlags);
        Add(assertions, 339, "a", "a", flags: eventFlags, eventType: TerminalKeyEventType.Repeat);
        Add(assertions, 340, "Escape", "\x1b[27;1:2u", flags: eventFlags, eventType: TerminalKeyEventType.Repeat);
        Add(assertions, 341, "Enter", "\r", flags: eventFlags, eventType: TerminalKeyEventType.Repeat);
        Add(assertions, 342, "Tab", "\t", flags: eventFlags, eventType: TerminalKeyEventType.Repeat);
        Add(assertions, 343, "Backspace", "\x7f", flags: eventFlags, eventType: TerminalKeyEventType.Repeat);
        Add(assertions, 344, "a", "\x1b[97;1:3u", flags: eventFlags, eventType: TerminalKeyEventType.Release);
        Add(assertions, 345, "Escape", "\x1b[27;1:3u", flags: eventFlags, eventType: TerminalKeyEventType.Release);
        Add(assertions, 346, "Enter", null, flags: eventFlags, eventType: TerminalKeyEventType.Release);
        Add(assertions, 347, "Tab", null, flags: eventFlags, eventType: TerminalKeyEventType.Release);
        Add(assertions, 348, "Backspace", null, flags: eventFlags, eventType: TerminalKeyEventType.Release);
        Add(assertions, 349, "a", "\x1b[97;5:3u", modifiers: TerminalModifiers.Control, flags: eventFlags, eventType: TerminalKeyEventType.Release);
        Add(assertions, 350, "a", "\x1b[97;4:2u", modifiers: TerminalModifiers.Shift | TerminalModifiers.Alt, flags: eventFlags, eventType: TerminalKeyEventType.Repeat);
        Add(assertions, 351, "Delete", "\x1b[3;1:3~", flags: eventFlags, eventType: TerminalKeyEventType.Release);
        Add(assertions, 352, "Shift", "\x1b[57441;1:3u", "ShiftLeft", flags: eventFlags | A, eventType: TerminalKeyEventType.Release);

        Add(assertions, 353, "a", "\x1b[97;5u", modifiers: TerminalModifiers.Control, flags: E);
        Add(assertions, 354, "a", "\x1b[97;5:2u", modifiers: TerminalModifiers.Control, flags: E, eventType: TerminalKeyEventType.Repeat);
        Add(assertions, 355, "a", "\x1b[97;5:3u", modifiers: TerminalModifiers.Control, flags: E, eventType: TerminalKeyEventType.Release);
        Add(assertions, 356, "Shift", null, "ShiftLeft", TerminalModifiers.Shift, E);
        Add(assertions, 357, "Shift", null, "ShiftLeft", flags: E, eventType: TerminalKeyEventType.Release);
        assertions[Id(358)] = () =>
        {
            AssertKey(null, "CapsLock", "CapsLock", flags: D);
            AssertKey(null, "CapsLock", "CapsLock", flags: E);
            AssertKey(null, "CapsLock", "CapsLock", flags: D | E);
        };
        Add(assertions, 359, "NumLock", null, "NumLock");
        Add(assertions, 360, "ScrollLock", null, "ScrollLock");
        Add(assertions, 361, "CapsLock", null, "CapsLock", flags: E, eventType: TerminalKeyEventType.Release);

        Add(assertions, 362, "a", "\x1b[97u", flags: A);
        Add(assertions, 363, "A", "\x1b[97;2u", modifiers: TerminalModifiers.Shift, flags: A);
        Add(assertions, 364, "5", "\x1b[53u", flags: A);
        assertions[Id(365)] = () =>
        {
            AssertKey("\x1b[46u", ".", flags: A);
            AssertKey("\x1b[44u", ",", flags: A);
            AssertKey("\x1b[59u", ";", flags: A);
            AssertKey("\x1b[47u", "/", flags: A);
        };
        assertions[Id(366)] = () =>
        {
            AssertKey("\x1b[91u", "[", flags: A);
            AssertKey("\x1b[93u", "]", flags: A);
        };
        Add(assertions, 367, " ", "\x1b[32u", flags: A);

        KittyKeyboardFlags allWithEvents = A | E;
        Add(assertions, 368, "Enter", "\x1b[13u", flags: allWithEvents);
        Add(assertions, 369, "Tab", "\x1b[9u", flags: allWithEvents);
        Add(assertions, 370, "Backspace", "\x1b[127u", flags: allWithEvents);
        Add(assertions, 371, "Enter", "\x1b[13;1:2u", flags: allWithEvents, eventType: TerminalKeyEventType.Repeat);
        Add(assertions, 372, "Tab", "\x1b[9;1:2u", flags: allWithEvents, eventType: TerminalKeyEventType.Repeat);
        Add(assertions, 373, "Backspace", "\x1b[127;1:2u", flags: allWithEvents, eventType: TerminalKeyEventType.Repeat);
        Add(assertions, 374, "Enter", "\x1b[13;1:3u", flags: allWithEvents, eventType: TerminalKeyEventType.Release);
        Add(assertions, 375, "Tab", "\x1b[9;1:3u", flags: allWithEvents, eventType: TerminalKeyEventType.Release);
        Add(assertions, 376, "Backspace", "\x1b[127;1:3u", flags: allWithEvents, eventType: TerminalKeyEventType.Release);

        KittyKeyboardFlags associated = A | T;
        Add(assertions, 377, "a", "\x1b[97;;97u", flags: associated);
        Add(assertions, 378, "A", "\x1b[97;2;65u", modifiers: TerminalModifiers.Shift, flags: associated);
        Add(assertions, 379, "a", "\x1b[97;5u", modifiers: TerminalModifiers.Control, flags: associated);
        Add(assertions, 380, "Escape", "\x1b[27u", flags: associated);
        Add(assertions, 381, "a", "\x1b[97;1:3u", flags: associated | E, eventType: TerminalKeyEventType.Release);
        Add(assertions, 382, "5", "\x1b[53;;53u", flags: associated);
        Add(assertions, 383, "%", "\x1b[53;2;37u", "Digit5", TerminalModifiers.Shift, associated);

        KittyKeyboardFlags alternate = A | L;
        Add(assertions, 384, "A", "\x1b[97:65;2u", "KeyA", TerminalModifiers.Shift, alternate);
        Add(assertions, 385, "a", "\x1b[97u", "KeyA", flags: alternate);
        Add(assertions, 386, "%", "\x1b[53:37;2u", "Digit5", TerminalModifiers.Shift, alternate);
        Add(assertions, 387, "Escape", "\x1b[27;2u", modifiers: TerminalModifiers.Shift, flags: alternate);
        Add(assertions, 388, "A", "\x1b[97:65;2;65u", "KeyA", TerminalModifiers.Shift, A | L | T);
        Add(assertions, 389, "A", "\x1b[97:65;2:3u", "KeyA", TerminalModifiers.Shift, A | L | T | E, TerminalKeyEventType.Release);
        Add(assertions, 390, "a", null, eventType: TerminalKeyEventType.Release);

        Add(assertions, 391, "A", "A", modifiers: TerminalModifiers.Shift);
        Add(assertions, 392, "A", "\x1b[97;6u", modifiers: TerminalModifiers.Control | TerminalModifiers.Shift);
        Add(assertions, 393, "Dead", null);
        Add(assertions, 394, "Unidentified", null);
        Add(assertions, 395, "PrintScreen", "\x1b[57361u");
        Add(assertions, 396, "Pause", "\x1b[57362u");
        Add(assertions, 397, "ContextMenu", "\x1b[57363u");
        Add(assertions, 398, "MediaPlayPause", "\x1b[57430u");
        Add(assertions, 399, "MediaStop", "\x1b[57432u");
        Add(assertions, 400, "MediaTrackNext", "\x1b[57435u");
        Add(assertions, 401, "MediaTrackPrevious", "\x1b[57436u");
        Add(assertions, 402, "AudioVolumeDown", "\x1b[57438u");
        Add(assertions, 403, "AudioVolumeUp", "\x1b[57439u");
        Add(assertions, 404, "AudioVolumeMute", "\x1b[57440u");

        Add(assertions, 405, "ƒ", "\x1b[102;3u", "KeyF", TerminalModifiers.Alt, macOptionAsAlt: true);
        Add(assertions, 406, "∫", "\x1b[98;3u", "KeyB", TerminalModifiers.Alt, macOptionAsAlt: true);
        Add(assertions, 407, "∂", "\x1b[100;3u", "KeyD", TerminalModifiers.Alt, macOptionAsAlt: true);
        Add(assertions, 408, "Dead", "\x1b[110;3u", "KeyN", TerminalModifiers.Alt, macOptionAsAlt: true);
        Add(assertions, 409, "Dead", "\x1b[101;3u", "KeyE", TerminalModifiers.Alt, macOptionAsAlt: true);
        Add(assertions, 410, "Dead", "\x1b[117;3u", "KeyU", TerminalModifiers.Alt, macOptionAsAlt: true);
        Add(assertions, 411, "∞", "\x1b[53;3u", "Digit5", TerminalModifiers.Alt, macOptionAsAlt: true);
        Add(assertions, 412, "Ï", "\x1b[102;4u", "KeyF", TerminalModifiers.Alt | TerminalModifiers.Shift, macOptionAsAlt: true);
        Add(assertions, 413, "ƒ", "\x1b[102;7u", "KeyF", TerminalModifiers.Alt | TerminalModifiers.Control, macOptionAsAlt: true);
        Add(assertions, 414, "a", "\x1b[97;3u", "KeyA", TerminalModifiers.Alt, macOptionAsAlt: false);
        Add(assertions, 415, "a", "\x1b[97;3u", "KeyQ", TerminalModifiers.Alt, macOptionAsAlt: false);
        Add(assertions, 416, "ƒ", "\x1b[402;3u", "KeyF", TerminalModifiers.Alt, macOptionAsAlt: false);
        Add(assertions, 417, "ƒ", "ƒ", "KeyF", macOptionAsAlt: true);
        Add(assertions, 418, "…", "\x1b[8230;3u", "Semicolon", TerminalModifiers.Alt, macOptionAsAlt: true);
        Add(assertions, 419, "ƒ", "\x1b[102;3:3u", "KeyF", TerminalModifiers.Alt, D | E, TerminalKeyEventType.Release, true);

        Assert.Equal(165, assertions.Count);
        return assertions;
    }

    private static void Add(
        IDictionary<string, Action> assertions,
        int id,
        string key,
        string? expected,
        string code = "",
        TerminalModifiers modifiers = TerminalModifiers.None,
        KittyKeyboardFlags flags = D,
        TerminalKeyEventType eventType = TerminalKeyEventType.Press,
        bool macOptionAsAlt = false)
    {
        TerminalKeyEvent keyEvent = KeyEvent.Create(key, code, modifiers: modifiers, eventType: eventType);
        assertions[Id(id)] = () => Assert.Equal(expected, new KittyKeyboard().Evaluate(keyEvent, flags, macOptionAsAlt).Key);
    }

    private static void AssertKey(
        string? expected,
        string key,
        string code = "",
        TerminalModifiers modifiers = TerminalModifiers.None,
        KittyKeyboardFlags flags = D,
        TerminalKeyEventType eventType = TerminalKeyEventType.Press,
        bool macOptionAsAlt = false)
    {
        TerminalKeyEvent keyEvent = KeyEvent.Create(key, code, modifiers: modifiers, eventType: eventType);
        Assert.Equal(expected, new KittyKeyboard().Evaluate(keyEvent, flags, macOptionAsAlt).Key);
    }

    private static string Id(int value) => $"XTJS-{value:0000}";
}

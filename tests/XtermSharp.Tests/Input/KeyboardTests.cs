using XtermSharp.Internal.Input;

namespace XtermSharp.Tests.Input;

public sealed class KeyboardTests
{
    private static readonly IReadOnlyDictionary<string, Action> Assertions = CreateAssertions();

    public static TheoryData<string> Cases { get; } =
        UpstreamInputRows.ForFile("src/common/input/Keyboard.test.ts");

    [Theory]
    [MemberData(nameof(Cases))]
    public void Matches_upstream_keyboard_cases(string upstreamId)
    {
        Assert.True(Assertions.TryGetValue(upstreamId, out Action? assertion), $"Missing assertion for {upstreamId}.");
        assertion!();
    }

    private static IReadOnlyDictionary<string, Action> CreateAssertions()
    {
        var assertions = new Dictionary<string, Action>(StringComparer.Ordinal)
        {
            ["XTJS-0194"] = AssertUnmodifiedKeys,
            ["XTJS-0195"] = () => AssertKey("\x1b[3;5~", keyCode: 46, modifiers: TerminalModifiers.Control),
            ["XTJS-0196"] = () => AssertKey("\x1b[3;2~", keyCode: 46, modifiers: TerminalModifiers.Shift),
            ["XTJS-0197"] = () => AssertKey("\x1b[3;3~", keyCode: 46, modifiers: TerminalModifiers.Alt),
            ["XTJS-0198"] = () => AssertKey("\x1b\r", keyCode: 13, modifiers: TerminalModifiers.Alt),
            ["XTJS-0199"] = () => AssertKey("\x1b\x1b", keyCode: 27, modifiers: TerminalModifiers.Alt),
            ["XTJS-0200"] = () => AssertKey("\x1b[1;5D", keyCode: 37, modifiers: TerminalModifiers.Control),
            ["XTJS-0201"] = () => AssertKey("\x1b[1;5C", keyCode: 39, modifiers: TerminalModifiers.Control),
            ["XTJS-0202"] = () => AssertKey("\x1b[1;5A", keyCode: 38, modifiers: TerminalModifiers.Control),
            ["XTJS-0203"] = () => AssertKey("\x1b[1;5B", keyCode: 40, modifiers: TerminalModifiers.Control),
            ["XTJS-0204"] = () => AssertKey("\b", keyCode: 8, modifiers: TerminalModifiers.Control),
            ["XTJS-0205"] = () => AssertKey("\x1b\x7f", keyCode: 8, modifiers: TerminalModifiers.Alt),
            ["XTJS-0206"] = () => AssertKey("\x1b\b", keyCode: 8, modifiers: TerminalModifiers.Control | TerminalModifiers.Alt),
            ["XTJS-0207"] = () => AssertKey("\x1b[3;2~", keyCode: 46, modifiers: TerminalModifiers.Shift),
            ["XTJS-0208"] = () => AssertKey("\x1b[3;3~", keyCode: 46, modifiers: TerminalModifiers.Alt),
            ["XTJS-0209"] = () => AssertKey("\x1b[1;3A", keyCode: 38, modifiers: TerminalModifiers.Alt),
            ["XTJS-0210"] = () => AssertKey("\x1b[1;3B", keyCode: 40, modifiers: TerminalModifiers.Alt),
            ["XTJS-0211"] = AssertModifiedFunctionKeys,
            ["XTJS-0212"] = () => AssertKey("\x1b\x01", keyCode: 65, modifiers: TerminalModifiers.Control | TerminalModifiers.Alt),
            ["XTJS-0234"] = AssertMobileArrows,
            ["XTJS-0235"] = AssertLowercaseCharacters,
            ["XTJS-0236"] = AssertUppercaseCharacters,
            ["XTJS-0237"] = AssertAltShiftLetters,
            ["XTJS-0238"] = () => AssertKey("\0", "@", "Digit2", 50, TerminalModifiers.Control | TerminalModifiers.Shift),
            ["XTJS-0239"] = () => AssertKey("\x1e", "^", "Digit6", 54, TerminalModifiers.Control | TerminalModifiers.Shift),
            ["XTJS-0240"] = () => AssertKey("\x1f", "_", "Minus", 189, TerminalModifiers.Control | TerminalModifiers.Shift),
            ["XTJS-0241"] = () => AssertKey("\x1b[1;3D", keyCode: 37, modifiers: TerminalModifiers.Alt, isMac: false),
            ["XTJS-0242"] = () => AssertKey("\x1b[1;3C", keyCode: 39, modifiers: TerminalModifiers.Alt, isMac: false),
            ["XTJS-0243"] = () => AssertKey("\x1b[1;3A", keyCode: 38, modifiers: TerminalModifiers.Alt, isMac: false),
            ["XTJS-0244"] = () => AssertKey("\x1b[1;3B", keyCode: 40, modifiers: TerminalModifiers.Alt, isMac: false),
            ["XTJS-0245"] = () => AssertKey("\x1b" + "a", keyCode: 65, modifiers: TerminalModifiers.Alt, isMac: false),
            ["XTJS-0246"] = () => AssertKey("\x1b ", keyCode: 32, modifiers: TerminalModifiers.Alt, isMac: false),
            ["XTJS-0247"] = () => AssertKey("\x1b\0", keyCode: 32, modifiers: TerminalModifiers.Control | TerminalModifiers.Alt, isMac: false),
            ["XTJS-0248"] = () => AssertKey("\x1b[1;3D", keyCode: 37, modifiers: TerminalModifiers.Alt, isMac: true),
            ["XTJS-0249"] = () => AssertKey("\x1b[1;3C", keyCode: 39, modifiers: TerminalModifiers.Alt, isMac: true),
            ["XTJS-0250"] = () => AssertKey("\x1b[1;3A", keyCode: 38, modifiers: TerminalModifiers.Alt, isMac: true),
            ["XTJS-0251"] = () => AssertKey("\x1b[1;3B", keyCode: 40, modifiers: TerminalModifiers.Alt, isMac: true),
            ["XTJS-0252"] = () => AssertKey(null, keyCode: 65, modifiers: TerminalModifiers.Alt, isMac: true),
            ["XTJS-0253"] = () => AssertKey("\x1b" + "a", keyCode: 65, modifiers: TerminalModifiers.Alt, isMac: true, macOptionIsMeta: true),
            ["XTJS-0254"] = () => AssertKey("\x1b\r", keyCode: 13, modifiers: TerminalModifiers.Alt, isMac: true, macOptionIsMeta: true)
        };

        string[] digits = ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9"];
        string[] shiftedDigits = [")", "!", "@", "#", "$", "%", "^", "&", "*", "("];
        for (int index = 0; index < digits.Length; index++)
        {
            int caseIndex = index;
            int keyCode = 48 + caseIndex;
            string id = $"XTJS-{213 + caseIndex:0000}";
            assertions[id] = () =>
            {
                AssertKey("\x1b" + digits[caseIndex], keyCode: keyCode, modifiers: TerminalModifiers.Alt);
                AssertKey("\x1b" + shiftedDigits[caseIndex], keyCode: keyCode, modifiers: TerminalModifiers.Alt | TerminalModifiers.Shift);
            };
        }

        (int Id, int KeyCode, string Plain, string Shifted)[] punctuation =
        [
            (223, 186, ";", ":"), (224, 187, "=", "+"), (225, 188, ",", "<"),
            (226, 189, "-", "_"), (227, 190, ".", ">"), (228, 191, "/", "?"),
            (229, 192, "`", "~"), (230, 219, "[", "{"), (231, 220, "\\", "|"),
            (232, 221, "]", "}"), (233, 222, "'", "\"")
        ];
        foreach ((int idNumber, int keyCode, string plain, string shifted) in punctuation)
        {
            assertions[$"XTJS-{idNumber:0000}"] = () =>
            {
                AssertKey("\x1b" + plain, keyCode: keyCode, modifiers: TerminalModifiers.Alt);
                AssertKey("\x1b" + shifted, keyCode: keyCode, modifiers: TerminalModifiers.Alt | TerminalModifiers.Shift);
            };
        }

        Assert.Equal(61, assertions.Count);
        return assertions;
    }

    private static void AssertUnmodifiedKeys()
    {
        (int KeyCode, string Expected)[] cases =
        [
            (8, "\x7f"), (9, "\t"), (13, "\r"), (27, "\x1b"), (33, "\x1b[5~"),
            (34, "\x1b[6~"), (35, "\x1b[F"), (36, "\x1b[H"), (37, "\x1b[D"),
            (38, "\x1b[A"), (39, "\x1b[C"), (40, "\x1b[B"), (45, "\x1b[2~"),
            (46, "\x1b[3~"), (112, "\x1bOP"), (113, "\x1bOQ"), (114, "\x1bOR"),
            (115, "\x1bOS"), (116, "\x1b[15~"), (117, "\x1b[17~"), (118, "\x1b[18~"),
            (119, "\x1b[19~"), (120, "\x1b[20~"), (121, "\x1b[21~"),
            (122, "\x1b[23~"), (123, "\x1b[24~")
        ];
        foreach ((int keyCode, string expected) in cases)
        {
            AssertKey(expected, keyCode: keyCode);
        }
    }

    private static void AssertModifiedFunctionKeys()
    {
        string[] endings = ["1;{0}P", "1;{0}Q", "1;{0}R", "1;{0}S", "15;{0}~", "17;{0}~", "18;{0}~", "19;{0}~", "20;{0}~", "21;{0}~", "23;{0}~", "24;{0}~"];
        (TerminalModifiers Modifier, int Parameter)[] modifiers =
        [
            (TerminalModifiers.Shift, 2),
            (TerminalModifiers.Alt, 3),
            (TerminalModifiers.Control, 5)
        ];
        foreach ((TerminalModifiers modifier, int parameter) in modifiers)
        {
            for (int index = 0; index < endings.Length; index++)
            {
                AssertKey("\x1b[" + string.Format(endings[index], parameter), keyCode: 112 + index, modifiers: modifier);
            }
        }
    }

    private static void AssertMobileArrows()
    {
        (string Key, string Normal, string Application)[] cases =
        [
            ("UIKeyInputUpArrow", "\x1b[A", "\x1bOA"),
            ("UIKeyInputLeftArrow", "\x1b[D", "\x1bOD"),
            ("UIKeyInputRightArrow", "\x1b[C", "\x1bOC"),
            ("UIKeyInputDownArrow", "\x1b[B", "\x1bOB")
        ];
        foreach ((string key, string normal, string application) in cases)
        {
            AssertKey(normal, key: key);
            AssertKey(application, key: key, applicationCursorMode: true);
        }
    }

    private static void AssertLowercaseCharacters()
    {
        AssertKey("a", "a", keyCode: 65);
        AssertKey("-", "-", keyCode: 189);
    }

    private static void AssertUppercaseCharacters()
    {
        AssertKey("A", "A", keyCode: 65, modifiers: TerminalModifiers.Shift);
        AssertKey("!", "!", keyCode: 49, modifiers: TerminalModifiers.Shift);
    }

    private static void AssertAltShiftLetters()
    {
        foreach ((int keyCode, string lower, string upper) in new[] { (65, "a", "A"), (72, "h", "H"), (90, "z", "Z") })
        {
            AssertKey("\x1b" + upper, keyCode: keyCode, modifiers: TerminalModifiers.Alt | TerminalModifiers.Shift);
            AssertKey("\x1b" + lower, keyCode: keyCode, modifiers: TerminalModifiers.Alt);
        }
    }

    private static void AssertKey(
        string? expected,
        string key = "",
        string code = "",
        int keyCode = 0,
        TerminalModifiers modifiers = TerminalModifiers.None,
        bool applicationCursorMode = false,
        bool isMac = false,
        bool macOptionIsMeta = false)
    {
        KeyboardResult result = Keyboard.Evaluate(
            KeyEvent.Create(key, code, keyCode, modifiers),
            applicationCursorMode,
            isMac,
            macOptionIsMeta);
        Assert.Equal(expected, result.Key);
    }
}

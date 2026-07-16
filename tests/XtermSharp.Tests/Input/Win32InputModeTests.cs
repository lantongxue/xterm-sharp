using System.Globalization;
using XtermSharp.Internal.Input;

namespace XtermSharp.Tests.Input;

public sealed class Win32InputModeTests
{
    private static readonly IReadOnlyDictionary<string, Action> Assertions = CreateAssertions();

    public static TheoryData<string> Cases { get; } =
        UpstreamInputRows.ForFile("src/common/input/Win32InputMode.test.ts");

    [Theory]
    [MemberData(nameof(Cases))]
    public void Matches_upstream_win32_input_mode_cases(string upstreamId)
    {
        Assert.True(Assertions.TryGetValue(upstreamId, out Action? assertion), $"Missing assertion for {upstreamId}.");
        assertion!();
    }

    private static IReadOnlyDictionary<string, Action> CreateAssertions()
    {
        var assertions = new Dictionary<string, Action>(StringComparer.Ordinal)
        {
            [Id(708)] = () =>
            {
                KeyboardResult result = Evaluate("a", "KeyA", 65);
                Assert.Equal(KeyboardResultType.SendKey, result.Type);
                Assert.True(result.Cancel);
                Win32Packet packet = Parse(result);
                Assert.Equal((0x41, 97, 1, 1), (packet.VirtualKey, packet.UnicodeCharacter, packet.KeyDown, packet.RepeatCount));
            },
            [Id(709)] = () => Assert.Equal(0, Packet("a", "KeyA", 65, isDown: false).KeyDown),
            [Id(710)] = () => Assert.Equal((0x31, 49), SelectVirtualKeyAndUnicode(Packet("1", "Digit1", 49))),
            [Id(711)] = () => Assert.Equal((0x0D, 13), SelectVirtualKeyAndUnicode(Packet("Enter", "Enter", 13))),
            [Id(712)] = () => Assert.Equal((0x1B, 27), SelectVirtualKeyAndUnicode(Packet("Escape", "Escape", 27))),
            [Id(713)] = () => Assert.Equal((0x20, 32), SelectVirtualKeyAndUnicode(Packet(" ", "Space", 32))),
            [Id(714)] = () => AssertFlag(Packet("A", "KeyA", 65, TerminalModifiers.Shift), Win32ControlKeyState.ShiftPressed),
            [Id(715)] = () => AssertFlag(Packet("a", "KeyA", 65, TerminalModifiers.Control), Win32ControlKeyState.LeftControlPressed),
            [Id(716)] = () =>
            {
                Win32Packet packet = Packet("Control", "ControlRight", 17, TerminalModifiers.Control);
                AssertFlag(packet, Win32ControlKeyState.RightControlPressed);
                AssertFlag(packet, Win32ControlKeyState.EnhancedKey);
            },
            [Id(717)] = () => AssertFlag(Packet("a", "KeyA", 65, TerminalModifiers.Alt), Win32ControlKeyState.LeftAltPressed),
            [Id(718)] = () =>
            {
                Win32Packet packet = Packet("Alt", "AltRight", 18, TerminalModifiers.Alt);
                AssertFlag(packet, Win32ControlKeyState.RightAltPressed);
                AssertFlag(packet, Win32ControlKeyState.EnhancedKey);
            },
            [Id(719)] = () =>
            {
                Win32Packet packet = Packet("A", "KeyA", 65, TerminalModifiers.Shift | TerminalModifiers.Control | TerminalModifiers.Alt);
                AssertFlag(packet, Win32ControlKeyState.ShiftPressed);
                AssertFlag(packet, Win32ControlKeyState.LeftControlPressed);
                AssertFlag(packet, Win32ControlKeyState.LeftAltPressed);
            },
            [Id(720)] = () => Assert.Equal(0x70, Packet("F1", "F1", 112).VirtualKey),
            [Id(721)] = () => Assert.Equal(0x74, Packet("F5", "F5", 116).VirtualKey),
            [Id(722)] = () => Assert.Equal(0x7B, Packet("F12", "F12", 123).VirtualKey),
            [Id(723)] = () =>
            {
                Win32Packet packet = Packet("F1", "F1", 112, TerminalModifiers.Control);
                Assert.Equal(0x70, packet.VirtualKey);
                AssertFlag(packet, Win32ControlKeyState.LeftControlPressed);
            },
            [Id(734)] = () => Assert.Equal((0x09, 9), SelectVirtualKeyAndUnicode(Packet("Tab", "Tab", 9))),
            [Id(735)] = () => Assert.Equal((0x08, 8), SelectVirtualKeyAndUnicode(Packet("Backspace", "Backspace", 8))),
            [Id(736)] = () => Assert.Equal(0x60, Packet("0", "Numpad0", 96).VirtualKey),
            [Id(737)] = () =>
            {
                Win32Packet packet = Packet("Enter", "NumpadEnter", 13);
                Assert.Equal(0x0D, packet.VirtualKey);
                AssertFlag(packet, Win32ControlKeyState.EnhancedKey);
            },
            [Id(738)] = () => Assert.Equal(0x6B, Packet("+", "NumpadAdd", 107).VirtualKey),
            [Id(739)] = () => Assert.Equal(0x6D, Packet("-", "NumpadSubtract", 109).VirtualKey),
            [Id(740)] = () => Assert.Equal(0x6A, Packet("*", "NumpadMultiply", 106).VirtualKey),
            [Id(741)] = () =>
            {
                Win32Packet packet = Packet("/", "NumpadDivide", 111);
                Assert.Equal(0x6F, packet.VirtualKey);
                AssertFlag(packet, Win32ControlKeyState.EnhancedKey);
            },
            [Id(742)] = () => Assert.Equal(0x6E, Packet(".", "NumpadDecimal", 110).VirtualKey),
            [Id(743)] = () => Assert.Equal(97, Packet("a", "KeyA", 65).UnicodeCharacter),
            [Id(744)] = () => Assert.Equal(65, Packet("A", "KeyA", 65, TerminalModifiers.Shift).UnicodeCharacter),
            [Id(745)] = () => Assert.Equal(0, Packet("ArrowUp", "ArrowUp", 38).UnicodeCharacter),
            [Id(746)] = () => Assert.Equal(233, Packet("é", "KeyE", 69).UnicodeCharacter),
            [Id(747)] = () => Assert.Equal(36, Packet("$", "Digit4", 52, TerminalModifiers.Shift).UnicodeCharacter),
            [Id(748)] = () => Assert.Equal(0x01, Packet("a", "KeyA", 65, TerminalModifiers.Control).UnicodeCharacter),
            [Id(749)] = () => Assert.Equal(0x03, Packet("c", "KeyC", 67, TerminalModifiers.Control).UnicodeCharacter),
            [Id(750)] = () => Assert.Equal(0x1A, Packet("z", "KeyZ", 90, TerminalModifiers.Control).UnicodeCharacter),
            [Id(751)] = () => Assert.Equal(0x01, Packet("A", "KeyA", 65, TerminalModifiers.Control | TerminalModifiers.Shift).UnicodeCharacter),
            [Id(752)] = () => Assert.Equal(0x03, Packet("C", "KeyC", 67, TerminalModifiers.Control | TerminalModifiers.Shift).UnicodeCharacter),
            [Id(753)] = () => Assert.Equal(99, Packet("c", "KeyC", 67, TerminalModifiers.Control | TerminalModifiers.Alt).UnicodeCharacter),
            [Id(754)] = () => Assert.Equal(0x1E, Packet("a", "KeyA", 65).ScanCode),
            [Id(755)] = () => Assert.Equal(0x01, Packet("Escape", "Escape", 27).ScanCode),
            [Id(756)] = () =>
            {
                string sequence = Evaluate("a", "KeyA", 65).Key!;
                Assert.StartsWith("\x1b[", sequence, StringComparison.Ordinal);
                Assert.EndsWith("_", sequence, StringComparison.Ordinal);
                Assert.Equal(6, sequence[2..^1].Split(';').Length);
            },
            [Id(757)] = () =>
            {
                Win32Packet packet = Packet("Shift", "ShiftLeft", 16, TerminalModifiers.Shift);
                Assert.Equal(0x10, packet.VirtualKey);
                AssertFlag(packet, Win32ControlKeyState.ShiftPressed);
            },
            [Id(758)] = () =>
            {
                Win32Packet packet = Packet("Shift", "ShiftRight", 16, TerminalModifiers.Shift);
                Assert.Equal(0x10, packet.VirtualKey);
                AssertFlag(packet, Win32ControlKeyState.ShiftPressed);
            },
            [Id(759)] = () =>
            {
                Win32Packet packet = Packet("Control", "ControlLeft", 17, TerminalModifiers.Control);
                Assert.Equal(0x11, packet.VirtualKey);
                AssertFlag(packet, Win32ControlKeyState.LeftControlPressed);
            },
            [Id(760)] = () =>
            {
                Win32Packet packet = Packet("Control", "ControlRight", 17, TerminalModifiers.Control);
                Assert.Equal(0x11, packet.VirtualKey);
                AssertFlag(packet, Win32ControlKeyState.RightControlPressed);
                AssertFlag(packet, Win32ControlKeyState.EnhancedKey);
            },
            [Id(761)] = () =>
            {
                Win32Packet packet = Packet("Alt", "AltLeft", 18, TerminalModifiers.Alt);
                Assert.Equal(0x12, packet.VirtualKey);
                AssertFlag(packet, Win32ControlKeyState.LeftAltPressed);
            },
            [Id(762)] = () =>
            {
                Win32Packet packet = Packet("Alt", "AltRight", 18, TerminalModifiers.Alt);
                Assert.Equal(0x12, packet.VirtualKey);
                AssertFlag(packet, Win32ControlKeyState.RightAltPressed);
                AssertFlag(packet, Win32ControlKeyState.EnhancedKey);
            },
            [Id(763)] = () => Assert.Equal(0, Packet("Shift", "ShiftLeft", 16, isDown: false).KeyDown),
            [Id(764)] = () =>
            {
                Win32Packet packet = Packet(" ", "Space", 32, TerminalModifiers.Control);
                Assert.Equal(0x20, packet.VirtualKey);
                AssertFlag(packet, Win32ControlKeyState.LeftControlPressed);
            },
            [Id(765)] = () =>
            {
                Win32Packet packet = Packet("Enter", "Enter", 13, TerminalModifiers.Shift);
                Assert.Equal(0x0D, packet.VirtualKey);
                AssertFlag(packet, Win32ControlKeyState.ShiftPressed);
            },
            [Id(766)] = () =>
            {
                Win32Packet packet = Packet("Pause", "Pause", 19, TerminalModifiers.Control);
                Assert.Equal(0x13, packet.VirtualKey);
                AssertFlag(packet, Win32ControlKeyState.LeftControlPressed);
            },
            [Id(767)] = () =>
            {
                Win32Packet packet = Packet("/", "Slash", 191, TerminalModifiers.Control | TerminalModifiers.Alt);
                AssertFlag(packet, Win32ControlKeyState.LeftControlPressed);
                AssertFlag(packet, Win32ControlKeyState.LeftAltPressed);
            },
            [Id(768)] = () =>
            {
                Win32Packet packet = Packet("Enter", "Enter", 13, TerminalModifiers.Control);
                Assert.Equal(0x0D, packet.VirtualKey);
                Assert.Equal(0x0A, packet.UnicodeCharacter);
                AssertFlag(packet, Win32ControlKeyState.LeftControlPressed);
            },
            [Id(769)] = () =>
            {
                Win32Packet packet = Packet("Backspace", "Backspace", 8, TerminalModifiers.Control);
                Assert.Equal(0x08, packet.VirtualKey);
                Assert.Equal(0x7F, packet.UnicodeCharacter);
                AssertFlag(packet, Win32ControlKeyState.LeftControlPressed);
            },
            [Id(770)] = () =>
            {
                Win32Packet packet = Packet("Meta", "MetaLeft", 91, TerminalModifiers.Meta);
                Assert.Equal(0x5B, packet.VirtualKey);
                AssertFlag(packet, Win32ControlKeyState.EnhancedKey);
            },
            [Id(771)] = () =>
            {
                Win32Packet packet = Packet("Meta", "MetaRight", 92, TerminalModifiers.Meta);
                Assert.Equal(0x5C, packet.VirtualKey);
                AssertFlag(packet, Win32ControlKeyState.EnhancedKey);
            }
        };

        (string Code, string Key, int KeyCode, int VirtualKey)[] navigation =
        [
            ("ArrowUp", "ArrowUp", 38, 0x26), ("ArrowDown", "ArrowDown", 40, 0x28),
            ("ArrowLeft", "ArrowLeft", 37, 0x25), ("ArrowRight", "ArrowRight", 39, 0x27),
            ("Home", "Home", 36, 0x24), ("End", "End", 35, 0x23),
            ("PageUp", "PageUp", 33, 0x21), ("PageDown", "PageDown", 34, 0x22),
            ("Insert", "Insert", 45, 0x2D), ("Delete", "Delete", 46, 0x2E)
        ];
        for (int index = 0; index < navigation.Length; index++)
        {
            (string code, string key, int keyCode, int virtualKey) = navigation[index];
            assertions[Id(724 + index)] = () =>
            {
                Win32Packet packet = Packet(key, code, keyCode);
                Assert.Equal(virtualKey, packet.VirtualKey);
                AssertFlag(packet, Win32ControlKeyState.EnhancedKey);
            };
        }

        Assert.Equal(64, assertions.Count);
        return assertions;
    }

    private static KeyboardResult Evaluate(
        string key,
        string code,
        int keyCode,
        TerminalModifiers modifiers = TerminalModifiers.None,
        bool isDown = true) =>
        new Win32InputMode().Evaluate(KeyEvent.Create(key, code, keyCode, modifiers), isDown);

    private static Win32Packet Packet(
        string key,
        string code,
        int keyCode,
        TerminalModifiers modifiers = TerminalModifiers.None,
        bool isDown = true) =>
        Parse(Evaluate(key, code, keyCode, modifiers, isDown));

    private static Win32Packet Parse(KeyboardResult result)
    {
        Assert.NotNull(result.Key);
        string sequence = result.Key!;
        Assert.StartsWith("\x1b[", sequence, StringComparison.Ordinal);
        Assert.EndsWith("_", sequence, StringComparison.Ordinal);
        int[] values = sequence[2..^1]
            .Split(';')
            .Select(value => int.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture))
            .ToArray();
        Assert.Equal(6, values.Length);
        return new Win32Packet(values[0], values[1], values[2], values[3], values[4], values[5]);
    }

    private static void AssertFlag(Win32Packet packet, Win32ControlKeyState flag) =>
        Assert.NotEqual(0, packet.ControlState & (int)flag);

    private static (int VirtualKey, int UnicodeCharacter) SelectVirtualKeyAndUnicode(Win32Packet packet) =>
        (packet.VirtualKey, packet.UnicodeCharacter);

    private static string Id(int value) => $"XTJS-{value:0000}";

    private readonly record struct Win32Packet(
        int VirtualKey,
        int ScanCode,
        int UnicodeCharacter,
        int KeyDown,
        int ControlState,
        int RepeatCount);
}

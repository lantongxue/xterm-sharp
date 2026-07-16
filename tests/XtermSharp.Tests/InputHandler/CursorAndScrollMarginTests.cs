using XtermSharp.Internal;
using XtermSharp.Internal.Parser;

namespace XtermSharp.Tests.InputHandler;

public sealed class CursorAndScrollMarginTests
{
    public static TheoryData<string> Cases { get; } = UpstreamInputHandlerRows.ForRange(869, 909);

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Matches_upstream_cursor_and_scroll_margin_cases(string upstreamId)
    {
        switch (upstreamId)
        {
            case "XTJS-0869": await AssertCursorForwardAsync(); break;
            case "XTJS-0870": await AssertCursorBackwardAsync(); break;
            case "XTJS-0871": await AssertCursorDownAsync(); break;
            case "XTJS-0872": await AssertCursorUpAsync(); break;
            case "XTJS-0873": await AssertCursorNextLineAsync(); break;
            case "XTJS-0874": await AssertCursorPreviousLineAsync(); break;
            case "XTJS-0875": await AssertCursorCharacterAbsoluteAsync(); break;
            case "XTJS-0876": await AssertCursorPositionAsync(); break;
            case "XTJS-0877": await AssertOriginCursorPositionAsync(); break;
            case "XTJS-0878": await AssertHorizontalPositionAbsoluteAsync(); break;
            case "XTJS-0879": await AssertHorizontalPositionRelativeAsync(); break;
            case "XTJS-0880": await AssertVerticalPositionAbsoluteAsync(); break;
            case "XTJS-0881": await AssertVerticalPositionRelativeAsync(); break;
            case "XTJS-0882": await AssertClampsAsync("\x1b[C", (9, 9), (1, 0)); break;
            case "XTJS-0883": await AssertClampsAsync("\x1b[D", (8, 9), (0, 0)); break;
            case "XTJS-0884": await AssertClampsAsync("\x1b[B", (9, 9), (0, 1)); break;
            case "XTJS-0885": await AssertClampsAsync("\x1b[A", (9, 8), (0, 0)); break;
            case "XTJS-0886": await AssertClampsAsync("\x1b[E", (0, 9), (0, 1)); break;
            case "XTJS-0887": await AssertClampsAsync("\x1b[F", (0, 8), (0, 0)); break;
            case "XTJS-0888": await AssertClampsAsync("\x1b[5G", (4, 9), (4, 0)); break;
            case "XTJS-0889": await AssertClampsAsync("\x1b[5;5H", (4, 4), (4, 4)); break;
            case "XTJS-0890": await AssertClampsAsync("\x1b[5`", (4, 9), (4, 0)); break;
            case "XTJS-0891": await AssertClampsAsync("\x1b[a", (9, 9), (1, 0)); break;
            case "XTJS-0892": await AssertClampsAsync("\x1b[5d", (9, 4), (0, 4)); break;
            case "XTJS-0893": await AssertClampsAsync("\x1b[e", (9, 9), (0, 1)); break;
            case "XTJS-0894": await AssertClampsAsync("\x1b[P", (9, 9), (0, 0)); break;
            case "XTJS-0895": await AssertDeletesLastCellAsync("P"); break;
            case "XTJS-0896": await AssertClampsAsync("\x1b[X", (9, 9), (0, 0)); break;
            case "XTJS-0897": await AssertDeletesLastCellAsync("X"); break;
            case "XTJS-0898": await AssertClampsAsync("\x1b[@", (9, 9), (0, 0)); break;
            case "XTJS-0899": await AssertDeletesLastCellAsync("@"); break;
            case "XTJS-0900": await AssertDefaultScrollMarginsAsync(); break;
            case "XTJS-0901": await AssertScrollBottomClampsAsync(); break;
            case "XTJS-0902": await AssertInvalidScrollMarginsAreIgnoredAsync(); break;
            case "XTJS-0903": await AssertScrollMarginsHomeCursorAsync(); break;
            case "XTJS-0904": await AssertLinesAsync("0\r\n1\r\n2\r\n3\r\n4\r\n5\r\n6\r\n7\r\n8\r\n9\x1b[2;4r\x1b[2Sm", ["m", "3", "", "", "4", "5", "6", "7", "8", "9"]); break;
            case "XTJS-0905": await AssertLinesAsync("0\r\n1\r\n2\r\n3\r\n4\r\n5\r\n6\r\n7\r\n8\r\n9\x1b[2;4r\x1b[2Tm", ["m", "", "", "1", "4", "5", "6", "7", "8", "9"]); break;
            case "XTJS-0906": await AssertInsertLinesOutOfMarginsAsync(); break;
            case "XTJS-0907": await AssertInsertLinesWithinMarginsAsync(); break;
            case "XTJS-0908": await AssertDeleteLinesOutOfMarginsAsync(); break;
            case "XTJS-0909": await AssertDeleteLinesWithinMarginsAsync(); break;
            default: throw new InvalidOperationException($"Missing assertion for {upstreamId}.");
        }
    }

    private static async Task AssertCursorForwardAsync()
    {
        await using var terminal = NewTerminal();
        await WriteAndAssertCursorAsync(terminal, "\x1b[C", 1, 0);
        await WriteAndAssertCursorAsync(terminal, "\x1b[1C", 2, 0);
        await WriteAndAssertCursorAsync(terminal, "\x1b[4C", 6, 0);
        await WriteAndAssertCursorAsync(terminal, "\x1b[100C", 9, 0);
        await WriteAndAssertCursorAsync(terminal, "\x1b[5;9H\x1b[C", 9, 4);
    }

    private static async Task AssertCursorBackwardAsync()
    {
        await using var terminal = NewTerminal();
        await WriteAndAssertCursorAsync(terminal, "\x1b[D", 0, 0);
        await WriteAndAssertCursorAsync(terminal, "\x1b[1D", 0, 0);
        await WriteAndAssertCursorAsync(terminal, "\x1b[100C\x1b[D", 8, 0);
        await WriteAndAssertCursorAsync(terminal, "\x1b[1D", 7, 0);
        await WriteAndAssertCursorAsync(terminal, "\x1b[4D", 3, 0);
        await WriteAndAssertCursorAsync(terminal, "\x1b[100D", 0, 0);
        await WriteAndAssertCursorAsync(terminal, "\x1b[5;5H\x1b[D", 3, 4);
    }

    private static async Task AssertCursorDownAsync()
    {
        await using var terminal = NewTerminal();
        await WriteAndAssertCursorAsync(terminal, "\x1b[B", 0, 1);
        await WriteAndAssertCursorAsync(terminal, "\x1b[1B", 0, 2);
        await WriteAndAssertCursorAsync(terminal, "\x1b[4B", 0, 6);
        await WriteAndAssertCursorAsync(terminal, "\x1b[100B", 0, 9);
        await WriteAndAssertCursorAsync(terminal, "\x1b[1;9H\x1b[B", 8, 1);
    }

    private static async Task AssertCursorUpAsync()
    {
        await using var terminal = NewTerminal();
        await WriteAndAssertCursorAsync(terminal, "\x1b[A", 0, 0);
        await WriteAndAssertCursorAsync(terminal, "\x1b[1A", 0, 0);
        await WriteAndAssertCursorAsync(terminal, "\x1b[100B\x1b[A", 0, 8);
        await WriteAndAssertCursorAsync(terminal, "\x1b[1A", 0, 7);
        await WriteAndAssertCursorAsync(terminal, "\x1b[4A", 0, 3);
        await WriteAndAssertCursorAsync(terminal, "\x1b[100A", 0, 0);
        await WriteAndAssertCursorAsync(terminal, "\x1b[10;9H\x1b[A", 8, 8);
    }

    private static async Task AssertCursorNextLineAsync()
    {
        await using var terminal = NewTerminal();
        await WriteAndAssertCursorAsync(terminal, "\x1b[E", 0, 1);
        await WriteAndAssertCursorAsync(terminal, "\x1b[1E", 0, 2);
        await WriteAndAssertCursorAsync(terminal, "\x1b[4E", 0, 6);
        await WriteAndAssertCursorAsync(terminal, "\x1b[100E", 0, 9);
        await WriteAndAssertCursorAsync(terminal, "\x1b[1;9H\x1b[E", 0, 1);
    }

    private static async Task AssertCursorPreviousLineAsync()
    {
        await using var terminal = NewTerminal();
        await WriteAndAssertCursorAsync(terminal, "\x1b[F", 0, 0);
        await WriteAndAssertCursorAsync(terminal, "\x1b[1F", 0, 0);
        await WriteAndAssertCursorAsync(terminal, "\x1b[100E\x1b[F", 0, 8);
        await WriteAndAssertCursorAsync(terminal, "\x1b[1F", 0, 7);
        await WriteAndAssertCursorAsync(terminal, "\x1b[4F", 0, 3);
        await WriteAndAssertCursorAsync(terminal, "\x1b[100F", 0, 0);
        await WriteAndAssertCursorAsync(terminal, "\x1b[10;9H\x1b[F", 0, 8);
    }

    private static async Task AssertCursorCharacterAbsoluteAsync()
    {
        await using var terminal = NewTerminal();
        await WriteAndAssertCursorAsync(terminal, "\x1b[G", 0, 0);
        await WriteAndAssertCursorAsync(terminal, "\x1b[1G", 0, 0);
        await WriteAndAssertCursorAsync(terminal, "\x1b[2G", 1, 0);
        await WriteAndAssertCursorAsync(terminal, "\x1b[5G", 4, 0);
        await WriteAndAssertCursorAsync(terminal, "\x1b[100G", 9, 0);
    }

    private static async Task AssertCursorPositionAsync()
    {
        await using var terminal = NewTerminal();
        await WriteAndAssertCursorAsync(terminal, "\x1b[6;6H\x1b[H", 0, 0);
        await WriteAndAssertCursorAsync(terminal, "\x1b[6;6H\x1b[1H", 0, 0);
        await WriteAndAssertCursorAsync(terminal, "\x1b[6;6H\x1b[1;1H", 0, 0);
        await WriteAndAssertCursorAsync(terminal, "\x1b[6;6H\x1b[8H", 0, 7);
        await WriteAndAssertCursorAsync(terminal, "\x1b[6;6H\x1b[;8H", 7, 0);
        await WriteAndAssertCursorAsync(terminal, "\x1b[6;6H\x1b[100;100H", 9, 9);
    }

    private static async Task AssertOriginCursorPositionAsync()
    {
        await using var terminal = NewTerminal();
        await WriteAndAssertCursorAsync(terminal, "\x1b[?6h\x1b[2;3r\x1b[1;1H", 0, 1);
        await terminal.WriteAsync("X");
        Assert.Equal("X", Lines(await terminal.GetSnapshotAsync())[1]);
        await WriteAndAssertCursorAsync(terminal, "\x1b[2;1H", 0, 2);
        await WriteAndAssertCursorAsync(terminal, "\x1b[10;10H", 9, 2);
        await WriteAndAssertCursorAsync(terminal, "\x1b[?6l\x1b[2;1H", 0, 1);
    }

    private static async Task AssertHorizontalPositionAbsoluteAsync()
    {
        await using var terminal = NewTerminal();
        await WriteAndAssertCursorAsync(terminal, "\x1b[`", 0, 0);
        await WriteAndAssertCursorAsync(terminal, "\x1b[1`", 0, 0);
        await WriteAndAssertCursorAsync(terminal, "\x1b[2`", 1, 0);
        await WriteAndAssertCursorAsync(terminal, "\x1b[5`", 4, 0);
        await WriteAndAssertCursorAsync(terminal, "\x1b[100`", 9, 0);
    }

    private static async Task AssertHorizontalPositionRelativeAsync()
    {
        await using var terminal = NewTerminal();
        await WriteAndAssertCursorAsync(terminal, "\x1b[a", 1, 0);
        await WriteAndAssertCursorAsync(terminal, "\x1b[1a", 2, 0);
        await WriteAndAssertCursorAsync(terminal, "\x1b[4a", 6, 0);
        await WriteAndAssertCursorAsync(terminal, "\x1b[100a", 9, 0);
        await WriteAndAssertCursorAsync(terminal, "\x1b[5;9H\x1b[a", 9, 4);
    }

    private static async Task AssertVerticalPositionAbsoluteAsync()
    {
        await using var terminal = NewTerminal();
        await WriteAndAssertCursorAsync(terminal, "\x1b[d", 0, 0);
        await WriteAndAssertCursorAsync(terminal, "\x1b[1d", 0, 0);
        await WriteAndAssertCursorAsync(terminal, "\x1b[2d", 0, 1);
        await WriteAndAssertCursorAsync(terminal, "\x1b[5d", 0, 4);
        await WriteAndAssertCursorAsync(terminal, "\x1b[100d", 0, 9);
        await WriteAndAssertCursorAsync(terminal, "\x1b[5;9H\x1b[d", 8, 0);
    }

    private static async Task AssertVerticalPositionRelativeAsync()
    {
        await using var terminal = NewTerminal();
        await WriteAndAssertCursorAsync(terminal, "\x1b[e", 0, 1);
        await WriteAndAssertCursorAsync(terminal, "\x1b[1e", 0, 2);
        await WriteAndAssertCursorAsync(terminal, "\x1b[4e", 0, 6);
        await WriteAndAssertCursorAsync(terminal, "\x1b[100e", 0, 9);
        await WriteAndAssertCursorAsync(terminal, "\x1b[5;9H\x1b[e", 8, 5);
    }

    private static async Task AssertClampsAsync(string sequence, (int X, int Y) high, (int X, int Y) low)
    {
        TerminalEngine engine = CreateEngine();
        engine.ActiveBuffer.CursorX = 10_000;
        engine.ActiveBuffer.CursorY = 10_000;
        await engine.WriteAsync(sequence);
        Assert.Equal(high, (engine.ActiveBuffer.CursorX, engine.ActiveBuffer.CursorY));

        engine.ActiveBuffer.CursorX = -10_000;
        engine.ActiveBuffer.CursorY = -10_000;
        await engine.WriteAsync(sequence);
        Assert.Equal(low, (engine.ActiveBuffer.CursorX, engine.ActiveBuffer.CursorY));
    }

    private static async Task AssertDeletesLastCellAsync(string final)
    {
        await using var terminal = NewTerminal();
        await terminal.WriteAsync($"0123456789\x1b[{final}");
        Assert.Equal("012345678 ", (await terminal.GetSnapshotAsync()).ActiveBuffer.Lines[0].TranslateToString());
    }

    private static async Task AssertDefaultScrollMarginsAsync()
    {
        TerminalEngine engine = CreateEngine();
        await engine.WriteAsync("\x1b[r");
        Assert.Equal((0, 9), (engine.ActiveBuffer.ScrollTop, engine.ActiveBuffer.ScrollBottom));
        await engine.WriteAsync("\x1b[3;7r");
        Assert.Equal((2, 6), (engine.ActiveBuffer.ScrollTop, engine.ActiveBuffer.ScrollBottom));
        await engine.WriteAsync("\x1b[0;0r");
        Assert.Equal((0, 9), (engine.ActiveBuffer.ScrollTop, engine.ActiveBuffer.ScrollBottom));
    }

    private static async Task AssertScrollBottomClampsAsync()
    {
        TerminalEngine engine = CreateEngine();
        await engine.WriteAsync("\x1b[3;1000r");
        Assert.Equal((2, 9), (engine.ActiveBuffer.ScrollTop, engine.ActiveBuffer.ScrollBottom));
    }

    private static async Task AssertInvalidScrollMarginsAreIgnoredAsync()
    {
        TerminalEngine engine = CreateEngine();
        await engine.WriteAsync("\x1b[7;2r");
        Assert.Equal((0, 9), (engine.ActiveBuffer.ScrollTop, engine.ActiveBuffer.ScrollBottom));
    }

    private static async Task AssertScrollMarginsHomeCursorAsync()
    {
        TerminalEngine engine = CreateEngine();
        engine.ActiveBuffer.CursorX = 10_000;
        engine.ActiveBuffer.CursorY = 10_000;
        await engine.WriteAsync("\x1b[2;7r");
        Assert.Equal((0, 0), (engine.ActiveBuffer.CursorX, engine.ActiveBuffer.CursorY));
    }

    private static async Task AssertInsertLinesOutOfMarginsAsync()
    {
        await using var terminal = NewTerminal();
        await terminal.WriteAsync(NumberedLines + "\x1b[3;6r");
        await WriteAndAssertLinesAsync(terminal, "\x1b[2Lm", ["m", "1", "2", "3", "4", "5", "6", "7", "8", "9"]);
        await WriteAndAssertLinesAsync(terminal, "\x1b[2H\x1b[2Ln", ["m", "n", "2", "3", "4", "5", "6", "7", "8", "9"]);
        await WriteAndAssertLinesAsync(terminal, "\x1b[7H\x1b[2Lo", ["m", "n", "2", "3", "4", "5", "o", "7", "8", "9"]);
        await WriteAndAssertLinesAsync(terminal, "\x1b[8H\x1b[2Lp", ["m", "n", "2", "3", "4", "5", "o", "p", "8", "9"]);
        await WriteAndAssertLinesAsync(terminal, "\x1b[100H\x1b[2Lq", ["m", "n", "2", "3", "4", "5", "o", "p", "8", "q"]);
    }

    private static async Task AssertInsertLinesWithinMarginsAsync()
    {
        await using var terminal = NewTerminal();
        await terminal.WriteAsync(NumberedLines + "\x1b[3;6r");
        await WriteAndAssertLinesAsync(terminal, "\x1b[3H\x1b[2Lm", ["0", "1", "m", "", "2", "3", "6", "7", "8", "9"]);
        await WriteAndAssertLinesAsync(terminal, "\x1b[6H\x1b[2Ln", ["0", "1", "m", "", "2", "n", "6", "7", "8", "9"]);
    }

    private static async Task AssertDeleteLinesOutOfMarginsAsync()
    {
        await using var terminal = NewTerminal();
        await terminal.WriteAsync(NumberedLines + "\x1b[3;6r");
        await WriteAndAssertLinesAsync(terminal, "\x1b[2Mm", ["m", "1", "2", "3", "4", "5", "6", "7", "8", "9"]);
        await WriteAndAssertLinesAsync(terminal, "\x1b[2H\x1b[2Mn", ["m", "n", "2", "3", "4", "5", "6", "7", "8", "9"]);
        await WriteAndAssertLinesAsync(terminal, "\x1b[7H\x1b[2Mo", ["m", "n", "2", "3", "4", "5", "o", "7", "8", "9"]);
        await WriteAndAssertLinesAsync(terminal, "\x1b[8H\x1b[2Mp", ["m", "n", "2", "3", "4", "5", "o", "p", "8", "9"]);
        await WriteAndAssertLinesAsync(terminal, "\x1b[100H\x1b[2Mq", ["m", "n", "2", "3", "4", "5", "o", "p", "8", "q"]);
    }

    private static async Task AssertDeleteLinesWithinMarginsAsync()
    {
        await using var terminal = NewTerminal();
        await terminal.WriteAsync(NumberedLines + "\x1b[3;6r");
        await WriteAndAssertLinesAsync(terminal, "\x1b[6H\x1b[2Mm", ["0", "1", "2", "3", "4", "m", "6", "7", "8", "9"]);
        await WriteAndAssertLinesAsync(terminal, "\x1b[3H\x1b[2Mn", ["0", "1", "n", "m", "", "", "6", "7", "8", "9"]);
    }

    private static async Task AssertLinesAsync(string input, string[] expected)
    {
        await using var terminal = NewTerminal();
        await terminal.WriteAsync(input);
        Assert.Equal(expected, Lines(await terminal.GetSnapshotAsync()));
    }

    private static async Task WriteAndAssertLinesAsync(Terminal terminal, string input, string[] expected)
    {
        await terminal.WriteAsync(input);
        Assert.Equal(expected, Lines(await terminal.GetSnapshotAsync()));
    }

    private static async Task WriteAndAssertCursorAsync(Terminal terminal, string input, int x, int y)
    {
        await terminal.WriteAsync(input);
        TerminalBufferSnapshot buffer = (await terminal.GetSnapshotAsync()).ActiveBuffer;
        Assert.Equal((x, y), (buffer.CursorX, buffer.CursorY));
    }

    private static string[] Lines(TerminalSnapshot snapshot) =>
        snapshot.ActiveBuffer.Lines.Select(line => line.TranslateToString(trimRight: true)).ToArray();

    private static Terminal NewTerminal() => new(new TerminalOptions { Columns = 10, Rows = 10 });

    private static TerminalEngine CreateEngine()
    {
        TerminalOptions options = new TerminalOptions { Columns = 10, Rows = 10 }.ValidateAndClone();
        var unicode = new UnicodeRegistry(options.UnicodeVersion);
        return new TerminalEngine(options, unicode, new EscapeSequenceParser());
    }

    private const string NumberedLines = "0\r\n1\r\n2\r\n3\r\n4\r\n5\r\n6\r\n7\r\n8\r\n9";
}

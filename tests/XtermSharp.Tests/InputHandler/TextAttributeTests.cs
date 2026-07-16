using System.Text;

namespace XtermSharp.Tests.InputHandler;

public sealed class TextAttributeTests
{
    public static TheoryData<string> Cases { get; } = UpstreamInputHandlerRows.ForRange(834, 868);

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Matches_upstream_text_attribute_cases(string upstreamId)
    {
        switch (upstreamId)
        {
            case "XTJS-0834": await AssertFlagToggleAsync("1", "22", CellAttributes.Bold); break;
            case "XTJS-0835": await AssertFlagToggleAsync("2", "22", CellAttributes.Dim); break;
            case "XTJS-0836": await AssertKittyResetAsync("221", CellAttributes.Dim); break;
            case "XTJS-0837": await AssertKittyResetAsync("222", CellAttributes.Bold); break;
            case "XTJS-0838": await AssertFlagToggleAsync("3", "23", CellAttributes.Italic); break;
            case "XTJS-0839": await AssertFlagToggleAsync("4", "24", CellAttributes.Underline); break;
            case "XTJS-0840": await AssertFlagToggleAsync("5", "25", CellAttributes.Blink); break;
            case "XTJS-0841": await AssertFlagToggleAsync("7", "27", CellAttributes.Inverse); break;
            case "XTJS-0842": await AssertFlagToggleAsync("8", "28", CellAttributes.Invisible); break;
            case "XTJS-0843": await AssertFlagToggleAsync("9", "29", CellAttributes.Strikethrough); break;
            case "XTJS-0844": await AssertPalette16Async(); break;
            case "XTJS-0845": await AssertPalette256Async(); break;
            case "XTJS-0846": await AssertRgbAsync(); break;
            case "XTJS-0847": await AssertColorTransitionAsync("38;2;1;2;3;48;2;4;5;6", "38;5;255;48;5;255", TerminalColor.Palette(255)); break;
            case "XTJS-0848": await AssertColorTransitionAsync("38;2;1;2;3;48;2;4;5;6", "37;47", TerminalColor.Palette(7)); break;
            case "XTJS-0849": await AssertColorTransitionAsync("37;47", "38;5;255;48;5;255", TerminalColor.Palette(255)); break;
            case "XTJS-0850": await AssertColorTransitionAsync("38;5;255;48;5;255", "37;47", TerminalColor.Palette(7)); break;
            case "XTJS-0851": await AssertSingleColorAsync("\x1b[38;2;1;2;3m\x1b[38;2;5m", TerminalColor.Rgb(5, 0, 0)); break;
            case "XTJS-0852": await AssertEquivalentColorAsync("38:2::50:100:150", "38;2;50;100;150", TerminalColor.Rgb(50, 100, 150)); break;
            case "XTJS-0853": await AssertEquivalentColorAsync("38:2::50:100:", "38;2;50;100;", TerminalColor.Rgb(50, 100, 0)); break;
            case "XTJS-0854": await AssertEquivalentColorAsync("38:2::50::", "38;2;50;;", TerminalColor.Rgb(50, 0, 0)); break;
            case "XTJS-0855": await AssertEquivalentColorAsync("38:2::::", "38;2;;;", TerminalColor.Rgb(0, 0, 0)); break;
            case "XTJS-0856": await AssertEquivalentColorAsync("38;2::50:100:150", "38;2;50;100;150", TerminalColor.Rgb(50, 100, 150)); break;
            case "XTJS-0857": await AssertEquivalentColorAsync("38;2;50:100:150", "38;2;50;100;150", TerminalColor.Rgb(50, 100, 150)); break;
            case "XTJS-0858": await AssertEquivalentColorAsync("38;2;50;100:150", "38;2;50;100;150", TerminalColor.Rgb(50, 100, 150)); break;
            case "XTJS-0859": await AssertEquivalentColorAsync("38:5:50", "38;5;50", TerminalColor.Palette(50)); break;
            case "XTJS-0860": await AssertEquivalentColorAsync("38:5:", "38;5;", TerminalColor.Palette(0)); break;
            case "XTJS-0861": await AssertEquivalentColorAsync("38;5:50", "38;5;50", TerminalColor.Palette(50)); break;
            case "XTJS-0862": await AssertEquivalentColorAsync("38:2", "38;2", TerminalColor.Rgb(0, 0, 0)); break;
            case "XTJS-0863": await AssertEquivalentColorAsync("38:5", "38;5", TerminalColor.Palette(0)); break;
            case "XTJS-0864": await AssertDecoratedEquivalentAsync("1;38:2::50:100:150;4", "1;38;2;50;100;150;4", TerminalColor.Rgb(50, 100, 150)); break;
            case "XTJS-0865": await AssertDecoratedEquivalentAsync("1;38:2::50:100:;4", "1;38;2;50;100;;4", TerminalColor.Rgb(50, 100, 0)); break;
            case "XTJS-0866": await AssertDecoratedEquivalentAsync("1;38:2::50:100;4", "1;38;2;50;100;;4", TerminalColor.Rgb(50, 100, 0)); break;
            case "XTJS-0867": await AssertDecoratedEquivalentAsync("1;38:2::;4", "1;38;2;;;;4", TerminalColor.Rgb(0, 0, 0)); break;
            case "XTJS-0868": await AssertDecoratedEquivalentAsync("1;38;2::;4", "1;38;2;;;;4", TerminalColor.Rgb(0, 0, 0)); break;
            default: throw new InvalidOperationException($"Missing assertion for {upstreamId}.");
        }
    }

    private static async Task AssertFlagToggleAsync(string set, string reset, CellAttributes attribute)
    {
        TerminalCellSnapshot[] cells = await CellsAsync($"\x1b[{set}mX\x1b[{reset}mY", 2);
        Assert.True(cells[0].Attributes.HasFlag(attribute));
        Assert.False(cells[1].Attributes.HasFlag(attribute));
    }

    private static async Task AssertKittyResetAsync(string reset, CellAttributes remaining)
    {
        TerminalCellSnapshot[] cells = await CellsAsync($"\x1b[1;2mX\x1b[{reset}mY", 2);
        Assert.True(cells[0].Attributes.HasFlag(CellAttributes.Bold));
        Assert.True(cells[0].Attributes.HasFlag(CellAttributes.Dim));
        Assert.Equal(remaining, cells[1].Attributes & (CellAttributes.Bold | CellAttributes.Dim));
    }

    private static async Task AssertPalette16Async()
    {
        var input = new StringBuilder();
        for (int index = 0; index < 8; index++)
        {
            input.Append($"\x1b[{index + 30};{index + 40}mX");
        }
        input.Append("\x1b[39;49mR");

        TerminalCellSnapshot[] cells = await CellsAsync(input.ToString(), 9);
        for (int index = 0; index < 8; index++)
        {
            Assert.Equal(TerminalColor.Palette(index), cells[index].Foreground);
            Assert.Equal(TerminalColor.Palette(index), cells[index].Background);
        }
        Assert.Equal(TerminalColor.Default, cells[8].Foreground);
        Assert.Equal(TerminalColor.Default, cells[8].Background);
    }

    private static async Task AssertPalette256Async()
    {
        var input = new StringBuilder();
        for (int index = 0; index < 256; index++)
        {
            input.Append($"\x1b[38;5;{index};48;5;{index}mX");
        }
        input.Append("\x1b[39;49mR");

        TerminalCellSnapshot[] cells = await CellsAsync(input.ToString(), 257);
        for (int index = 0; index < 256; index++)
        {
            Assert.Equal(TerminalColor.Palette(index), cells[index].Foreground);
            Assert.Equal(TerminalColor.Palette(index), cells[index].Background);
        }
        Assert.Equal(TerminalColor.Default, cells[256].Foreground);
        Assert.Equal(TerminalColor.Default, cells[256].Background);
    }

    private static async Task AssertRgbAsync()
    {
        TerminalCellSnapshot[] cells = await CellsAsync("\x1b[38;2;1;2;3;48;2;4;5;6mX\x1b[39;49mY", 2);
        Assert.Equal(TerminalColor.Rgb(1, 2, 3), cells[0].Foreground);
        Assert.Equal(TerminalColor.Rgb(4, 5, 6), cells[0].Background);
        Assert.Equal(TerminalColor.Default, cells[1].Foreground);
        Assert.Equal(TerminalColor.Default, cells[1].Background);
    }

    private static async Task AssertColorTransitionAsync(string initial, string final, TerminalColor expected)
    {
        TerminalCellSnapshot cell = (await CellsAsync($"\x1b[{initial}m\x1b[{final}mX", 1))[0];
        Assert.Equal(expected, cell.Foreground);
        Assert.Equal(expected, cell.Background);
    }

    private static async Task AssertSingleColorAsync(string sequence, TerminalColor expected)
    {
        TerminalCellSnapshot cell = (await CellsAsync(sequence + "X", 1))[0];
        Assert.Equal(expected, cell.Foreground);
    }

    private static async Task AssertEquivalentColorAsync(string candidate, string reference, TerminalColor expected)
    {
        TerminalCellSnapshot[] cells = await CellsAsync($"\x1b[{candidate}mX\x1b[0m\x1b[{reference}mY", 2);
        Assert.Equal(expected, cells[0].Foreground);
        Assert.Equal(cells[1].Foreground, cells[0].Foreground);
    }

    private static async Task AssertDecoratedEquivalentAsync(string candidate, string reference, TerminalColor expected)
    {
        TerminalCellSnapshot[] cells = await CellsAsync($"\x1b[{candidate}mX\x1b[0m\x1b[{reference}mY", 2);
        foreach (TerminalCellSnapshot cell in cells)
        {
            Assert.True(cell.Attributes.HasFlag(CellAttributes.Bold));
            Assert.True(cell.Attributes.HasFlag(CellAttributes.Underline));
            Assert.Equal(expected, cell.Foreground);
        }
        Assert.Equal(cells[1].Foreground, cells[0].Foreground);
    }

    private static async Task<TerminalCellSnapshot[]> CellsAsync(string input, int count)
    {
        await using var terminal = new Terminal(new TerminalOptions { Columns = Math.Max(count, 2), Rows = 2 });
        await terminal.WriteAsync(input);
        TerminalLineSnapshot line = (await terminal.GetSnapshotAsync()).ActiveBuffer.Lines[0];
        return line.Cells.Take(count).ToArray();
    }
}

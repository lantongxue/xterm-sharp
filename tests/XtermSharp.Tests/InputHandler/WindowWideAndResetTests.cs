using XtermSharp.Internal;
using XtermSharp.Internal.Parser;

namespace XtermSharp.Tests.InputHandler;

public sealed class WindowWideAndResetTests
{
    private const string XtermVersionReport = "\x1bP>|xterm.js(6.0.0)\x1b\\";
    private const string TtyBackspace = "\b \b";

    public static TheoryData<string> Cases { get; } = UpstreamInputHandlerRows.ForRange(910, 951);

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Matches_upstream_window_wide_attribute_and_reset_cases(string upstreamId)
    {
        switch (upstreamId)
        {
            case "XTJS-0910": await AssertWindowReportsDisabledByDefaultAsync(); break;
            case "XTJS-0911": await AssertRendererWindowReportRemainsHeadlessAsync(getWindowPixels: true); break;
            case "XTJS-0912": await AssertRendererWindowReportRemainsHeadlessAsync(getWindowPixels: false); break;
            case "XTJS-0913": await AssertWindowSizeCharactersReportAsync(); break;
            case "XTJS-0914": await AssertTitleStacksAsync(0); break;
            case "XTJS-0915": await AssertTitleStacksAsync(1); break;
            case "XTJS-0916": await AssertTitleStacksAsync(2); break;
            case "XTJS-0917": await AssertDecColumnModeIsGatedAsync(); break;
            case "XTJS-0918": await AssertXtVersionAsync("\x1b[>q", reports: true); break;
            case "XTJS-0919": await AssertXtVersionAsync("\x1b[>0q", reports: true); break;
            case "XTJS-0920": await AssertXtVersionAsync("\x1b[>1q", reports: false); break;
            case "XTJS-0921": await AssertWidePrintCleanupAsync(); break;
            case "XTJS-0922": await AssertWideEraseInLineCleanupAsync(); break;
            case "XTJS-0923": await AssertWideInsertCharactersCleanupAsync(); break;
            case "XTJS-0924": await AssertWideDeleteCharactersCleanupAsync(); break;
            case "XTJS-0925": await AssertWideEraseCharactersCleanupAsync(); break;
            case "XTJS-0926": await AssertDefaultBackspaceCannotDeleteLastCellAsync(); break;
            case "XTJS-0927": await AssertDefaultBackspaceCannotReachPreviousLineAsync(); break;
            case "XTJS-0928": await AssertReverseBackspaceDeletesLastCellAsync(); break;
            case "XTJS-0929": await AssertReverseBackspaceReachesWrappedLineAsync(); break;
            case "XTJS-0930": await AssertReverseBackspaceClearsWrappedFlagAsync(); break;
            case "XTJS-0931": await AssertReverseBackspaceStopsAtHardNewlineAsync(); break;
            case "XTJS-0932": await AssertReverseBackspaceHandlesWideCharactersAsync(); break;
            case "XTJS-0933": await AssertSgrResetClearsAttributesAsync(); break;
            case "XTJS-0934": await AssertSgrResetPreservesHyperlinkAsync(); break;
            case "XTJS-0935": await AssertUnderlineToggleAsync("4", TerminalUnderlineStyle.Single); break;
            case "XTJS-0936": await AssertUnderlineToggleAsync("21", TerminalUnderlineStyle.Double); break;
            case "XTJS-0937": await AssertExtendedUnderlineToggleAsync(1, TerminalUnderlineStyle.Single); break;
            case "XTJS-0938": await AssertExtendedUnderlineToggleAsync(2, TerminalUnderlineStyle.Double); break;
            case "XTJS-0939": await AssertExtendedUnderlineToggleAsync(3, TerminalUnderlineStyle.Curly); break;
            case "XTJS-0940": await AssertExtendedUnderlineToggleAsync(4, TerminalUnderlineStyle.Dotted); break;
            case "XTJS-0941": await AssertExtendedUnderlineToggleAsync(5, TerminalUnderlineStyle.Dashed); break;
            case "XTJS-0942": await AssertPlainUnderlineReplacesExtendedStyleAsync(); break;
            case "XTJS-0943": await AssertUnderlineColorDefaultsToForegroundAsync(); break;
            case "XTJS-0944": await AssertUnderlineColorsAsync(); break;
            case "XTJS-0945": await AssertUnderlineColorPersistenceAsync(); break;
            case "XTJS-0946": await AssertSoftResetInsertModeAsync(); break;
            case "XTJS-0947": await AssertSoftResetCursorVisibilityAsync(); break;
            case "XTJS-0948": await AssertSoftResetScrollMarginsAsync(); break;
            case "XTJS-0949": await AssertSoftResetTextAttributesAsync(); break;
            case "XTJS-0950": await AssertSoftResetSavedCursorAsync(); break;
            case "XTJS-0951": await AssertSoftResetOriginModeAsync(); break;
            default: throw new InvalidOperationException($"Missing assertion for {upstreamId}.");
        }
    }

    private static async Task AssertWindowReportsDisabledByDefaultAsync()
    {
        await using var terminal = NewTerminal(10, 10);
        List<string> reports = CaptureData(terminal);
        await terminal.WriteAsync("\x1b[14t\x1b[16t\x1b[18t\x1b[20t\x1b[21t");
        Assert.Empty(reports);
    }

    private static async Task AssertRendererWindowReportRemainsHeadlessAsync(bool getWindowPixels)
    {
        var windowOptions = new TerminalWindowOptions
        {
            GetWindowSizePixels = getWindowPixels,
            GetCellSizePixels = !getWindowPixels
        };
        await using var terminal = NewTerminal(10, 10, windowOptions: windowOptions);
        List<string> reports = CaptureData(terminal);
        await terminal.WriteAsync(getWindowPixels ? "\x1b[14t" : "\x1b[16t");
        Assert.Empty(reports);
    }

    private static async Task AssertWindowSizeCharactersReportAsync()
    {
        await using var terminal = NewTerminal(
            10,
            10,
            windowOptions: new TerminalWindowOptions { GetWindowSizeCharacters = true });
        List<string> reports = CaptureData(terminal);
        await terminal.WriteAsync("\x1b[18t");
        await terminal.ResizeAsync(50, 20);
        await terminal.WriteAsync("\x1b[18t");
        Assert.Equal(["\x1b[8;10;10t", "\x1b[8;20;50t"], reports);
    }

    private static async Task AssertTitleStacksAsync(int selector)
    {
        using TerminalEngine engine = CreateEngine(
            10,
            10,
            new TerminalWindowOptions { PushTitle = true, PopTitle = true });
        string suffix = selector == 0 ? string.Empty : $";{selector}";
        for (int value = 1; value <= 3; value++)
        {
            await engine.WriteAsync($"\x1b]0;{value}\a\x1b[22{suffix}t");
        }

        Assert.Equal(["1", "2", "3"], ConsumeTitles(engine));
        Assert.Equal(selector is 0 or 2 ? ["1", "2", "3"] : [], engine.WindowTitleStack);
        Assert.Equal(selector is 0 or 1 ? ["1", "2", "3"] : [], engine.IconNameStack);

        await engine.WriteAsync($"\x1b[23{suffix}t\x1b[23{suffix}t\x1b[23{suffix}t\x1b[23{suffix}t");
        Assert.Empty(engine.WindowTitleStack);
        Assert.Empty(engine.IconNameStack);
        Assert.Equal(selector is 0 or 2 ? ["3", "2", "1"] : [], ConsumeTitles(engine));
    }

    private static async Task AssertDecColumnModeIsGatedAsync()
    {
        await using var terminal = NewTerminal(10, 10);
        await terminal.WriteAsync("\x1b[?3l\x1b[?3h");
        Assert.Equal(10, terminal.Columns);

        await terminal.UpdateOptionsAsync(new TerminalOptionsUpdate
        {
            WindowOptions = new TerminalWindowOptions { SetWindowLines = true }
        });
        await terminal.WriteAsync("\x1b[?3l");
        Assert.Equal(80, terminal.Columns);
        await terminal.WriteAsync("\x1b[?3h");
        Assert.Equal(132, terminal.Columns);
    }

    private static async Task AssertXtVersionAsync(string query, bool reports)
    {
        await using var terminal = NewTerminal();
        List<string> responses = CaptureData(terminal);
        await terminal.WriteAsync(query);
        if (reports)
        {
            Assert.Equal([XtermVersionReport], responses);
        }
        else
        {
            Assert.Empty(responses);
        }
    }

    private static async Task AssertWidePrintCleanupAsync()
    {
        await using Terminal terminal = await NewWideTerminalAsync();
        await terminal.WriteAsync("\x1b[H#");
        await AssertLinesAsync(terminal, ["# ￥￥￥￥", "￥￥￥￥￥", "￥￥￥￥￥", "￥￥￥￥￥", ""]);
        await terminal.WriteAsync("\x1b[1;6H######");
        await AssertLinesAsync(terminal, ["# ￥ #####", "# ￥￥￥￥", "￥￥￥￥￥", "￥￥￥￥￥", ""]);
        await terminal.WriteAsync("#");
        await AssertLinesAsync(terminal, ["# ￥ #####", "##￥￥￥￥", "￥￥￥￥￥", "￥￥￥￥￥", ""]);
        await terminal.WriteAsync("#");
        await AssertLinesAsync(terminal, ["# ￥ #####", "### ￥￥￥", "￥￥￥￥￥", "￥￥￥￥￥", ""]);
        await terminal.WriteAsync("\x1b[3;9H#");
        await AssertLinesAsync(terminal, ["# ￥ #####", "### ￥￥￥", "￥￥￥￥#", "￥￥￥￥￥", ""]);
        await terminal.WriteAsync("#");
        await AssertLinesAsync(terminal, ["# ￥ #####", "### ￥￥￥", "￥￥￥￥##", "￥￥￥￥￥", ""]);
        await terminal.WriteAsync("#");
        await AssertLinesAsync(terminal, ["# ￥ #####", "### ￥￥￥", "￥￥￥￥##", "# ￥￥￥￥", ""]);
        await terminal.WriteAsync("\x1b[4;10H#");
        await AssertLinesAsync(terminal, ["# ￥ #####", "### ￥￥￥", "￥￥￥￥##", "# ￥￥￥ #", ""]);
    }

    private static async Task AssertWideEraseInLineCleanupAsync()
    {
        await using Terminal terminal = await NewWideTerminalAsync();
        await terminal.WriteAsync("\x1b[1;6H\x1b[K#");
        await AssertLinesAsync(terminal, ["￥￥ #", "￥￥￥￥￥", "￥￥￥￥￥", "￥￥￥￥￥", ""]);
        await terminal.WriteAsync("\x1b[2;5H\x1b[1K");
        await AssertLinesAsync(terminal, ["￥￥ #", "      ￥￥", "￥￥￥￥￥", "￥￥￥￥￥", ""]);
        await terminal.WriteAsync("\x1b[3;6H\x1b[1K");
        await AssertLinesAsync(terminal, ["￥￥ #", "      ￥￥", "      ￥￥", "￥￥￥￥￥", ""]);
    }

    private static async Task AssertWideInsertCharactersCleanupAsync()
    {
        await using Terminal terminal = await NewWideTerminalAsync();
        await terminal.WriteAsync("\x1b[1;6H\x1b[@");
        await AssertLinesAsync(terminal, ["￥￥   ￥", "￥￥￥￥￥", "￥￥￥￥￥", "￥￥￥￥￥", ""]);
        await terminal.WriteAsync("\x1b[2;4H\x1b[2@");
        await AssertLinesAsync(terminal, ["￥￥   ￥", "￥    ￥￥", "￥￥￥￥￥", "￥￥￥￥￥", ""]);
        await terminal.WriteAsync("\x1b[3;4H\x1b[3@");
        await AssertLinesAsync(terminal, ["￥￥   ￥", "￥    ￥￥", "￥     ￥", "￥￥￥￥￥", ""]);
        await terminal.WriteAsync("\x1b[4;4H\x1b[4@");
        await AssertLinesAsync(terminal, ["￥￥   ￥", "￥    ￥￥", "￥     ￥", "￥      ￥", ""]);
    }

    private static async Task AssertWideDeleteCharactersCleanupAsync()
    {
        await using Terminal terminal = await NewWideTerminalAsync();
        await terminal.WriteAsync("\x1b[1;6H\x1b[P");
        await AssertLinesAsync(terminal, ["￥￥ ￥￥", "￥￥￥￥￥", "￥￥￥￥￥", "￥￥￥￥￥", ""]);
        await terminal.WriteAsync("\x1b[2;6H\x1b[2P");
        await AssertLinesAsync(terminal, ["￥￥ ￥￥", "￥￥  ￥", "￥￥￥￥￥", "￥￥￥￥￥", ""]);
        await terminal.WriteAsync("\x1b[3;6H\x1b[3P");
        await AssertLinesAsync(terminal, ["￥￥ ￥￥", "￥￥  ￥", "￥￥ ￥", "￥￥￥￥￥", ""]);
    }

    private static async Task AssertWideEraseCharactersCleanupAsync()
    {
        await using Terminal terminal = await NewWideTerminalAsync();
        await terminal.WriteAsync("\x1b[1;6H\x1b[X");
        await AssertLinesAsync(terminal, ["￥￥  ￥￥", "￥￥￥￥￥", "￥￥￥￥￥", "￥￥￥￥￥", ""]);
        await terminal.WriteAsync("\x1b[2;6H\x1b[2X");
        await AssertLinesAsync(terminal, ["￥￥  ￥￥", "￥￥    ￥", "￥￥￥￥￥", "￥￥￥￥￥", ""]);
        await terminal.WriteAsync("\x1b[3;6H\x1b[3X");
        await AssertLinesAsync(terminal, ["￥￥  ￥￥", "￥￥    ￥", "￥￥    ￥", "￥￥￥￥￥", ""]);
    }

    private static async Task AssertDefaultBackspaceCannotDeleteLastCellAsync()
    {
        await using var terminal = NewTerminal(5, 5, scrollback: 1);
        await terminal.WriteAsync("12345" + TtyBackspace);
        await AssertLinesAsync(terminal, ["123 5"], 1);
        await terminal.WriteAsync(string.Concat(Enumerable.Repeat(TtyBackspace, 10)));
        await AssertLinesAsync(terminal, ["    5"], 1);
    }

    private static async Task AssertDefaultBackspaceCannotReachPreviousLineAsync()
    {
        await using var terminal = NewTerminal(5, 5, scrollback: 1);
        await terminal.WriteAsync("1234512345" + TtyBackspace);
        await AssertLinesAsync(terminal, ["12345", "123 5"], 2);
        await terminal.WriteAsync(string.Concat(Enumerable.Repeat(TtyBackspace, 10)));
        await AssertLinesAsync(terminal, ["12345", "    5"], 2);
    }

    private static async Task AssertReverseBackspaceDeletesLastCellAsync()
    {
        await using var terminal = NewTerminal(5, 5, scrollback: 1);
        await terminal.WriteAsync("\x1b[?45h12345" + TtyBackspace);
        await AssertLinesAsync(terminal, ["1234 "], 1);
        await terminal.WriteAsync(string.Concat(Enumerable.Repeat(TtyBackspace, 7)));
        await AssertLinesAsync(terminal, ["     "], 1);
    }

    private static async Task AssertReverseBackspaceReachesWrappedLineAsync()
    {
        await using var terminal = NewTerminal(5, 5, scrollback: 1);
        await terminal.WriteAsync("\x1b[?45h1234512345" + TtyBackspace);
        await AssertLinesAsync(terminal, ["12345", "1234 "], 2);
        await terminal.WriteAsync(string.Concat(Enumerable.Repeat(TtyBackspace, 7)));
        await AssertLinesAsync(terminal, ["12   ", "     "], 2);
    }

    private static async Task AssertReverseBackspaceClearsWrappedFlagAsync()
    {
        await using var terminal = NewTerminal(5, 5, scrollback: 1);
        await terminal.WriteAsync("\x1b[?45h1234512345");
        Assert.True((await terminal.GetSnapshotAsync()).ActiveBuffer.Lines[1].IsWrapped);
        await terminal.WriteAsync(string.Concat(Enumerable.Repeat(TtyBackspace, 7)));
        Assert.False((await terminal.GetSnapshotAsync()).ActiveBuffer.Lines[1].IsWrapped);
    }

    private static async Task AssertReverseBackspaceStopsAtHardNewlineAsync()
    {
        await using var terminal = NewTerminal(5, 5, scrollback: 1);
        await terminal.WriteAsync("\x1b[?45h12345\r\n1234512345");
        await terminal.WriteAsync(string.Concat(Enumerable.Repeat(TtyBackspace, 50)));
        TerminalSnapshot snapshot = await terminal.GetSnapshotAsync();
        Assert.Equal(["12345", "     ", "     "], Lines(snapshot, 3));
        Assert.Equal((0, 1), (snapshot.ActiveBuffer.CursorX, snapshot.ActiveBuffer.CursorY));
    }

    private static async Task AssertReverseBackspaceHandlesWideCharactersAsync()
    {
        await using var terminal = NewTerminal(5, 5, scrollback: 1);
        await terminal.WriteAsync("\x1b[?45h￥￥￥");
        await AssertLinesAsync(terminal, ["￥￥", "￥"], 2);

        int[] expectedCursorColumns = [1, 0, 3, 2, 1, 0];
        string[][] expectedLines =
        [
            ["￥￥", "  "],
            ["￥￥", "  "],
            ["￥  ", "  "],
            ["￥  ", "  "],
            ["    ", "  "],
            ["    ", "  "]
        ];
        for (int index = 0; index < expectedCursorColumns.Length; index++)
        {
            await terminal.WriteAsync(TtyBackspace);
            TerminalSnapshot snapshot = await terminal.GetSnapshotAsync();
            Assert.Equal(expectedLines[index], Lines(snapshot, 2));
            Assert.Equal(expectedCursorColumns[index], snapshot.ActiveBuffer.CursorX);
        }
    }

    private static async Task AssertSgrResetClearsAttributesAsync()
    {
        TerminalCellSnapshot[] cells = await CellsAsync("\x1b[30;40;4mA\x1b[mB", 2);
        Assert.NotEqual(TerminalColor.Default, cells[0].Foreground);
        Assert.NotEqual(TerminalColor.Default, cells[0].Background);
        Assert.Equal(TerminalUnderlineStyle.Single, cells[0].UnderlineStyle);
        Assert.Equal(TerminalColor.Default, cells[1].Foreground);
        Assert.Equal(TerminalColor.Default, cells[1].Background);
        Assert.Equal(TerminalUnderlineStyle.None, cells[1].UnderlineStyle);
        Assert.Equal(TerminalColor.Default, cells[1].UnderlineColor);
        Assert.Equal(0, cells[1].HyperlinkId);
    }

    private static async Task AssertSgrResetPreservesHyperlinkAsync()
    {
        TerminalCellSnapshot[] cells = await CellsAsync(
            "\x1b[30;40;4m\x1b]8;;http://example.com\x1b\\A\x1b[mB",
            2);
        Assert.NotEqual(0, cells[0].HyperlinkId);
        Assert.Equal(cells[0].HyperlinkId, cells[1].HyperlinkId);
        Assert.Equal(TerminalColor.Default, cells[1].Foreground);
        Assert.Equal(TerminalColor.Default, cells[1].Background);
        Assert.Equal(TerminalUnderlineStyle.None, cells[1].UnderlineStyle);
        Assert.Equal(TerminalColor.Default, cells[1].UnderlineColor);
    }

    private static async Task AssertUnderlineToggleAsync(string set, TerminalUnderlineStyle expected)
    {
        TerminalCellSnapshot[] cells = await CellsAsync($"\x1b[{set}mA\x1b[24mB", 2);
        Assert.Equal(expected, cells[0].UnderlineStyle);
        Assert.True(cells[0].Attributes.HasFlag(CellAttributes.Underline));
        Assert.Equal(TerminalUnderlineStyle.None, cells[1].UnderlineStyle);
        Assert.False(cells[1].Attributes.HasFlag(CellAttributes.Underline));
    }

    private static async Task AssertExtendedUnderlineToggleAsync(int style, TerminalUnderlineStyle expected)
    {
        TerminalCellSnapshot[] cells = await CellsAsync(
            $"\x1b[4:{style}mA\x1b[4:0mB\x1b[4:{style}mC\x1b[24mD",
            4);
        Assert.Equal(expected, cells[0].UnderlineStyle);
        Assert.Equal(TerminalUnderlineStyle.None, cells[1].UnderlineStyle);
        Assert.Equal(expected, cells[2].UnderlineStyle);
        Assert.Equal(TerminalUnderlineStyle.None, cells[3].UnderlineStyle);
    }

    private static async Task AssertPlainUnderlineReplacesExtendedStyleAsync()
    {
        TerminalCellSnapshot[] cells = await CellsAsync("\x1b[4:5mA\x1b[4mB", 2);
        Assert.Equal(TerminalUnderlineStyle.Dashed, cells[0].UnderlineStyle);
        Assert.Equal(TerminalUnderlineStyle.Single, cells[1].UnderlineStyle);
    }

    private static async Task AssertUnderlineColorDefaultsToForegroundAsync()
    {
        TerminalCellSnapshot[] cells = await CellsAsync(
            "\x1b[4mA\x1b[30mB\x1b[38;510mC\x1b[38;2;1;2;3mD",
            4);
        TerminalColor[] expected =
        [
            TerminalColor.Default,
            TerminalColor.Palette(0),
            TerminalColor.Palette(0),
            TerminalColor.Rgb(1, 2, 3)
        ];
        for (int index = 0; index < cells.Length; index++)
        {
            Assert.Equal(expected[index], cells[index].Foreground);
            Assert.Equal(expected[index], EffectiveUnderlineColor(cells[index]));
        }
    }

    private static async Task AssertUnderlineColorsAsync()
    {
        TerminalCellSnapshot[] cells = await CellsAsync(
            "\x1b[4;58;5;123mA\x1b[58;2::1:2:3mB",
            2);
        Assert.Equal(TerminalColor.Palette(123), cells[0].UnderlineColor);
        Assert.Equal(TerminalColor.Rgb(1, 2, 3), cells[1].UnderlineColor);
        Assert.All(cells, cell => Assert.Equal(TerminalUnderlineStyle.Single, cell.UnderlineStyle));
    }

    private static async Task AssertUnderlineColorPersistenceAsync()
    {
        using TerminalEngine engine = CreateEngine(10, 5);
        await engine.WriteAsync("\x1b[4m\x1b[58;5;123mab");
        BufferLine line = engine.ActiveBuffer.GetViewportLine(0);
        CellData first = line.GetCell(0);
        CellData second = line.GetCell(1);
        Assert.Equal(TerminalColor.Palette(123), first.Style.UnderlineColor);
        Assert.Same(first.Extended, second.Extended);

        await engine.WriteAsync("\x1b[4:0ma");
        CellData plain = line.GetCell(2);
        Assert.Equal(TerminalUnderlineStyle.None, plain.Style.UnderlineStyle);
        Assert.Equal(TerminalColor.Default, plain.Style.UnderlineColor);
        Assert.Null(plain.Extended);

        await engine.WriteAsync("\x1b[4m\x1b[58;2::1:2:3ma\x1b[24m");
        CellData rgb = line.GetCell(3);
        Assert.Equal(TerminalColor.Rgb(1, 2, 3), rgb.Style.UnderlineColor);
        Assert.NotSame(second.Extended, rgb.Extended);
    }

    private static async Task AssertSoftResetInsertModeAsync()
    {
        await using Terminal terminal = await NewSoftResetTerminalAsync();
        await terminal.WriteAsync("\x1b[4h");
        Assert.True((await terminal.GetSnapshotAsync()).Modes.Insert);
        await terminal.WriteAsync("\x1b[!p");
        Assert.False((await terminal.GetSnapshotAsync()).Modes.Insert);
    }

    private static async Task AssertSoftResetCursorVisibilityAsync()
    {
        await using Terminal terminal = await NewSoftResetTerminalAsync();
        await terminal.WriteAsync("\x1b[?25l");
        Assert.False((await terminal.GetSnapshotAsync()).Modes.ShowCursor);
        await terminal.WriteAsync("\x1b[!p");
        Assert.True((await terminal.GetSnapshotAsync()).Modes.ShowCursor);
    }

    private static async Task AssertSoftResetScrollMarginsAsync()
    {
        using TerminalEngine engine = await NewSoftResetEngineAsync();
        await engine.WriteAsync("\x1b[2;4r");
        Assert.Equal((1, 3), (engine.ActiveBuffer.ScrollTop, engine.ActiveBuffer.ScrollBottom));
        await engine.WriteAsync("\x1b[!p");
        Assert.Equal((0, 4), (engine.ActiveBuffer.ScrollTop, engine.ActiveBuffer.ScrollBottom));
    }

    private static async Task AssertSoftResetTextAttributesAsync()
    {
        await using Terminal terminal = await NewSoftResetTerminalAsync();
        await terminal.WriteAsync("\x1b[1;2;32;43mA\x1b[!pB");
        TerminalCellSnapshot[] cells = (await terminal.GetSnapshotAsync()).ActiveBuffer.Lines[1].Cells
            .Skip(4)
            .Take(2)
            .ToArray();
        Assert.True(cells[0].Attributes.HasFlag(CellAttributes.Bold));
        Assert.Equal(TerminalColor.Default, cells[1].Foreground);
        Assert.Equal(TerminalColor.Default, cells[1].Background);
        Assert.Equal(CellAttributes.None, cells[1].Attributes);
    }

    private static async Task AssertSoftResetSavedCursorAsync()
    {
        using TerminalEngine engine = await NewSoftResetEngineAsync();
        await engine.WriteAsync("\u001b7");
        Assert.Equal((4, 1), (engine.ActiveBuffer.SavedCursorX, engine.ActiveBuffer.SavedCursorY));
        await engine.WriteAsync("\x1b[!p");
        Assert.Equal((0, 0), (engine.ActiveBuffer.SavedCursorX, engine.ActiveBuffer.SavedCursorY));
    }

    private static async Task AssertSoftResetOriginModeAsync()
    {
        await using Terminal terminal = await NewSoftResetTerminalAsync();
        await terminal.WriteAsync("\x1b[?6h");
        Assert.True((await terminal.GetSnapshotAsync()).Modes.Origin);
        await terminal.WriteAsync("\x1b[!p");
        Assert.False((await terminal.GetSnapshotAsync()).Modes.Origin);
    }

    private static Terminal NewTerminal(
        int columns = 10,
        int rows = 5,
        int scrollback = 0,
        TerminalWindowOptions? windowOptions = null) =>
        new(new TerminalOptions
        {
            Columns = columns,
            Rows = rows,
            Scrollback = scrollback,
            WindowOptions = windowOptions ?? new TerminalWindowOptions()
        });

    private static TerminalEngine CreateEngine(
        int columns,
        int rows,
        TerminalWindowOptions? windowOptions = null)
    {
        TerminalOptions options = new TerminalOptions
        {
            Columns = columns,
            Rows = rows,
            Scrollback = 1,
            WindowOptions = windowOptions ?? new TerminalWindowOptions()
        }.ValidateAndClone();
        return new TerminalEngine(
            options,
            new UnicodeRegistry(options.UnicodeVersion),
            new EscapeSequenceParser());
    }

    private static async Task<Terminal> NewWideTerminalAsync()
    {
        Terminal terminal = NewTerminal(10, 5, scrollback: 1);
        await terminal.WriteAsync("￥￥￥￥￥￥￥￥￥￥￥￥￥￥￥￥￥￥￥￥");
        return terminal;
    }

    private static async Task<Terminal> NewSoftResetTerminalAsync()
    {
        Terminal terminal = NewTerminal(10, 5, scrollback: 1);
        await terminal.WriteAsync("01234567890123");
        return terminal;
    }

    private static async Task<TerminalEngine> NewSoftResetEngineAsync()
    {
        TerminalEngine engine = CreateEngine(10, 5);
        await engine.WriteAsync("01234567890123");
        return engine;
    }

    private static async Task<TerminalCellSnapshot[]> CellsAsync(string input, int count)
    {
        await using var terminal = NewTerminal(Math.Max(10, count), 5);
        await terminal.WriteAsync(input);
        return (await terminal.GetSnapshotAsync()).ActiveBuffer.Lines[0].Cells.Take(count).ToArray();
    }

    private static List<string> CaptureData(Terminal terminal)
    {
        var result = new List<string>();
        terminal.Data += (_, args) => result.Add(args.Data);
        return result;
    }

    private static string[] ConsumeTitles(TerminalEngine engine) =>
        engine.ConsumeEvents(includeWriteParsed: false)
            .Where(value => value.Kind == EngineEventKind.TitleChanged)
            .Select(value => value.Text ?? string.Empty)
            .ToArray();

    private static TerminalColor EffectiveUnderlineColor(TerminalCellSnapshot cell) =>
        cell.UnderlineColor.Mode == TerminalColorMode.Default ? cell.Foreground : cell.UnderlineColor;

    private static string[] Lines(TerminalSnapshot snapshot, int? limit = null) =>
        snapshot.ActiveBuffer.Lines
            .Take(limit ?? snapshot.ActiveBuffer.Lines.Length)
            .Select(line => line.TranslateToString(trimRight: true))
            .ToArray();

    private static async Task AssertLinesAsync(Terminal terminal, string[] expected, int? limit = null) =>
        Assert.Equal(expected, Lines(await terminal.GetSnapshotAsync(), limit));
}

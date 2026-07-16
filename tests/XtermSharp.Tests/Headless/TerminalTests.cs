using System.Text;

namespace XtermSharp.Tests.Headless;

public sealed class TerminalTests
{
    [UpstreamFact("XTJS-1317", "Headless API Tests Default options")]
    public void DefaultOptions_UseEightyColumnsAndTwentyFourRows()
    {
        using var terminal = new Terminal();
        Assert.Equal(80, terminal.Columns);
        Assert.Equal(24, terminal.Rows);
    }

    [UpstreamFact("XTJS-1318", "Headless API Tests Proposed API check")]
    public void ProposedApiCheck_RejectsUnicodeWhenDisabled()
    {
        using var terminal = new Terminal(new TerminalOptions { AllowProposedApi = false });
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => terminal.Unicode);
        Assert.Equal("You must set the allowProposedApi option to true to use proposed API", exception.Message);
    }

    [UpstreamFact("XTJS-1319", "Headless API Tests write")]
    public async Task Write_ProcessesOrderedStrings()
    {
        await using var terminal = new Terminal();
        await terminal.WriteAsync("foo");
        await terminal.WriteAsync("bar");
        await terminal.WriteAsync("文");
        AssertLine(terminal, 0, "foobar文");
    }

    [UpstreamFact("XTJS-1320", "Headless API Tests write with callback")]
    public async Task WriteWithCallback_InvokesCallbacksInQueueOrder()
    {
        await using var terminal = new Terminal();
        var calls = new List<string>();
        ValueTask first = terminal.WriteAsync("foo", () => calls.Add("a"));
        ValueTask second = terminal.WriteAsync("bar", () => calls.Add("b"));
        ValueTask third = terminal.WriteAsync("文", () => calls.Add("c"));
        await Task.WhenAll(first.AsTask(), second.AsTask(), third.AsTask());
        AssertLine(terminal, 0, "foobar文");
        Assert.Equal(["a", "b", "c"], calls);
    }

    [UpstreamFact("XTJS-1321", "Headless API Tests write - bytes (UTF8)")]
    public async Task WriteBytes_DecodesUtf8InOrder()
    {
        await using var terminal = new Terminal();
        await terminal.WriteAsync(new byte[] { 102, 111, 111 });
        await terminal.WriteAsync(new byte[] { 98, 97, 114 });
        await terminal.WriteAsync(new byte[] { 230, 150, 135 });
        AssertLine(terminal, 0, "foobar文");
    }

    [UpstreamFact("XTJS-1322", "Headless API Tests write - bytes (UTF8) with callback")]
    public async Task WriteBytesWithCallback_InvokesCallbacksInQueueOrder()
    {
        await using var terminal = new Terminal();
        var calls = new List<string>();
        ValueTask first = terminal.WriteAsync(new byte[] { 102, 111, 111 }, () => calls.Add("A"));
        ValueTask second = terminal.WriteAsync(new byte[] { 98, 97, 114 }, () => calls.Add("B"));
        ValueTask third = terminal.WriteAsync(new byte[] { 230, 150, 135 }, () => calls.Add("C"));
        await Task.WhenAll(first.AsTask(), second.AsTask(), third.AsTask());
        AssertLine(terminal, 0, "foobar文");
        Assert.Equal(["A", "B", "C"], calls);
    }

    [UpstreamFact("XTJS-1323", "Headless API Tests writeln")]
    public async Task WriteLine_WritesTextAndNewLine()
    {
        await using var terminal = new Terminal();
        await terminal.WriteLineAsync("foo");
        await terminal.WriteLineAsync("bar");
        await terminal.WriteLineAsync("文");
        AssertLine(terminal, 0, "foo");
        AssertLine(terminal, 1, "bar");
        AssertLine(terminal, 2, "文");
    }

    [UpstreamFact("XTJS-1324", "Headless API Tests writeln with callback")]
    public async Task WriteLineWithCallback_InvokesCallbacksInQueueOrder()
    {
        await using var terminal = new Terminal();
        var calls = new List<string>();
        ValueTask first = terminal.WriteLineAsync("foo", () => calls.Add("1"));
        ValueTask second = terminal.WriteLineAsync("bar", () => calls.Add("2"));
        ValueTask third = terminal.WriteLineAsync("文", () => calls.Add("3"));
        await Task.WhenAll(first.AsTask(), second.AsTask(), third.AsTask());
        AssertLine(terminal, 0, "foo");
        AssertLine(terminal, 1, "bar");
        AssertLine(terminal, 2, "文");
        Assert.Equal(["1", "2", "3"], calls);
    }

    [UpstreamFact("XTJS-1325", "Headless API Tests writeln - bytes (UTF8)")]
    public async Task WriteLineBytes_DecodesUtf8AndWritesNewLine()
    {
        await using var terminal = new Terminal();
        await terminal.WriteLineAsync(new byte[] { 102, 111, 111 });
        await terminal.WriteLineAsync(new byte[] { 98, 97, 114 });
        await terminal.WriteLineAsync(new byte[] { 230, 150, 135 });
        AssertLine(terminal, 0, "foo");
        AssertLine(terminal, 1, "bar");
        AssertLine(terminal, 2, "文");
    }

    [UpstreamFact("XTJS-1326", "Headless API Tests clear")]
    public async Task Clear_PreservesCursorLineAndClearsRestOfViewport()
    {
        await using var terminal = CreateTerminal(rows: 5);
        for (int index = 0; index < 10; index++)
        {
            await terminal.WriteAsync($"\n\rtest{index}");
        }
        await terminal.ClearAsync();
        Assert.Equal(5, terminal.Buffer.Active.Length);
        AssertLine(terminal, 0, "test9");
        for (int index = 1; index < 5; index++)
        {
            AssertLine(terminal, index, string.Empty);
        }
    }

    [UpstreamFact("XTJS-1327", "Headless API Tests clear disposes markers")]
    public async Task Clear_DisposesAllRegisteredMarkers()
    {
        await using var terminal = CreateTerminal(rows: 5);
        for (int index = 0; index < 10; index++)
        {
            await terminal.WriteAsync($"\n\rtest{index}");
        }
        TerminalMarker[] markers =
        [
            await terminal.RegisterMarkerAsync(1),
            await terminal.RegisterMarkerAsync(2),
            await terminal.RegisterMarkerAsync(3),
            await terminal.RegisterMarkerAsync(4)
        ];
        int disposeCount = 0;
        foreach (TerminalMarker marker in markers)
        {
            marker.Disposed += (_, _) => disposeCount++;
        }
        await terminal.ClearAsync();
        Assert.Equal(markers.Length, disposeCount);
        Assert.All(markers, marker => Assert.True(marker.IsDisposed));
        Assert.Empty(terminal.Markers);
    }

    [UpstreamFact("XTJS-1328", "Headless API Tests dispose")]
    public async Task Dispose_TransitionsTerminalToDisposedState()
    {
        var terminal = new Terminal();
        Assert.False(terminal.IsDisposed);
        await terminal.DisposeAsync();
        Assert.True(terminal.IsDisposed);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => terminal.WriteAsync("x").AsTask());
    }

    [UpstreamFact("XTJS-1329", "Headless API Tests options get options")]
    public void Options_GetReturnsHeadlessDefaults()
    {
        using var terminal = new Terminal();
        Assert.Equal(1, terminal.Options.LineHeight);
        Assert.Equal(1, terminal.Options.CursorWidth);
    }

    [UpstreamFact("XTJS-1330", "Headless API Tests options set options")]
    public async Task Options_UpdateAppliesRuntimeOptions()
    {
        await using var terminal = new Terminal();
        await terminal.UpdateOptionsAsync(new TerminalOptionsUpdate { Scrollback = 1 });
        Assert.Equal(1, terminal.Options.Scrollback);
        await terminal.UpdateOptionsAsync(new TerminalOptionsUpdate { FontSize = 12, FontFamily = "Arial" });
        Assert.Equal(12, terminal.Options.FontSize);
        Assert.Equal("Arial", terminal.Options.FontFamily);
    }

    [UpstreamFact("XTJS-1331", "Headless API Tests loadAddon constructor")]
    public void LoadAddon_ActivatesAddonWithTerminal()
    {
        using var terminal = CreateTerminal(columns: 5);
        var addon = new TestAddon(value => value.Columns);
        terminal.LoadAddon(addon);
        Assert.Equal(5, addon.ActivationValue);
    }

    [UpstreamFact("XTJS-1332", "Headless API Tests loadAddon dispose (addon)")]
    public void LoadAddon_AddonCanBeDisposedDirectly()
    {
        using var terminal = new Terminal();
        var addon = new TestAddon(_ => 0);
        terminal.LoadAddon(addon);
        Assert.False(addon.IsDisposed);
        addon.Dispose();
        Assert.True(addon.IsDisposed);
    }

    [UpstreamFact("XTJS-1333", "Headless API Tests loadAddon dispose (terminal)")]
    public async Task LoadAddon_TerminalDisposesLoadedAddon()
    {
        var terminal = new Terminal();
        var addon = new TestAddon(_ => 0);
        terminal.LoadAddon(addon);
        Assert.False(addon.IsDisposed);
        await terminal.DisposeAsync();
        Assert.True(addon.IsDisposed);
    }

    [UpstreamFact("XTJS-1334", "Headless API Tests Events onCursorMove")]
    public async Task Events_CursorMovedFiresOncePerWriteBatch()
    {
        await using var terminal = new Terminal();
        int calls = 0;
        terminal.CursorMoved += (_, _) => calls++;
        await terminal.WriteAsync("foo");
        Assert.Equal(1, calls);
        await terminal.WriteAsync("bar");
        Assert.Equal(2, calls);
    }

    [UpstreamFact("XTJS-1335", "Headless API Tests Events onData")]
    public async Task Events_DataReportsDeviceStatusResponse()
    {
        await using var terminal = new Terminal();
        var calls = new List<string>();
        terminal.Data += (_, args) => calls.Add(args.Data);
        await terminal.WriteAsync("\x1b[5n");
        Assert.Equal(["\x1b[0n"], calls);
    }

    [UpstreamFact("XTJS-1336", "Headless API Tests Events onLineFeed")]
    public async Task Events_LineFeedFiresForEachWriteLine()
    {
        await using var terminal = new Terminal();
        int calls = 0;
        terminal.LineFeed += (_, _) => calls++;
        await terminal.WriteLineAsync("foo");
        Assert.Equal(1, calls);
        await terminal.WriteLineAsync("bar");
        Assert.Equal(2, calls);
    }

    [UpstreamFact("XTJS-1337", "Headless API Tests Events onRender")]
    public async Task Events_RenderCoalescesDirtyRowRange()
    {
        await using var terminal = new Terminal();
        var calls = new List<(int Start, int End)>();
        terminal.RenderRequested += (_, args) => calls.Add((args.StartRow, args.EndRow));
        await terminal.WriteAsync("foo");
        Assert.Equal([(0, 0)], calls);
        await terminal.WriteAsync("\n\nbar");
        Assert.Equal([(0, 0), (0, 2)], calls);
    }

    [UpstreamFact("XTJS-1338", "Headless API Tests Events onScroll")]
    public async Task Events_ScrollReportsNewViewportBase()
    {
        await using var terminal = CreateTerminal(rows: 5);
        var calls = new List<int>();
        terminal.Scrolled += (_, args) => calls.Add(args.ViewportY);
        for (int index = 0; index < 4; index++)
        {
            await terminal.WriteLineAsync("foo");
        }
        Assert.Empty(calls);
        await terminal.WriteLineAsync("bar");
        Assert.Equal([1], calls);
        await terminal.WriteLineAsync("baz");
        Assert.Equal([1, 2], calls);
    }

    [UpstreamFact("XTJS-1339", "Headless API Tests Events onResize")]
    public async Task Events_ResizeReportsEachNewGeometry()
    {
        await using var terminal = new Terminal();
        var calls = new List<(int Columns, int Rows)>();
        terminal.Resized += (_, args) => calls.Add((args.Columns, args.Rows));
        await terminal.ResizeAsync(10, 5);
        await terminal.ResizeAsync(20, 15);
        Assert.Equal([(10, 5), (20, 15)], calls);
    }

    [UpstreamFact("XTJS-1340", "Headless API Tests Events onTitleChange")]
    public async Task Events_TitleChangedReportsOscTitle()
    {
        await using var terminal = new Terminal();
        var calls = new List<string>();
        terminal.TitleChanged += (_, args) => calls.Add(args.Title);
        await terminal.WriteAsync("\x1b]2;foo\x9c");
        Assert.Equal(["foo"], calls);
    }

    [UpstreamFact("XTJS-1341", "Headless API Tests Events onBell")]
    public async Task Events_BellFiresForBelControl()
    {
        await using var terminal = new Terminal();
        int calls = 0;
        terminal.Bell += (_, _) => calls++;
        await terminal.WriteAsync("\x07");
        Assert.Equal(1, calls);
    }

    [UpstreamFact("XTJS-1342", "Headless API Tests buffer cursorX, cursorY")]
    public async Task Buffer_ReportsLogicalCursorCoordinates()
    {
        await using var terminal = CreateTerminal(columns: 5, rows: 5);
        Assert.Equal((0, 0), (terminal.Buffer.Active.CursorX, terminal.Buffer.Active.CursorY));
        await terminal.WriteAsync("foo");
        Assert.Equal((3, 0), (terminal.Buffer.Active.CursorX, terminal.Buffer.Active.CursorY));
        await terminal.WriteAsync("\n");
        Assert.Equal((3, 1), (terminal.Buffer.Active.CursorX, terminal.Buffer.Active.CursorY));
        await terminal.WriteAsync("\r");
        Assert.Equal((0, 1), (terminal.Buffer.Active.CursorX, terminal.Buffer.Active.CursorY));
        await terminal.WriteAsync("abcde");
        Assert.Equal((5, 1), (terminal.Buffer.Active.CursorX, terminal.Buffer.Active.CursorY));
        await terminal.WriteAsync("\n\r\n\n\n\n\n");
        Assert.Equal((0, 4), (terminal.Buffer.Active.CursorX, terminal.Buffer.Active.CursorY));
    }

    [UpstreamFact("XTJS-1343", "Headless API Tests buffer viewportY")]
    public async Task Buffer_ReportsAndScrollsViewportY()
    {
        await using var terminal = CreateTerminal(rows: 5);
        Assert.Equal(0, terminal.Buffer.Active.ViewportY);
        await terminal.WriteAsync("\n\n\n\n");
        Assert.Equal(0, terminal.Buffer.Active.ViewportY);
        await terminal.WriteAsync("\n");
        Assert.Equal(1, terminal.Buffer.Active.ViewportY);
        await terminal.WriteAsync("\n\n\n\n");
        Assert.Equal(5, terminal.Buffer.Active.ViewportY);
        await terminal.ScrollLinesAsync(-1);
        Assert.Equal(4, terminal.Buffer.Active.ViewportY);
        await terminal.ScrollToTopAsync();
        Assert.Equal(0, terminal.Buffer.Active.ViewportY);
    }

    [UpstreamFact("XTJS-1344", "Headless API Tests buffer baseY")]
    public async Task Buffer_ReportsBaseYIndependentOfViewportScrolling()
    {
        await using var terminal = CreateTerminal(rows: 5);
        Assert.Equal(0, terminal.Buffer.Active.BaseY);
        await terminal.WriteAsync("\n\n\n\n");
        Assert.Equal(0, terminal.Buffer.Active.BaseY);
        await terminal.WriteAsync("\n");
        Assert.Equal(1, terminal.Buffer.Active.BaseY);
        await terminal.WriteAsync("\n\n\n\n");
        Assert.Equal(5, terminal.Buffer.Active.BaseY);
        await terminal.ScrollLinesAsync(-1);
        Assert.Equal(5, terminal.Buffer.Active.BaseY);
        await terminal.ScrollToTopAsync();
        Assert.Equal(5, terminal.Buffer.Active.BaseY);
    }

    [UpstreamFact("XTJS-1345", "Headless API Tests buffer length")]
    public async Task Buffer_ReportsTotalLineCountIncludingScrollback()
    {
        await using var terminal = CreateTerminal(rows: 5);
        Assert.Equal(5, terminal.Buffer.Active.Length);
        await terminal.WriteAsync("\n\n\n\n");
        Assert.Equal(5, terminal.Buffer.Active.Length);
        await terminal.WriteAsync("\n");
        Assert.Equal(6, terminal.Buffer.Active.Length);
        await terminal.WriteAsync("\n\n\n\n");
        Assert.Equal(10, terminal.Buffer.Active.Length);
    }

    [UpstreamFact("XTJS-1346", "Headless API Tests buffer active, normal, alternate")]
    public async Task Buffer_ExposesActiveNormalAndAlternateBuffers()
    {
        await using var terminal = CreateTerminal(columns: 5);
        Assert.Equal(TerminalBufferKind.Normal, terminal.Buffer.Active.Kind);
        Assert.Equal(TerminalBufferKind.Normal, terminal.Buffer.Normal.Kind);
        Assert.Equal(TerminalBufferKind.Alternate, terminal.Buffer.Alternate.Kind);
        await terminal.WriteAsync("norm ");
        Assert.Equal("norm ", terminal.Buffer.Normal.GetLine(0)!.TranslateToString());
        Assert.Null(terminal.Buffer.Alternate.GetLine(0));

        await terminal.WriteAsync("\x1b[?47h\r");
        Assert.Equal(TerminalBufferKind.Alternate, terminal.Buffer.Active.Kind);
        Assert.Equal("     ", terminal.Buffer.Active.GetLine(0)!.TranslateToString());
        await terminal.WriteAsync("alt  ");
        Assert.Equal("alt  ", terminal.Buffer.Alternate.GetLine(0)!.TranslateToString());
        Assert.Equal("norm ", terminal.Buffer.Normal.GetLine(0)!.TranslateToString());

        await terminal.WriteAsync("\x1b[?47l\r");
        Assert.Equal(TerminalBufferKind.Normal, terminal.Buffer.Active.Kind);
        Assert.Equal("norm ", terminal.Buffer.Active.GetLine(0)!.TranslateToString());
        Assert.Null(terminal.Buffer.Alternate.GetLine(0));
    }

    [UpstreamFact("XTJS-1347", "Headless API Tests buffer registerMarker on alternate buffer")]
    public async Task Buffer_RegisterMarkerTargetsActiveAlternateBuffer()
    {
        await using var terminal = CreateTerminal(columns: 5);
        await terminal.WriteAsync("\x1b[?47h");
        TerminalMarker marker = await terminal.RegisterMarkerAsync();
        Assert.Equal(TerminalBufferKind.Alternate, terminal.Buffer.Active.Kind);
        Assert.Single(terminal.Markers);
        Assert.Same(marker, terminal.Markers[0]);
    }

    [UpstreamFact("XTJS-1348", "Headless API Tests buffer getLine invalid index")]
    public async Task Buffer_GetLineReturnsNullForInvalidIndex()
    {
        await using var terminal = CreateTerminal(rows: 5);
        Assert.Null(terminal.Buffer.Active.GetLine(-1));
        Assert.Null(terminal.Buffer.Active.GetLine(5));
    }

    [UpstreamFact("XTJS-1349", "Headless API Tests buffer getLine isWrapped")]
    public async Task Buffer_GetLineReportsWrappedState()
    {
        await using var terminal = CreateTerminal(columns: 5);
        Assert.False(terminal.Buffer.Active.GetLine(0)!.IsWrapped);
        Assert.False(terminal.Buffer.Active.GetLine(1)!.IsWrapped);
        await terminal.WriteAsync("abcde");
        Assert.False(terminal.Buffer.Active.GetLine(1)!.IsWrapped);
        await terminal.WriteAsync("f");
        Assert.True(terminal.Buffer.Active.GetLine(1)!.IsWrapped);
    }

    [UpstreamFact("XTJS-1350", "Headless API Tests buffer getLine translateToString")]
    public async Task Buffer_LineTranslatesFullTrimmedAndSlicedText()
    {
        await using var terminal = CreateTerminal(columns: 5);
        Assert.Equal("     ", terminal.Buffer.Active.GetLine(0)!.TranslateToString());
        Assert.Equal(string.Empty, terminal.Buffer.Active.GetLine(0)!.TranslateToString(true));
        await terminal.WriteAsync("foo");
        Assert.Equal("foo  ", terminal.Buffer.Active.GetLine(0)!.TranslateToString());
        Assert.Equal("foo", terminal.Buffer.Active.GetLine(0)!.TranslateToString(true));
        await terminal.WriteAsync("bar");
        Assert.Equal("fooba", terminal.Buffer.Active.GetLine(0)!.TranslateToString());
        Assert.Equal("r", terminal.Buffer.Active.GetLine(1)!.TranslateToString(true));
        Assert.Equal("ooba", terminal.Buffer.Active.GetLine(0)!.TranslateToString(false, 1));
        Assert.Equal("oo", terminal.Buffer.Active.GetLine(0)!.TranslateToString(false, 1, 3));
    }

    [UpstreamFact("XTJS-1351", "Headless API Tests buffer getLine getCell")]
    public async Task Buffer_LineGetCellReportsCharactersAndWidths()
    {
        await using var terminal = CreateTerminal(columns: 5);
        TerminalLineSnapshot line = terminal.Buffer.Active.GetLine(0)!;
        Assert.Null(line.GetCell(-1));
        Assert.Null(line.GetCell(5));
        Assert.Equal(string.Empty, line.GetCell(0)!.Value.GetChars());
        Assert.Equal(1, line.GetCell(0)!.Value.GetWidth());
        await terminal.WriteAsync("a文");
        line = terminal.Buffer.Active.GetLine(0)!;
        Assert.Equal("a", line.GetCell(0)!.Value.GetChars());
        Assert.Equal(1, line.GetCell(0)!.Value.GetWidth());
        Assert.Equal("文", line.GetCell(1)!.Value.GetChars());
        Assert.Equal(2, line.GetCell(1)!.Value.GetWidth());
        Assert.Equal(string.Empty, line.GetCell(2)!.Value.GetChars());
        Assert.Equal(0, line.GetCell(2)!.Value.GetWidth());
    }

    [UpstreamFact("XTJS-1352", "Headless API Tests modes defaults")]
    public void Modes_DefaultsMatchHeadlessTerminalDefaults()
    {
        using var terminal = new Terminal();
        TerminalModes modes = terminal.Modes;
        Assert.False(modes.ApplicationCursorKeys);
        Assert.False(modes.ApplicationKeypad);
        Assert.False(modes.BracketedPaste);
        Assert.False(modes.Insert);
        Assert.Equal(TerminalMouseTrackingMode.None, modes.MouseTracking);
        Assert.False(modes.Origin);
        Assert.False(modes.ReverseWraparound);
        Assert.False(modes.SendFocus);
        Assert.True(modes.ShowCursor);
        Assert.False(modes.SynchronizedOutput);
        Assert.False(modes.Win32InputMode);
        Assert.True(modes.Wraparound);
    }

    [UpstreamFact("XTJS-1353", "Headless API Tests modes applicationCursorKeysMode")]
    public async Task Modes_ApplicationCursorKeysCanBeSetAndReset() =>
        await AssertModeToggle("\x1b[?1h", "\x1b[?1l", modes => modes.ApplicationCursorKeys);

    [UpstreamFact("XTJS-1354", "Headless API Tests modes applicationKeypadMode")]
    public async Task Modes_ApplicationKeypadCanBeSetAndReset() =>
        await AssertModeToggle("\x1b[?66h", "\x1b[?66l", modes => modes.ApplicationKeypad);

    [UpstreamFact("XTJS-1355", "Headless API Tests modes bracketedPasteMode")]
    public async Task Modes_BracketedPasteCanBeSetAndReset() =>
        await AssertModeToggle("\x1b[?2004h", "\x1b[?2004l", modes => modes.BracketedPaste);

    [UpstreamFact("XTJS-1356", "Headless API Tests modes insertMode")]
    public async Task Modes_InsertCanBeSetAndReset() =>
        await AssertModeToggle("\x1b[4h", "\x1b[4l", modes => modes.Insert);

    [UpstreamFact("XTJS-1357", "Headless API Tests modes mouseTrackingMode")]
    public async Task Modes_MouseTrackingSupportsAllHeadlessModes()
    {
        await using var terminal = new Terminal();
        await AssertMouseMode(terminal, 9, TerminalMouseTrackingMode.X10);
        await AssertMouseMode(terminal, 1000, TerminalMouseTrackingMode.Vt200);
        await AssertMouseMode(terminal, 1002, TerminalMouseTrackingMode.Drag);
        await AssertMouseMode(terminal, 1003, TerminalMouseTrackingMode.Any);
    }

    [UpstreamFact("XTJS-1358", "Headless API Tests modes originMode")]
    public async Task Modes_OriginCanBeSetAndReset() =>
        await AssertModeToggle("\x1b[?6h", "\x1b[?6l", modes => modes.Origin);

    [UpstreamFact("XTJS-1359", "Headless API Tests modes reverseWraparoundMode")]
    public async Task Modes_ReverseWraparoundCanBeSetAndReset() =>
        await AssertModeToggle("\x1b[?45h", "\x1b[?45l", modes => modes.ReverseWraparound);

    [UpstreamFact("XTJS-1360", "Headless API Tests modes sendFocusMode")]
    public async Task Modes_SendFocusCanBeSetAndReset() =>
        await AssertModeToggle("\x1b[?1004h", "\x1b[?1004l", modes => modes.SendFocus);

    [UpstreamFact("XTJS-1361", "Headless API Tests modes wraparoundMode")]
    public async Task Modes_WraparoundCanBeSetAndReset()
    {
        await using var terminal = new Terminal();
        await terminal.WriteAsync("\x1b[?7h");
        Assert.True(terminal.Modes.Wraparound);
        await terminal.WriteAsync("\x1b[?7l");
        Assert.False(terminal.Modes.Wraparound);
    }

    private static Terminal CreateTerminal(int columns = 80, int rows = 24) =>
        new(new TerminalOptions { Columns = columns, Rows = rows, AllowProposedApi = true });

    private static void AssertLine(Terminal terminal, int index, string expected) =>
        Assert.Equal(expected, terminal.Buffer.Active.GetLine(index)!.TranslateToString(true));

    private static async Task AssertModeToggle(
        string setSequence,
        string resetSequence,
        Func<TerminalModes, bool> selector)
    {
        await using var terminal = new Terminal();
        await terminal.WriteAsync(setSequence);
        Assert.True(selector(terminal.Modes));
        await terminal.WriteAsync(resetSequence);
        Assert.False(selector(terminal.Modes));
    }

    private static async Task AssertMouseMode(Terminal terminal, int mode, TerminalMouseTrackingMode expected)
    {
        await terminal.WriteAsync($"\x1b[?{mode}h");
        Assert.Equal(expected, terminal.Modes.MouseTracking);
        await terminal.WriteAsync($"\x1b[?{mode}l");
        Assert.Equal(TerminalMouseTrackingMode.None, terminal.Modes.MouseTracking);
    }

    private sealed class TestAddon(Func<Terminal, int> activation) : ITerminalAddon
    {
        public int ActivationValue { get; private set; }
        public bool IsDisposed { get; private set; }

        public void Activate(Terminal terminal) => ActivationValue = activation(terminal);

        public void Dispose() => IsDisposed = true;
    }
}

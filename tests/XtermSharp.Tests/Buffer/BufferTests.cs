using XtermSharp.Internal;

namespace XtermSharp.Tests.Buffer;

public sealed class BufferTests
{
    private const int InitialColumns = 80;
    private const int InitialRows = 24;
    private const int InitialScrollback = 1000;

    public static TheoryData<string, bool[], int, int, int> WrappedRangeCases { get; } = CreateWrappedRangeCases();

    [UpstreamFact("XTJS-0002", "Buffer constructor should create a CircularList with max length equal to rows + scrollback, for its lines")]
    public void Constructor_SetsMaximumLineCountToRowsPlusScrollback()
    {
        using TerminalBuffer buffer = CreateBuffer();
        Assert.Equal(InitialRows + InitialScrollback, buffer.MaximumLineCount);
    }

    [UpstreamFact("XTJS-0003", "Buffer constructor should set the Buffer's scrollBottom value equal to the terminal's rows -1")]
    public void Constructor_SetsScrollBottomToLastViewportRow()
    {
        using TerminalBuffer buffer = CreateBuffer();
        Assert.Equal(InitialRows - 1, buffer.ScrollBottom);
    }

    [UpstreamFact("XTJS-0004", "Buffer fillViewportRows should fill the buffer with blank lines based on the size of the viewport")]
    public void FillViewportRows_FillsViewportWithBlankLines()
    {
        using TerminalBuffer buffer = CreateBuffer();
        buffer.FillViewportRows(CellStyle.Default);
        Assert.Equal(InitialRows, buffer.LineCount);
        Assert.All(buffer.Lines, line =>
        {
            Assert.Equal(InitialColumns, line.Length);
            Assert.All(line.CopyCells(line.Length), cell => Assert.Equal(string.Empty, cell.GetText()));
        });
    }

    [Theory]
    [MemberData(nameof(WrappedRangeCases))]
    public void GetWrappedRangeForLine_ReturnsCompleteLogicalRange(
        string upstreamId,
        bool[] wrappedLines,
        int requestedLine,
        int expectedFirst,
        int expectedLast)
    {
        Assert.StartsWith("XTJS-", upstreamId, StringComparison.Ordinal);
        using TerminalBuffer buffer = CreateBuffer();
        for (int index = 0; index < wrappedLines.Length; index++)
        {
            buffer.GetLine(index).IsWrapped = wrappedLines[index];
        }
        Assert.Equal((expectedFirst, expectedLast), buffer.GetWrappedRangeForLine(requestedLine));
    }

    [UpstreamFact("XTJS-0015", "Buffer resize column size is reduced should trim the data in the buffer")]
    public void Resize_ReducingColumnsTrimsEveryLine()
    {
        using TerminalBuffer buffer = CreateBuffer();
        buffer.Resize(InitialColumns / 2, InitialRows, InitialScrollback, CellStyle.Default);
        Assert.Equal(InitialRows, buffer.LineCount);
        Assert.All(buffer.Lines, line => Assert.Equal(InitialColumns / 2, line.Length));
    }

    [UpstreamFact("XTJS-0016", "Buffer resize column size is increased should add pad columns")]
    public void Resize_IncreasingColumnsPadsEveryLine()
    {
        using TerminalBuffer buffer = CreateBuffer();
        buffer.Resize(InitialColumns + 10, InitialRows, InitialScrollback, CellStyle.Default);
        Assert.Equal(InitialRows, buffer.LineCount);
        Assert.All(buffer.Lines, line => Assert.Equal(InitialColumns + 10, line.Length));
    }

    [UpstreamFact("XTJS-0017", "Buffer resize row size reduced should trim blank lines from the end")]
    public void Resize_ReducingRowsTrimsBlankLinesFromEnd()
    {
        using TerminalBuffer buffer = CreateBuffer();
        buffer.Resize(InitialColumns, InitialRows - 10, InitialScrollback, CellStyle.Default);
        Assert.Equal(InitialRows - 10, buffer.LineCount);
    }

    [UpstreamFact("XTJS-0018", "Buffer resize row size reduced should move the viewport down when it's at the end")]
    public void Resize_ReducingRowsMovesBottomViewportDown()
    {
        using TerminalBuffer buffer = CreateBuffer();
        buffer.CursorY = InitialRows - 6;
        buffer.Resize(InitialColumns, InitialRows - 10, InitialScrollback, CellStyle.Default);
        Assert.Equal(InitialRows - 5, buffer.LineCount);
        Assert.Equal(5, buffer.YBase);
        Assert.Equal(5, buffer.YDisp);
    }

    [UpstreamFact("XTJS-0019", "Buffer resize row size reduced no scrollback should trim from the top of the buffer when the cursor reaches the bottom")]
    public void Resize_ReducingRowsWithoutScrollbackTrimsTop()
    {
        using TerminalBuffer buffer = CreateBuffer(scrollback: 0);
        buffer.CursorY = InitialRows - 1;
        Put(buffer.GetLine(5), 0, "a");
        Put(buffer.GetLine(InitialRows - 1), 0, "b");
        buffer.Resize(InitialColumns, InitialRows - 5, 0, CellStyle.Default);
        Assert.Equal("a", buffer.GetLine(0).GetCell(0).GetText());
        Assert.Equal("b", buffer.GetLine(InitialRows - 6).GetCell(0).GetText());
    }

    [UpstreamFact("XTJS-0020", "Buffer resize row size increased empty buffer should add blank lines to end")]
    public void Resize_IncreasingRowsAddsBlankLinesToEmptyBuffer()
    {
        using TerminalBuffer buffer = CreateBuffer();
        buffer.Resize(InitialColumns, InitialRows + 10, InitialScrollback, CellStyle.Default);
        Assert.Equal(0, buffer.YDisp);
        Assert.Equal(InitialRows + 10, buffer.LineCount);
    }

    [UpstreamFact("XTJS-0021", "Buffer resize row size increased filled buffer should show more of the buffer above")]
    public void Resize_IncreasingRowsShowsMoreScrollbackAbove()
    {
        using TerminalBuffer buffer = CreateBuffer();
        AddBlankLines(buffer, 10);
        buffer.CursorY = InitialRows - 1;
        buffer.YBase = 10;
        buffer.YDisp = 10;
        buffer.Resize(InitialColumns, InitialRows + 5, InitialScrollback, CellStyle.Default);
        Assert.Equal(5, buffer.YBase);
        Assert.Equal(5, buffer.YDisp);
        Assert.Equal(InitialRows + 10, buffer.LineCount);
    }

    [UpstreamFact("XTJS-0022", "Buffer resize row size increased filled buffer should show more of the buffer below when the viewport is at the top of the buffer")]
    public void Resize_IncreasingRowsKeepsTopViewportAndShowsBelow()
    {
        using TerminalBuffer buffer = CreateBuffer();
        AddBlankLines(buffer, 10);
        buffer.CursorY = InitialRows - 1;
        buffer.YBase = 10;
        buffer.YDisp = 0;
        buffer.Resize(InitialColumns, InitialRows + 5, InitialScrollback, CellStyle.Default);
        Assert.Equal(5, buffer.YBase);
        Assert.Equal(0, buffer.YDisp);
        Assert.Equal(InitialRows + 10, buffer.LineCount);
    }

    [UpstreamFact("XTJS-0023", "Buffer resize row size increased Windows ConPTY should not adjust ybase or ydisp when growing rows")]
    public void Resize_WindowsConPtyDoesNotPullRowsFromScrollbackWhenGrowing()
    {
        using TerminalBuffer buffer = CreateBuffer();
        AddBlankLines(buffer, 10);
        buffer.CursorY = InitialRows - 1;
        buffer.YBase = 10;
        buffer.YDisp = 10;
        int before = buffer.LineCount;
        buffer.Resize(InitialColumns, InitialRows + 5, InitialScrollback, CellStyle.Default, new BufferResizeOptions(true, 19000));
        Assert.Equal(10, buffer.YBase);
        Assert.Equal(10, buffer.YDisp);
        Assert.Equal(before + 5, buffer.LineCount);
    }

    [UpstreamFact("XTJS-0024", "Buffer resize row and column increased should resize properly")]
    public void Resize_IncreasingRowsAndColumnsResizesAllLines()
    {
        using TerminalBuffer buffer = CreateBuffer();
        buffer.Resize(InitialColumns + 5, InitialRows + 5, InitialScrollback, CellStyle.Default);
        Assert.Equal(InitialRows + 5, buffer.LineCount);
        Assert.All(buffer.Lines, line => Assert.Equal(InitialColumns + 5, line.Length));
    }

    [UpstreamFact("XTJS-0025", "Buffer resize reflow should not wrap empty lines")]
    public void Reflow_DoesNotWrapEmptyLines()
    {
        using TerminalBuffer buffer = CreateBuffer();
        buffer.Resize(InitialColumns - 5, InitialRows, InitialScrollback, CellStyle.Default);
        Assert.Equal(InitialRows, buffer.LineCount);
        Assert.DoesNotContain(buffer.Lines, line => line.IsWrapped);
    }

    [UpstreamFact("XTJS-0026", "Buffer resize reflow should shrink row length")]
    public void Reflow_ShrinksEveryRowLength()
    {
        using TerminalBuffer buffer = CreateBuffer();
        buffer.Resize(5, 10, InitialScrollback, CellStyle.Default);
        Assert.Equal(10, buffer.LineCount);
        Assert.All(buffer.Lines, line => Assert.Equal(5, line.Length));
    }

    [UpstreamFact("XTJS-0027", "Buffer resize reflow should wrap and unwrap lines")]
    public void Reflow_WrapsAndUnwrapsLogicalLines()
    {
        using TerminalBuffer buffer = CreateBuffer(columns: 5, rows: 10);
        Write(buffer.GetLine(0), "abcde");
        buffer.CursorY = 1;
        buffer.Resize(1, 10, InitialScrollback, CellStyle.Default);
        Assert.Equal(["a", "b", "c", "d", "e"], buffer.Lines.Take(5).Select(line => line.TranslateToString()));
        buffer.Resize(5, 10, InitialScrollback, CellStyle.Default, new BufferResizeOptions(ReflowCursorLine: true));
        Assert.Equal("abcde", buffer.GetLine(0).TranslateToString());
        Assert.All(buffer.Lines.Skip(1), line => Assert.Equal("     ", line.TranslateToString()));
    }

    [Fact]
    public async Task Resize_ToSingleColumnWithWideOrOrphanCellsTruncatesAndNormalizes()
    {
        using TerminalBuffer buffer = CreateBuffer(columns: 2, rows: 3, scrollback: 1);
        SetWide(buffer.GetLine(0), 0, "\u6c49");
        buffer.GetLine(1).SetCell(0, new CellData { Width = 0, Style = CellStyle.Default });
        TerminalMarker wideMarker = buffer.AddMarker(0);
        TerminalMarker orphanMarker = buffer.AddMarker(1);
        buffer.CursorX = 1;
        buffer.CursorY = 1;
        CellStyle eraseStyle = CellStyle.Default with { Background = TerminalColor.Palette(7) };

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await Task.Run(() => buffer.Resize(1, 3, 1, eraseStyle), cancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);

        Assert.Equal(1, buffer.Columns);
        Assert.All(buffer.Lines, line =>
        {
            Assert.Equal(1, line.Length);
            Assert.Equal(1, line.GetWidth(0));
        });
        Assert.Equal(string.Empty, buffer.GetLine(0).GetCell(0).GetText());
        Assert.Equal(eraseStyle, buffer.GetLine(0).GetCell(0).Style);
        Assert.Equal(string.Empty, buffer.GetLine(1).GetCell(0).GetText());
        Assert.Equal(eraseStyle, buffer.GetLine(1).GetCell(0).Style);
        Assert.Equal(0, buffer.CursorX);
        Assert.InRange(buffer.CursorY, 0, buffer.Rows - 1);
        Assert.Equal(0, wideMarker.Line);
        Assert.Equal(1, orphanMarker.Line);
        Assert.False(wideMarker.IsDisposed);
        Assert.False(orphanMarker.IsDisposed);
    }

    [UpstreamFact("XTJS-0028", "Buffer resize reflow should gate reflow on ConPTY buildNumber 21376")]
    public void Reflow_IsGatedByConPtyBuildNumber()
    {
        using TerminalBuffer legacy = CreateBuffer(columns: 5, rows: 10);
        Write(legacy.GetLine(0), "abcde");
        legacy.CursorY = 1;
        legacy.Resize(1, 10, InitialScrollback, CellStyle.Default, new BufferResizeOptions(true, 21375));
        Assert.Equal(string.Empty, legacy.GetLine(1).TranslateToString(true));

        using TerminalBuffer modern = CreateBuffer(columns: 5, rows: 10);
        Write(modern.GetLine(0), "abcde");
        modern.CursorY = 1;
        modern.Resize(1, 10, InitialScrollback, CellStyle.Default, new BufferResizeOptions(true, 21376));
        Assert.Equal("b", modern.GetLine(1).TranslateToString(true));
        Assert.True(modern.GetLine(1).IsWrapped);
    }

    [UpstreamFact("XTJS-0029", "Buffer resize reflow should unwrap lines on ConPTY builds with reflow support")]
    public void Reflow_UnwrapsOnSupportedConPtyBuilds()
    {
        using TerminalBuffer buffer = CreateBuffer(columns: 5, rows: 10);
        Write(buffer.GetLine(0), "abcde");
        buffer.CursorY = 1;
        BufferResizeOptions options = new(true, 21376, true);
        buffer.Resize(1, 10, InitialScrollback, CellStyle.Default, options);
        buffer.Resize(5, 10, InitialScrollback, CellStyle.Default, options);
        Assert.Equal("abcde", buffer.GetLine(0).TranslateToString());
        Assert.Equal("     ", buffer.GetLine(1).TranslateToString());
    }

    [UpstreamFact("XTJS-0030", "Buffer resize reflow should reflow wrapped lines containing the cursor when reflowCursorLine is enabled")]
    public void Reflow_ReflowsCursorLogicalLineWhenEnabled()
    {
        using TerminalBuffer buffer = CreateBuffer(columns: 5, rows: 10);
        Write(buffer.GetLine(0), "abcde");
        buffer.Resize(1, 10, InitialScrollback, CellStyle.Default, new BufferResizeOptions(ReflowCursorLine: true));
        buffer.CursorY = 2;
        buffer.Resize(5, 10, InitialScrollback, CellStyle.Default, new BufferResizeOptions(ReflowCursorLine: true));
        Assert.Equal("abcde", buffer.GetLine(0).TranslateToString());
    }

    [UpstreamFact("XTJS-0031", "Buffer resize reflow should not reflow wrapped lines containing the cursor by default")]
    public void Reflow_DoesNotReflowCursorLogicalLineByDefault()
    {
        using TerminalBuffer buffer = CreateBuffer(columns: 5, rows: 10);
        Write(buffer.GetLine(0), "abcde");
        buffer.Resize(1, 10, InitialScrollback, CellStyle.Default, new BufferResizeOptions(ReflowCursorLine: true));
        buffer.CursorY = 2;
        buffer.Resize(5, 10, InitialScrollback, CellStyle.Default);
        Assert.NotEqual("abcde", buffer.GetLine(0).TranslateToString());
        Assert.True(buffer.GetLine(1).IsWrapped);
    }

    [UpstreamFact("XTJS-0032", "Buffer resize reflow should discard parts of wrapped lines that go out of the scrollback")]
    public void Reflow_DiscardsLogicalLinePrefixBeyondScrollback()
    {
        using TerminalBuffer buffer = CreateBuffer(columns: 10, rows: 5, scrollback: 1);
        Write(buffer.GetLine(3), "abcdefghij");
        buffer.CursorY = 4;
        buffer.Resize(2, 5, 1, CellStyle.Default);
        Assert.Equal(6, buffer.LineCount);
        Assert.Equal(["ab", "cd", "ef", "gh", "ij", "  "], buffer.Lines.Select(line => line.TranslateToString()));
        buffer.Resize(1, 5, 1, CellStyle.Default);
        Assert.Equal(["f", "g", "h", "i", "j", " "], buffer.Lines.Select(line => line.TranslateToString()));
        buffer.Resize(10, 5, 1, CellStyle.Default, new BufferResizeOptions(ReflowCursorLine: true));
        Assert.Equal("fghij     ", buffer.GetLine(0).TranslateToString());
    }

    [UpstreamFact("XTJS-0033", "Buffer resize reflow should remove the correct amount of rows when reflowing larger")]
    public void Reflow_LargerRemovesAllObsoleteWrappedRows()
    {
        using TerminalBuffer buffer = CreateBuffer(columns: 10, rows: 10);
        Write(buffer.GetLine(0), "abcdefghij");
        Write(buffer.GetLine(1), "0123456789");
        buffer.CursorY = 2;
        buffer.Resize(2, 10, InitialScrollback, CellStyle.Default);
        Assert.Equal(["ab", "cd", "ef", "gh", "ij", "01", "23", "45", "67", "89"], buffer.Lines.Take(10).Select(line => line.TranslateToString()));
        buffer.Resize(10, 10, InitialScrollback, CellStyle.Default, new BufferResizeOptions(ReflowCursorLine: true));
        Assert.Equal("abcdefghij", buffer.GetLine(0).TranslateToString());
        Assert.Equal("0123456789", buffer.GetLine(1).TranslateToString());
    }

    [UpstreamFact("XTJS-0034", "Buffer resize reflow should transfer combined char data over to reflowed lines")]
    public void Reflow_PreservesCombinedCharacterData()
    {
        using TerminalBuffer buffer = CreateBuffer(columns: 4, rows: 3);
        Write(buffer.GetLine(0), "abc");
        buffer.GetLine(0).SetCell(3, CellData.FromText("😁", 1, CellStyle.Default));
        buffer.CursorY = 2;
        buffer.Resize(2, 3, InitialScrollback, CellStyle.Default);
        Assert.Equal("ab", buffer.GetLine(0).TranslateToString());
        Assert.Equal("c😁", buffer.GetLine(1).TranslateToString());
    }

    [UpstreamFact("XTJS-0035", "Buffer resize reflow should adjust markers when reflowing")]
    public void Reflow_AdjustsMarkersInBothDirections()
    {
        using TerminalBuffer buffer = CreateThreePopulatedLines(10, 16);
        TerminalMarker first = buffer.AddMarker(0);
        TerminalMarker second = buffer.AddMarker(1);
        TerminalMarker third = buffer.AddMarker(2);
        buffer.Resize(2, 16, InitialScrollback, CellStyle.Default);
        Assert.Equal((0, 5, 10), (first.Line, second.Line, third.Line));
        buffer.Resize(10, 16, InitialScrollback, CellStyle.Default, new BufferResizeOptions(ReflowCursorLine: true));
        Assert.Equal((0, 1, 2), (first.Line, second.Line, third.Line));
        Assert.False(first.IsDisposed || second.IsDisposed || third.IsDisposed);
    }

    [UpstreamFact("XTJS-0036", "Buffer resize reflow should dispose markers whose rows are trimmed during a reflow")]
    public void Reflow_DisposesMarkersTrimmedByScrollbackLimit()
    {
        using TerminalBuffer buffer = CreateThreePopulatedLines(10, 11, 1);
        TerminalMarker first = buffer.AddMarker(0);
        TerminalMarker second = buffer.AddMarker(1);
        TerminalMarker third = buffer.AddMarker(2);
        buffer.CursorY = 3;
        buffer.Resize(2, 11, 1, CellStyle.Default);
        Assert.True(first.IsDisposed);
        Assert.False(second.IsDisposed);
        Assert.False(third.IsDisposed);
        Assert.Equal(1, second.Line);
        Assert.Equal(6, third.Line);
    }

    [UpstreamFact("XTJS-0037", "Buffer resize reflow should correctly reflow wrapped lines that end in 0 space (via tab char)")]
    public void Reflow_LargerPreservesNullSpaceAtWrappedBoundary()
    {
        using TerminalBuffer buffer = CreateTabBoundaryBuffer();
        buffer.Resize(5, 10, InitialScrollback, CellStyle.Default, new BufferResizeOptions(ReflowCursorLine: true));
        Assert.Equal("ab  c", buffer.GetLine(0).TranslateToString(true));
        Assert.Equal("d    ", buffer.GetLine(1).TranslateToString());
        buffer.Resize(6, 10, InitialScrollback, CellStyle.Default, new BufferResizeOptions(ReflowCursorLine: true));
        Assert.Equal("ab  cd", buffer.GetLine(0).TranslateToString(true));
    }

    [UpstreamFact("XTJS-0038", "Buffer resize reflow should wrap wide characters correctly when reflowing larger")]
    public void Reflow_LargerWrapsWideCharactersWithoutSplitting()
    {
        using TerminalBuffer buffer = CreateWideWrappedBuffer();
        buffer.Resize(13, 10, InitialScrollback, CellStyle.Default, new BufferResizeOptions(ReflowCursorLine: true));
        Assert.Equal("汉语汉语汉语", buffer.GetLine(0).TranslateToString(true));
        Assert.Equal("汉语汉语汉语", buffer.GetLine(1).TranslateToString(true));
        buffer.Resize(14, 10, InitialScrollback, CellStyle.Default, new BufferResizeOptions(ReflowCursorLine: true));
        Assert.Equal("汉语汉语汉语汉", buffer.GetLine(0).TranslateToString(true));
        Assert.Equal("语汉语汉语", buffer.GetLine(1).TranslateToString(true));
    }

    [UpstreamFact("XTJS-0039", "Buffer resize reflow should correctly reflow wrapped lines that end in 0 space (via tab char)")]
    public void Reflow_SmallerPreservesNullSpaceAtWrappedBoundary()
    {
        using TerminalBuffer buffer = CreateTabBoundaryBuffer();
        buffer.Resize(3, 10, InitialScrollback, CellStyle.Default);
        Assert.Equal("ab ", buffer.GetLine(0).TranslateToString());
        Assert.Equal(" cd", buffer.GetLine(1).TranslateToString());
        buffer.Resize(2, 10, InitialScrollback, CellStyle.Default);
        Assert.Equal("ab", buffer.GetLine(0).TranslateToString());
        Assert.Equal("  ", buffer.GetLine(1).TranslateToString());
        Assert.Equal("cd", buffer.GetLine(2).TranslateToString());
    }

    [UpstreamFact("XTJS-0040", "Buffer resize reflow should wrap wide characters correctly when reflowing smaller")]
    public void Reflow_SmallerWrapsWideCharactersWithoutSplitting()
    {
        using TerminalBuffer buffer = CreateWideWrappedBuffer();
        buffer.Resize(11, 10, InitialScrollback, CellStyle.Default);
        Assert.Equal(["汉语汉语汉", "语汉语汉语", "汉语"], buffer.Lines.Take(3).Select(line => line.TranslateToString(true)));
        buffer.Resize(9, 10, InitialScrollback, CellStyle.Default);
        Assert.Equal(["汉语汉语", "汉语汉语", "汉语汉语"], buffer.Lines.Take(3).Select(line => line.TranslateToString(true)));
        buffer.Resize(7, 10, InitialScrollback, CellStyle.Default);
        Assert.Equal(["汉语汉", "语汉语", "汉语汉", "语汉语"], buffer.Lines.Take(4).Select(line => line.TranslateToString(true)));
    }

    [UpstreamFact("XTJS-0041", "Buffer resize reflow reflowLarger cases viewport not yet filled should move the cursor up and add empty lines")]
    public void ReflowLarger_ViewportNotFilledMovesCursorUpAndPads()
    {
        using TerminalBuffer buffer = CreateLargerMatrixBuffer();
        buffer.CursorY = 6;
        buffer.Resize(4, 10, InitialScrollback, CellStyle.Default, new BufferResizeOptions(ReflowCursorLine: true));
        Assert.Equal(3, buffer.CursorY);
        Assert.Equal((0, 0, 10), (buffer.YBase, buffer.YDisp, buffer.LineCount));
        AssertLogicalLines(buffer, ["abcd", "efgh", "ijkl"]);
    }

    [UpstreamFact("XTJS-0042", "Buffer resize reflow reflowLarger cases viewport filled, scrollback remaining ybase === 0 should move the cursor up and add empty lines")]
    public void ReflowLarger_FilledViewportAtBaseZeroMovesCursorUp()
    {
        using TerminalBuffer buffer = CreateLargerMatrixBuffer();
        buffer.CursorY = 9;
        buffer.Resize(4, 10, InitialScrollback, CellStyle.Default, new BufferResizeOptions(ReflowCursorLine: true));
        Assert.Equal(6, buffer.CursorY);
        Assert.Equal((0, 0, 10), (buffer.YBase, buffer.YDisp, buffer.LineCount));
        AssertLogicalLines(buffer, ["abcd", "efgh", "ijkl"]);
    }

    [UpstreamFact("XTJS-0043", "Buffer resize reflow reflowLarger cases viewport filled, scrollback remaining ybase !== 0 && ydisp === ybase should adjust the viewport and keep ydisp = ybase")]
    public void ReflowLarger_ScrollbackAtBottomKeepsViewportAtBottom()
    {
        using TerminalBuffer buffer = CreateLargerMatrixBufferWithScrollback(InitialScrollback, 10);
        buffer.Resize(4, 10, InitialScrollback, CellStyle.Default, new BufferResizeOptions(ReflowCursorLine: true));
        Assert.Equal(7, buffer.YBase);
        Assert.Equal(buffer.YBase, buffer.YDisp);
        Assert.Equal(17, buffer.LineCount);
    }

    [UpstreamFact("XTJS-0044", "Buffer resize reflow reflowLarger cases viewport filled, scrollback remaining ybase !== 0 && ydisp !== ybase should keep ydisp at the same value")]
    public void ReflowLarger_ScrolledViewportKeepsDisplayPosition()
    {
        using TerminalBuffer buffer = CreateLargerMatrixBufferWithScrollback(InitialScrollback, 5);
        buffer.Resize(4, 10, InitialScrollback, CellStyle.Default, new BufferResizeOptions(ReflowCursorLine: true));
        Assert.Equal(7, buffer.YBase);
        Assert.Equal(5, buffer.YDisp);
        Assert.Equal(17, buffer.LineCount);
    }

    [UpstreamFact("XTJS-0045", "Buffer resize reflow reflowLarger cases viewport filled, no scrollback remaining ybase !== 0 && ydisp === ybase should trim lines and keep ydisp = ybase")]
    public void ReflowLarger_FullScrollbackAtBottomTrimsAndKeepsBottom()
    {
        using TerminalBuffer buffer = CreateLargerMatrixBufferWithScrollback(10, 10);
        buffer.Resize(4, 10, 10, CellStyle.Default, new BufferResizeOptions(ReflowCursorLine: true));
        Assert.Equal(7, buffer.YBase);
        Assert.Equal(buffer.YBase, buffer.YDisp);
        Assert.Equal(17, buffer.LineCount);
    }

    [UpstreamFact("XTJS-0046", "Buffer resize reflow reflowLarger cases viewport filled, no scrollback remaining ybase !== 0 && ydisp !== ybase should trim lines and not change ydisp")]
    public void ReflowLarger_FullScrollbackScrolledTrimsWithoutMovingDisplay()
    {
        using TerminalBuffer buffer = CreateLargerMatrixBufferWithScrollback(10, 5);
        buffer.Resize(4, 10, 10, CellStyle.Default, new BufferResizeOptions(ReflowCursorLine: true));
        Assert.Equal(7, buffer.YBase);
        Assert.Equal(5, buffer.YDisp);
        Assert.Equal(17, buffer.LineCount);
    }

    [UpstreamFact("XTJS-0047", "Buffer resize reflow reflowSmaller cases viewport not yet filled should move the cursor down")]
    public void ReflowSmaller_ViewportNotFilledMovesCursorDown()
    {
        using TerminalBuffer buffer = CreateSmallerMatrixBuffer();
        buffer.CursorY = 3;
        buffer.Resize(2, 10, InitialScrollback, CellStyle.Default);
        Assert.Equal(6, buffer.CursorY);
        Assert.Equal((0, 0, 10), (buffer.YBase, buffer.YDisp, buffer.LineCount));
        AssertLogicalLines(buffer, ["ab", "cd", "ef", "gh", "ij", "kl"]);
    }

    [UpstreamFact("XTJS-0048", "Buffer resize reflow reflowSmaller cases viewport filled, scrollback remaining ybase === 0 should trim the top")]
    public void ReflowSmaller_FilledViewportAtBaseZeroCreatesScrollback()
    {
        using TerminalBuffer buffer = CreateSmallerMatrixBuffer();
        buffer.CursorY = 9;
        buffer.Resize(2, 10, InitialScrollback, CellStyle.Default);
        Assert.Equal((3, 3, 13), (buffer.YBase, buffer.YDisp, buffer.LineCount));
    }

    [UpstreamFact("XTJS-0049", "Buffer resize reflow reflowSmaller cases viewport filled, scrollback remaining ybase !== 0 && ydisp === ybase should adjust the viewport and keep ydisp = ybase")]
    public void ReflowSmaller_ScrollbackAtBottomKeepsViewportAtBottom()
    {
        using TerminalBuffer buffer = CreateSmallerMatrixBufferWithScrollback(InitialScrollback, 10);
        buffer.Resize(2, 10, InitialScrollback, CellStyle.Default);
        Assert.Equal(13, buffer.YBase);
        Assert.Equal(buffer.YBase, buffer.YDisp);
        Assert.Equal(23, buffer.LineCount);
    }

    [UpstreamFact("XTJS-0050", "Buffer resize reflow reflowSmaller cases viewport filled, scrollback remaining ybase !== 0 && ydisp !== ybase should keep ydisp at the same value")]
    public void ReflowSmaller_ScrolledViewportKeepsDisplayPosition()
    {
        using TerminalBuffer buffer = CreateSmallerMatrixBufferWithScrollback(InitialScrollback, 5);
        buffer.Resize(2, 10, InitialScrollback, CellStyle.Default);
        Assert.Equal(13, buffer.YBase);
        Assert.Equal(5, buffer.YDisp);
        Assert.Equal(23, buffer.LineCount);
    }

    [UpstreamFact("XTJS-0051", "Buffer resize reflow reflowSmaller cases viewport filled, no scrollback remaining ybase !== 0 && ydisp === ybase should trim lines and keep ydisp = ybase")]
    public void ReflowSmaller_FullScrollbackAtBottomTrimsAndKeepsBottom()
    {
        using TerminalBuffer buffer = CreateSmallerMatrixBufferWithScrollback(10, 10, 13);
        buffer.Resize(2, 10, 10, CellStyle.Default);
        Assert.Equal((10, 10, 20), (buffer.YBase, buffer.YDisp, buffer.LineCount));
    }

    [UpstreamFact("XTJS-0052", "Buffer resize reflow reflowSmaller cases viewport filled, no scrollback remaining ybase !== 0 && ydisp !== ybase should trim lines and not change ydisp")]
    public void ReflowSmaller_FullScrollbackScrolledTrimsWithoutMovingDisplay()
    {
        using TerminalBuffer buffer = CreateSmallerMatrixBufferWithScrollback(10, 5, 13);
        buffer.Resize(2, 10, 10, CellStyle.Default);
        Assert.Equal(10, buffer.YBase);
        Assert.Equal(5, buffer.YDisp);
        Assert.Equal(20, buffer.LineCount);
    }

    [UpstreamFact("XTJS-0053", "Buffer buffer marked to have no scrollback should always have a scrollback of 0")]
    public void AlternateBuffer_AlwaysHasZeroScrollback()
    {
        using var buffer = new TerminalBuffer(TerminalBufferKind.Alternate, InitialColumns, InitialRows, InitialScrollback, CellStyle.Default);
        Assert.Equal(InitialRows, buffer.MaximumLineCount);
        buffer.Resize(InitialColumns, InitialRows * 2, InitialScrollback, CellStyle.Default);
        Assert.Equal(InitialRows * 2, buffer.MaximumLineCount);
        buffer.Resize(InitialColumns, InitialRows / 2, InitialScrollback, CellStyle.Default);
        Assert.Equal(InitialRows / 2, buffer.MaximumLineCount);
    }

    [UpstreamFact("XTJS-0054", "Buffer addMarker should adjust a marker line when the buffer is trimmed")]
    public void AddMarker_AdjustsLineWhenBufferIsTrimmed()
    {
        using TerminalBuffer buffer = CreateBuffer(scrollback: 0);
        TerminalMarker marker = buffer.AddMarker(buffer.LineCount - 1);
        buffer.NotifyTrim(1);
        Assert.Equal(buffer.LineCount - 2, marker.Line);
    }

    [UpstreamFact("XTJS-0055", "Buffer addMarker should dispose of a marker if it is trimmed off the buffer")]
    public void AddMarker_DisposesMarkerTrimmedOffBuffer()
    {
        using TerminalBuffer buffer = CreateBuffer(scrollback: 0);
        TerminalMarker marker = buffer.AddMarker(0);
        buffer.NotifyTrim(1);
        Assert.True(marker.IsDisposed);
        Assert.Empty(buffer.Markers);
    }

    [UpstreamFact("XTJS-0056", "Buffer addMarker should call onDispose")]
    public void AddMarker_RaisesDisposedEventWhenTrimmed()
    {
        using TerminalBuffer buffer = CreateBuffer(scrollback: 0);
        TerminalMarker marker = buffer.AddMarker(0);
        int calls = 0;
        marker.Disposed += (_, _) => calls++;
        buffer.NotifyTrim(1);
        Assert.Equal(1, calls);
    }

    [UpstreamFact("XTJS-0057", "Buffer translateBufferLineToString should handle selecting a section of ascii text")]
    public void TranslateBufferLineToString_SelectsAsciiSection()
    {
        using TerminalBuffer buffer = CreateBuffer(columns: 4);
        Write(buffer.GetLine(0), "abcd");
        Assert.Equal("ab", buffer.TranslateBufferLineToString(0, true, 0, 2));
    }

    [UpstreamFact("XTJS-0058", "Buffer translateBufferLineToString should handle a cut-off double width character by including it")]
    public void TranslateBufferLineToString_IncludesCutOffWideCharacter()
    {
        using TerminalBuffer buffer = CreateBuffer(columns: 3);
        SetWide(buffer.GetLine(0), 0, "語");
        Put(buffer.GetLine(0), 2, "a");
        Assert.Equal("語", buffer.TranslateBufferLineToString(0, true, 0, 1));
    }

    [UpstreamFact("XTJS-0059", "Buffer translateBufferLineToString should handle a zero width character in the middle of the string by not including it")]
    public void TranslateBufferLineToString_SkipsZeroWidthContinuationCell()
    {
        using TerminalBuffer buffer = CreateBuffer(columns: 3);
        SetWide(buffer.GetLine(0), 0, "語");
        Put(buffer.GetLine(0), 2, "a");
        Assert.Equal("語", buffer.TranslateBufferLineToString(0, true, 0, 1));
        Assert.Equal("語", buffer.TranslateBufferLineToString(0, true, 0, 2));
        Assert.Equal("語a", buffer.TranslateBufferLineToString(0, true, 0, 3));
    }

    [UpstreamFact("XTJS-0060", "Buffer translateBufferLineToString should handle single width emojis")]
    public void TranslateBufferLineToString_HandlesSingleWidthEmoji()
    {
        using TerminalBuffer buffer = CreateBuffer(columns: 2);
        buffer.GetLine(0).SetCell(0, CellData.FromText("😁", 1, CellStyle.Default));
        Put(buffer.GetLine(0), 1, "a");
        Assert.Equal("😁", buffer.TranslateBufferLineToString(0, true, 0, 1));
        Assert.Equal("😁a", buffer.TranslateBufferLineToString(0, true, 0, 2));
    }

    [UpstreamFact("XTJS-0061", "Buffer translateBufferLineToString should handle double width emojis")]
    public void TranslateBufferLineToString_HandlesDoubleWidthEmoji()
    {
        using TerminalBuffer buffer = CreateBuffer(columns: 3);
        SetWide(buffer.GetLine(0), 0, "😁");
        Put(buffer.GetLine(0), 2, "a");
        Assert.Equal("😁", buffer.TranslateBufferLineToString(0, true, 0, 1));
        Assert.Equal("😁", buffer.TranslateBufferLineToString(0, true, 0, 2));
        Assert.Equal("😁a", buffer.TranslateBufferLineToString(0, true, 0, 3));
    }

    [UpstreamFact("XTJS-0062", "Buffer line string cache cleanup should clear shared cache entries with a single timer")]
    public void LineStringCache_ClearsSharedEntriesWithSingleScheduledCleanup()
    {
        using TerminalBuffer buffer = CreateBuffer();
        Put(buffer.GetLine(0), 0, "a");
        Put(buffer.GetLine(1), 0, "b");
        buffer.TranslateBufferLineToString(0, false);
        buffer.TranslateBufferLineToString(1, false);
        Assert.Equal(2, buffer.StringCache.EntryCount);
        Assert.True(buffer.StringCache.IsCleanupScheduled);
        buffer.StringCache.Sweep(DateTimeOffset.UtcNow.AddSeconds(20));
        Assert.Equal(0, buffer.StringCache.EntryCount);
        Assert.False(buffer.StringCache.IsCleanupScheduled);
    }

    [UpstreamFact("XTJS-0063", "Buffer line string cache cleanup should reset line string cache state on clear and resize")]
    public void LineStringCache_ClearAndResizeResetCacheState()
    {
        using TerminalBuffer buffer = CreateBuffer();
        Put(buffer.GetLine(0), 0, "a");
        buffer.TranslateBufferLineToString(0, false);
        Assert.Equal(1, buffer.StringCache.EntryCount);
        buffer.Clear(CellStyle.Default);
        Assert.Equal(0, buffer.StringCache.EntryCount);
        buffer.TranslateBufferLineToString(0, false);
        Assert.Equal(1, buffer.StringCache.EntryCount);
        buffer.Resize(InitialColumns - 1, InitialRows, InitialScrollback, CellStyle.Default);
        Assert.Equal(0, buffer.StringCache.EntryCount);
        Assert.False(buffer.StringCache.IsCleanupScheduled);
    }

    [UpstreamFact("XTJS-0064", "Buffer memory cleanup after shrinking should realign memory from idle task execution")]
    public void MemoryCleanup_RealignsRetainedCapacityAfterShrink()
    {
        using TerminalBuffer buffer = CreateBuffer();
        buffer.Resize(InitialColumns / 2 - 1, InitialRows, InitialScrollback, CellStyle.Default, new BufferResizeOptions(true, 19000));
        Assert.All(buffer.Lines, line =>
        {
            Assert.Equal(InitialColumns / 2 - 1, line.Length);
            Assert.Equal(InitialColumns, line.AllocatedColumns);
        });
        buffer.CleanupMemory();
        Assert.All(buffer.Lines, line => Assert.Equal(line.Length, line.AllocatedColumns));
    }

    private static TerminalBuffer CreateBuffer(
        int columns = InitialColumns,
        int rows = InitialRows,
        int scrollback = InitialScrollback) =>
        new(TerminalBufferKind.Normal, columns, rows, scrollback, CellStyle.Default);

    private static TheoryData<string, bool[], int, int, int> CreateWrappedRangeCases()
    {
        bool[] None() => new bool[InitialRows];
        bool[] Wrapped(params int[] lines)
        {
            bool[] values = None();
            foreach (int line in lines)
            {
                values[line] = true;
            }
            return values;
        }
        var data = new TheoryData<string, bool[], int, int, int>();
        Add(data, "XTJS-0005", "Buffer getWrappedRangeForLine non-wrapped should return a single row for the first row", None(), 0, 0, 0);
        Add(data, "XTJS-0006", "Buffer getWrappedRangeForLine non-wrapped should return a single row for a middle row", None(), 12, 12, 12);
        Add(data, "XTJS-0007", "Buffer getWrappedRangeForLine non-wrapped should return a single row for the last row", None(), 23, 23, 23);
        Add(data, "XTJS-0008", "Buffer getWrappedRangeForLine wrapped should return a range for the first row", Wrapped(1), 0, 0, 1);
        Add(data, "XTJS-0009", "Buffer getWrappedRangeForLine wrapped should return a range for a middle row wrapping upwards", Wrapped(12), 12, 11, 12);
        Add(data, "XTJS-0010", "Buffer getWrappedRangeForLine wrapped should return a range for a middle row wrapping downwards", Wrapped(13), 12, 12, 13);
        Add(data, "XTJS-0011", "Buffer getWrappedRangeForLine wrapped should return a range for a middle row wrapping both ways", Wrapped(11, 12, 13, 14), 12, 10, 14);
        Add(data, "XTJS-0012", "Buffer getWrappedRangeForLine wrapped should return a range for the last row", Wrapped(23), 23, 22, 23);
        Add(data, "XTJS-0013", "Buffer getWrappedRangeForLine wrapped should return a range for a row that wraps upward to first row", Wrapped(1), 1, 0, 1);
        Add(data, "XTJS-0014", "Buffer getWrappedRangeForLine wrapped should return a range for a row that wraps downward to last row", Wrapped(23), 22, 22, 23);
        return data;
    }

    private static void Add(
        TheoryData<string, bool[], int, int, int> data,
        string id,
        string title,
        bool[] wrapped,
        int requested,
        int first,
        int last) =>
        data.Add(new TheoryDataRow<string, bool[], int, int, int>(id, wrapped, requested, first, last)
        {
            TestDisplayName = $"{id} {title}"
        });

    private static void AddBlankLines(TerminalBuffer buffer, int count)
    {
        for (int index = 0; index < count; index++)
        {
            buffer.Lines.Add(new BufferLine(buffer.Columns, CellStyle.Default, stringCache: buffer.StringCache));
        }
    }

    private static void Put(BufferLine line, int column, string text) =>
        line.SetCell(column, CellData.FromText(text, 1, CellStyle.Default));

    private static void Write(BufferLine line, string text)
    {
        for (int index = 0; index < text.Length && index < line.Length; index++)
        {
            Put(line, index, text[index].ToString());
        }
    }

    private static void SetWide(BufferLine line, int column, string text)
    {
        line.SetCell(column, CellData.FromText(text, 2, CellStyle.Default));
        if (column + 1 < line.Length)
        {
            line.SetCell(column + 1, new CellData { Width = 0, Style = CellStyle.Default });
        }
    }

    private static TerminalBuffer CreateThreePopulatedLines(int columns, int rows, int scrollback = InitialScrollback)
    {
        TerminalBuffer buffer = CreateBuffer(columns, rows, scrollback);
        Write(buffer.GetLine(0), "abcdefghij");
        Write(buffer.GetLine(1), "0123456789");
        Write(buffer.GetLine(2), "klmnopqrst");
        buffer.CursorY = Math.Min(3, rows - 1);
        return buffer;
    }

    private static TerminalBuffer CreateTabBoundaryBuffer()
    {
        TerminalBuffer buffer = CreateBuffer(4, 10);
        Write(buffer.GetLine(0), "ab");
        Write(buffer.GetLine(1), "cd");
        buffer.GetLine(1).IsWrapped = true;
        buffer.CursorY = 2;
        return buffer;
    }

    private static TerminalBuffer CreateWideWrappedBuffer()
    {
        TerminalBuffer buffer = CreateBuffer(12, 10);
        for (int row = 0; row < 2; row++)
        {
            for (int column = 0; column < 12; column += 4)
            {
                SetWide(buffer.GetLine(row), column, "汉");
                SetWide(buffer.GetLine(row), column + 2, "语");
            }
        }
        buffer.GetLine(1).IsWrapped = true;
        buffer.CursorY = 2;
        return buffer;
    }

    private static TerminalBuffer CreateLargerMatrixBuffer()
    {
        TerminalBuffer buffer = CreateBuffer(2, 10);
        string[] values = ["ab", "cd", "ef", "gh", "ij", "kl"];
        for (int index = 0; index < values.Length; index++)
        {
            Write(buffer.GetLine(index), values[index]);
            buffer.GetLine(index).IsWrapped = index is 1 or 3 or 5;
        }
        return buffer;
    }

    private static TerminalBuffer CreateLargerMatrixBufferWithScrollback(int scrollback, int display)
    {
        TerminalBuffer buffer = CreateLargerMatrixBuffer();
        for (int index = 0; index < 10; index++)
        {
            buffer.Lines.Insert(0, new BufferLine(2, CellStyle.Default, stringCache: buffer.StringCache));
        }
        buffer.CursorY = 9;
        buffer.YBase = 10;
        buffer.YDisp = display;
        return buffer;
    }

    private static TerminalBuffer CreateSmallerMatrixBuffer()
    {
        TerminalBuffer buffer = CreateBuffer(4, 10);
        Write(buffer.GetLine(0), "abcd");
        Write(buffer.GetLine(1), "efgh");
        Write(buffer.GetLine(2), "ijkl");
        return buffer;
    }

    private static TerminalBuffer CreateSmallerMatrixBufferWithScrollback(int scrollback, int display, int cursorY = 9)
    {
        TerminalBuffer buffer = CreateSmallerMatrixBuffer();
        for (int index = 0; index < 10; index++)
        {
            buffer.Lines.Insert(0, new BufferLine(4, CellStyle.Default, stringCache: buffer.StringCache));
        }
        buffer.CursorY = cursorY;
        buffer.YBase = 10;
        buffer.YDisp = display;
        return buffer;
    }

    private static void AssertLogicalLines(TerminalBuffer buffer, string[] expected)
    {
        Assert.Equal(expected, buffer.Lines.Take(expected.Length).Select(line => line.TranslateToString()));
    }
}

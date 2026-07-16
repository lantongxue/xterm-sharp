using XtermSharp.Internal;

namespace XtermSharp.Tests.Services;

public sealed class BufferServiceTests
{
    [Fact]
    public void Constructor_UsesSharedMinimumDimensions()
    {
        using BufferService service = CreateService(rows: 1, columns: 1, scrollback: 0);

        Assert.Equal(TerminalDimensions.MinimumColumns, service.Columns);
        Assert.Equal(TerminalDimensions.MinimumRows, service.Rows);
        Assert.Equal(TerminalDimensions.MinimumColumns, service.Buffer.Columns);
        Assert.Equal(TerminalDimensions.MinimumRows, service.Buffer.Rows);
    }

    [UpstreamFact("XTJS-1240", "BufferService scroll should decrement ydisp when the buffer is full and the user has scrolled up")]
    public void Scroll_DecrementsViewportWhenFullAndUserHasScrolledUp()
    {
        using BufferService service = CreateService(rows: 3, columns: 10, scrollback: 2);
        TerminalBuffer buffer = service.Buffer;
        while (buffer.LineCount < buffer.MaximumLineCount)
        {
            service.Scroll(CellStyle.Default);
        }

        Assert.Equal(5, buffer.LineCount);
        service.IsUserScrolling = true;
        buffer.YDisp = 2;
        int yBaseBefore = buffer.YBase;

        service.Scroll(CellStyle.Default);

        Assert.Equal(yBaseBefore, buffer.YBase);
        Assert.Equal(1, buffer.YDisp);
    }

    [UpstreamFact("XTJS-1241", "BufferService scroll should not advance ydisp with ybase while the user has scrolled up and the buffer is not full")]
    public void Scroll_KeepsViewportFixedWhenUserHasScrolledUpAndBufferIsNotFull()
    {
        using BufferService service = CreateService(rows: 3, columns: 10, scrollback: 2);
        TerminalBuffer buffer = service.Buffer;
        service.IsUserScrolling = true;
        buffer.YDisp = 0;
        int yBaseBefore = buffer.YBase;

        service.Scroll(CellStyle.Default);

        Assert.Equal(yBaseBefore + 1, buffer.YBase);
        Assert.Equal(0, buffer.YDisp);
    }

    [UpstreamFact("XTJS-1242", "BufferService scroll should follow ybase with ydisp when the user is not scrolling")]
    public void Scroll_FollowsBufferBaseWhenUserIsNotScrolling()
    {
        using BufferService service = CreateService(rows: 3, columns: 10, scrollback: 2);
        TerminalBuffer buffer = service.Buffer;
        while (buffer.LineCount < buffer.MaximumLineCount)
        {
            service.Scroll(CellStyle.Default);
        }

        service.IsUserScrolling = false;
        service.Scroll(CellStyle.Default);

        Assert.Equal(buffer.YBase, buffer.YDisp);
    }

    [UpstreamFact("XTJS-1243", "BufferService scroll should scroll within DECSTBM margins without affecting lines outside the region")]
    public void Scroll_OnlyMovesLinesWithinDecstbmMargins()
    {
        using BufferService service = CreateService(rows: 5, columns: 10, scrollback: 10);
        TerminalBuffer buffer = service.Buffer;
        MarkRow(buffer, 0, 'A');
        MarkRow(buffer, 1, 'B');
        MarkRow(buffer, 2, 'C');
        MarkRow(buffer, 3, 'D');
        MarkRow(buffer, 4, 'E');
        buffer.ScrollTop = 1;
        buffer.ScrollBottom = 3;

        service.Scroll(CellStyle.Default);

        Assert.Equal("A", GetRow(buffer, 0));
        Assert.Equal("C", GetRow(buffer, 1));
        Assert.Equal("D", GetRow(buffer, 2));
        Assert.Equal(string.Empty, GetRow(buffer, 3));
        Assert.Equal("E", GetRow(buffer, 4));
    }

    [UpstreamFact("XTJS-1244", "BufferService scrollLines should move ydisp and set isUserScrolling when scrolling up")]
    public void ScrollLines_MovesViewportAndEntersUserScrollingState()
    {
        using BufferService service = CreateService(rows: 10, columns: 80, scrollback: 10);
        TerminalBuffer buffer = service.Buffer;
        buffer.YBase = 5;
        buffer.YDisp = 5;
        int? reportedPosition = null;
        using IDisposable subscription = service.OnScroll(value => reportedPosition = value);

        service.ScrollLines(-2);

        Assert.Equal(3, buffer.YDisp);
        Assert.True(service.IsUserScrolling);
        Assert.Equal(3, reportedPosition);
    }

    [UpstreamFact("XTJS-1245", "BufferService scrollLines should not scroll above the top of the buffer")]
    public void ScrollLines_DoesNotMoveAboveTopOfBuffer()
    {
        using BufferService service = CreateService(rows: 10, columns: 80, scrollback: 10);
        TerminalBuffer buffer = service.Buffer;
        buffer.YBase = 5;
        buffer.YDisp = 0;

        service.ScrollLines(-1);

        Assert.Equal(0, buffer.YDisp);
        Assert.False(service.IsUserScrolling);
    }

    [UpstreamFact("XTJS-1246", "BufferService scrollLines should clear isUserScrolling when scrolling to the bottom")]
    public void ScrollLines_LeavesUserScrollingStateAtBottom()
    {
        using BufferService service = CreateService(rows: 10, columns: 80, scrollback: 10);
        TerminalBuffer buffer = service.Buffer;
        buffer.YBase = 5;
        buffer.YDisp = 2;
        service.IsUserScrolling = true;

        service.ScrollLines(10);

        Assert.Equal(5, buffer.YDisp);
        Assert.False(service.IsUserScrolling);
    }

    private static BufferService CreateService(int rows, int columns, int scrollback)
    {
        using var options = new OptionsService(new TerminalOptions
        {
            Rows = rows,
            Columns = columns,
            Scrollback = scrollback
        });
        return new BufferService(options);
    }

    private static void MarkRow(TerminalBuffer buffer, int row, char value) =>
        buffer.GetLine(buffer.YBase + row).SetCellFromCodePoint(0, value, 1, CellStyle.Default);

    private static string GetRow(TerminalBuffer buffer, int row) =>
        buffer.GetLine(buffer.YBase + row).TranslateToString(true).Trim();
}

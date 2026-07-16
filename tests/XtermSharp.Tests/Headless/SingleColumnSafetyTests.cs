namespace XtermSharp.Tests.Headless;

public sealed class SingleColumnSafetyTests
{
    [Fact]
    public async Task Constructor_PreservesRequestedOptionButUsesTwoEffectiveColumns()
    {
        await using var terminal = new Terminal(new TerminalOptions { Columns = 1, Rows = 2 });

        Assert.Equal(1, terminal.Options.Columns);
        Assert.Equal(2, terminal.Columns);

        await terminal.WriteAsync("文A", CancellationToken);
        TerminalSnapshot snapshot = await terminal.GetSnapshotAsync(cancellationToken: CancellationToken);
        Assert.Equal(2, snapshot.Columns);
        Assert.All(snapshot.ActiveBuffer.Lines, line => Assert.Equal(2, line.Cells.Length));
        Assert.Equal(2, snapshot.ActiveBuffer.Lines[0].Cells[0].Width);
        Assert.Equal(0, snapshot.ActiveBuffer.Lines[0].Cells[1].Width);
    }

    [Fact]
    public async Task Resize_ToOneColumnWithWideTextCompletesAtTwoColumnsAndRemainsUsable()
    {
        await using var terminal = new Terminal(new TerminalOptions { Columns = 4, Rows = 3 });
        var resizes = new List<(int Columns, int Rows)>();
        terminal.Resized += (_, args) => resizes.Add((args.Columns, args.Rows));
        await terminal.WriteAsync("文A", CancellationToken);

        await terminal.ResizeAsync(1, 3, CancellationToken).AsTask()
            .WaitAsync(TimeSpan.FromSeconds(2), CancellationToken);

        Assert.Equal(2, terminal.Columns);
        Assert.Equal([(2, 3)], resizes);
        TerminalSnapshot snapshot = await terminal.GetSnapshotAsync(cancellationToken: CancellationToken);
        Assert.All(snapshot.ActiveBuffer.Lines, line =>
        {
            Assert.Equal(2, line.Cells.Length);
            for (int column = 0; column < line.Cells.Length; column++)
            {
                if (line.Cells[column].Width == 2)
                {
                    Assert.True(column + 1 < line.Cells.Length);
                    Assert.Equal(0, line.Cells[column + 1].Width);
                }
                else if (line.Cells[column].Width == 0)
                {
                    Assert.True(column > 0);
                    Assert.Equal(2, line.Cells[column - 1].Width);
                }
                else
                {
                    Assert.Equal(1, line.Cells[column].Width);
                }
            }
        });

        await terminal.WriteAsync("B", CancellationToken);
        Assert.Equal(2, (await terminal.GetSnapshotAsync(cancellationToken: CancellationToken)).Columns);
    }

    [Fact]
    public async Task Resize_UsesRawRequestForNoOpCheckAndEffectiveSizeForEvent()
    {
        await using var terminal = new Terminal(new TerminalOptions { Columns = 2, Rows = 2 });
        var resizes = new List<(int Columns, int Rows)>();
        terminal.Resized += (_, args) => resizes.Add((args.Columns, args.Rows));

        await terminal.ResizeAsync(1, 2, CancellationToken);
        await terminal.ResizeAsync(2, 2, CancellationToken);

        Assert.Equal([(2, 2)], resizes);
    }

    [Fact]
    public async Task ZeroAndNegativeDimensionsRemainInvalidAndDoNotBreakTheQueue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Terminal(new TerminalOptions { Columns = 0 }));

        await using var terminal = new Terminal();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            terminal.ResizeAsync(0, 2, CancellationToken).AsTask());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            terminal.ResizeAsync(2, -1, CancellationToken).AsTask());
        await terminal.WriteAsync("ok", CancellationToken);
        Assert.Equal("ok", terminal.Buffer.Active.GetLine(0)!.TranslateToString(trimRight: true));
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;
}

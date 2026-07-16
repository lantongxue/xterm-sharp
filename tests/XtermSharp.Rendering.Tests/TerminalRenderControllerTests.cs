namespace XtermSharp.Rendering.Tests;

public sealed class TerminalRenderControllerTests
{
    [Fact]
    public async Task BuildsStyledDisplayCommandsAndReusesUnchangedRows()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using var terminal = new Terminal(new TerminalOptions { Columns = 4, Rows = 2 });
        using var controller = new TerminalRenderController(terminal, new FixedMetrics());
        await terminal.WriteAsync("\x1b[?25l\x1b[1;31;4mA", cancellationToken);

        TerminalRenderFrame first = await controller.PrepareFrameAsync(
            new TerminalViewport(40, 20),
            cancellationToken);
        TerminalDisplayRow firstRow = first.DisplayList.Rows[0];
        Assert.Contains(firstRow.Commands, command => command is TerminalTextCommand { Text: "A", Bold: true });
        Assert.Contains(firstRow.Commands, command => command is TerminalLineCommand);

        TerminalRenderFrame second = await controller.PrepareFrameAsync(
            new TerminalViewport(40, 20),
            cancellationToken);
        Assert.Same(firstRow, second.DisplayList.Rows[0]);
    }

    [Fact]
    public async Task SelectionCopiesWrappedLinesWithoutNewline()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using var terminal = new Terminal(new TerminalOptions { Columns = 4, Rows = 2 });
        using var controller = new TerminalRenderController(terminal, new FixedMetrics());
        await terminal.WriteAsync("abcdef", cancellationToken);
        controller.SetSelection(new TerminalSelection(0, 0, 2, 1));

        Assert.Equal("abcdef", await controller.GetSelectedTextAsync(cancellationToken));
    }

    [Fact]
    public async Task OscPaletteChangesAreAppliedByThePrimaryController()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using var terminal = new Terminal(new TerminalOptions { Columns = 2, Rows = 1 });
        using var controller = new TerminalRenderController(terminal, new FixedMetrics());
        await terminal.WriteAsync("\x1b]4;1;rgb:01/02/03\x07\x1b[31mA", cancellationToken);

        TerminalRenderFrame frame = await controller.PrepareFrameAsync(
            new TerminalViewport(20, 10),
            cancellationToken);

        Assert.Contains(
            frame.DisplayList.Rows[0].Commands,
            command => command is TerminalTextCommand
            {
                Text: "A",
                Color: { Red: 1, Green: 2, Blue: 3 }
            });
    }

    [Fact]
    public async Task SynchronizedOutputDefersFramesUntilTimeout()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using var terminal = new Terminal(new TerminalOptions { Columns = 3, Rows = 1 });
        using var controller = new TerminalRenderController(
            terminal,
            new FixedMetrics(),
            new TerminalRenderOptions { SynchronizedOutputTimeout = TimeSpan.FromMilliseconds(20) });
        TerminalViewport viewport = new(30, 10);
        TerminalRenderFrame initial = await controller.PrepareFrameAsync(viewport, cancellationToken);

        await terminal.WriteAsync("\x1b[?2026hA", cancellationToken);
        TerminalRenderFrame deferred = await controller.PrepareFrameAsync(viewport, cancellationToken);
        Assert.Same(initial, deferred);

        await Task.Delay(40, cancellationToken);
        TerminalRenderFrame flushed = await controller.PrepareFrameAsync(viewport, cancellationToken);
        Assert.True(flushed.Revision > initial.Revision);
    }

    private sealed class FixedMetrics : ITerminalFontMetricsProvider
    {
        public TerminalFontMetrics MeasureFont(TerminalRenderConfiguration configuration) =>
            new(10, 10, 8, 9, 1, 5);
    }
}

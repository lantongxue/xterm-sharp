using XtermSharp.Addons.Search.Tests.Support;

namespace XtermSharp.Addons.Search.Tests;

public sealed class SearchAddonTests
{
    private static readonly SearchDecorationOptions Decorations = new()
    {
        MatchBackground = TerminalColor.Rgb(255, 0, 0),
        MatchBorder = TerminalColor.Rgb(128, 0, 0),
        MatchOverviewRuler = TerminalColor.Rgb(255, 255, 0),
        ActiveMatchBackground = TerminalColor.Rgb(0, 0, 255),
        ActiveMatchBorder = TerminalColor.Rgb(0, 0, 128),
        ActiveMatchOverviewRuler = TerminalColor.Rgb(0, 255, 0)
    };

    [Fact]
    public async Task FindNextSelectsMatchesAndWrapsToTheFirstResult()
    {
        await using var terminal = CreateTerminal(20, 3);
        using SearchAddon addon = LoadAddon(terminal);
        await terminal.WriteAsync("one two one", TestContext.Current.CancellationToken);

        Assert.True(addon.FindNext("one"));
        AssertSelection(terminal, 0, 0, 3, 0);
        Assert.True(addon.FindNext("one"));
        AssertSelection(terminal, 8, 0, 11, 0);
        Assert.True(addon.FindNext("one"));
        AssertSelection(terminal, 0, 0, 3, 0);
    }

    [Fact]
    public async Task IncrementalSearchExpandsFromTheCurrentMatch()
    {
        await using var terminal = CreateTerminal(40, 3);
        using SearchAddon addon = LoadAddon(terminal);
        var options = new SearchOptions { Incremental = true };
        await terminal.WriteAsync(
            "package.lock pack package.json package.ups\r\npackage.jsonc",
            TestContext.Current.CancellationToken);

        Assert.True(addon.FindNext("pack", options));
        AssertSelection(terminal, 0, 0, 4, 0);
        Assert.True(addon.FindNext("package.j", options));
        AssertSelection(terminal, 18, 0, 27, 0);
        Assert.True(addon.FindNext("package.jsonc", options));
        AssertSelection(terminal, 0, 2, 13, 2);
    }

    [Fact]
    public async Task SupportsCaseSensitiveWholeWordAndRegexSearches()
    {
        await using var terminal = CreateTerminal(40, 2);
        using SearchAddon addon = LoadAddon(terminal);
        await terminal.WriteAsync("Alpha alphabet ALPHA 123", TestContext.Current.CancellationToken);

        Assert.True(addon.FindNext("alpha", new SearchOptions { WholeWord = true }));
        AssertSelection(terminal, 0, 0, 5, 0);
        Assert.True(addon.FindNext("ALPHA", new SearchOptions { CaseSensitive = true }));
        AssertSelection(terminal, 15, 0, 20, 0);
        terminal.ClearSelection();
        Assert.True(addon.FindNext("[0-9]+", new SearchOptions { Regex = true }));
        AssertSelection(terminal, 21, 0, 24, 0);
        Assert.ThrowsAny<ArgumentException>(() =>
            addon.FindNext("[invalid", new SearchOptions { Regex = true }));
    }

    [Fact]
    public async Task MapsWideCharactersAndUtf16SurrogatesToBufferColumns()
    {
        await using var terminal = CreateTerminal(20, 2);
        using SearchAddon addon = LoadAddon(terminal);
        await terminal.WriteAsync("中文xx𝄞𝄞", TestContext.Current.CancellationToken);

        Assert.True(addon.FindNext("中"));
        AssertSelection(terminal, 0, 0, 2, 0);
        Assert.True(addon.FindNext("xx"));
        AssertSelection(terminal, 4, 0, 6, 0);
        Assert.True(addon.FindNext("𝄞"));
        AssertSelection(terminal, 6, 0, 7, 0);
        Assert.True(addon.FindNext("𝄞"));
        AssertSelection(terminal, 7, 0, 8, 0);
    }

    [Fact]
    public async Task HandlesWideCharactersThatWrapBeforeTheRightMargin()
    {
        await using var terminal = CreateTerminal(3, 2);
        using SearchAddon addon = LoadAddon(terminal);
        await terminal.WriteAsync("aa中", TestContext.Current.CancellationToken);

        Assert.True(addon.FindNext("中"));
        AssertSelection(terminal, 0, 1, 2, 1);
    }

    [Fact]
    public async Task WrappedMatchesSplitDecorationsAcrossRows()
    {
        await using var terminal = CreateTerminal(4, 2);
        using SearchAddon addon = LoadAddon(terminal);
        await terminal.WriteAsync("xxabc", TestContext.Current.CancellationToken);

        Assert.True(addon.FindNext("xabc", new SearchOptions { Decorations = Decorations }));
        AssertSelection(terminal, 1, 0, 1, 1);
        TerminalDecoration[] highlights = terminal.Decorations
            .Where(decoration => decoration.Layer == TerminalDecorationLayer.Bottom)
            .ToArray();
        Assert.Equal(2, highlights.Length);
        Assert.Equal(new TerminalDecorationRange(1, 0, 3), highlights[0].Range);
        Assert.Equal(new TerminalDecorationRange(0, 1, 1), highlights[1].Range);
    }

    [Fact]
    public async Task ResultEventsReportCountAndActiveIndex()
    {
        await using var terminal = CreateTerminal(20, 2);
        using SearchAddon addon = LoadAddon(terminal);
        var events = new List<(int Index, int Count)>();
        addon.ResultsChanged += (_, args) => events.Add((args.ResultIndex, args.ResultCount));
        await terminal.WriteAsync("abc bc c", TestContext.Current.CancellationToken);
        var options = new SearchOptions { Decorations = Decorations };

        Assert.True(addon.FindNext("c", options));
        Assert.True(addon.FindNext("c", options));
        Assert.True(addon.FindNext("c", options));

        Assert.Equal([(0, 3), (1, 3), (2, 3)], events);
    }

    [Fact]
    public async Task HighlightLimitCapsTrackedAndDecoratedResults()
    {
        await using var terminal = CreateTerminal(40, 2);
        using var addon = new SearchAddon(new SearchAddonOptions { HighlightLimit = 5 });
        terminal.LoadAddon(addon);
        SearchResultChangedEventArgs? resultEvent = null;
        addon.ResultsChanged += (_, args) => resultEvent = args;
        await terminal.WriteAsync(string.Concat(Enumerable.Repeat("a ", 10)), TestContext.Current.CancellationToken);

        Assert.True(addon.FindNext("a", new SearchOptions { Decorations = Decorations }));

        Assert.NotNull(resultEvent);
        Assert.Equal(5, resultEvent.ResultCount);
        Assert.Equal(0, resultEvent.ResultIndex);
        Assert.Equal(5, terminal.Decorations.Count(decoration => decoration.Layer == TerminalDecorationLayer.Bottom));
    }

    [Fact]
    public async Task DecoratedSearchRecomputesAfterWritesWithDebounce()
    {
        await using var terminal = CreateTerminal(20, 5);
        using SearchAddon addon = LoadAddon(terminal);
        var counts = new List<int>();
        addon.ResultsChanged += (_, args) => counts.Add(args.ResultCount);
        await terminal.WriteAsync("abc\r\nabc", TestContext.Current.CancellationToken);
        SearchOptions options = new() { Decorations = Decorations };
        Assert.True(addon.FindNext("abc", options));

        await terminal.WriteAsync("\r\nabc", TestContext.Current.CancellationToken);
        await Task.Delay(350, TestContext.Current.CancellationToken);

        Assert.Equal([2, 3], counts);
        AssertSelection(terminal, 0, 0, 3, 0);
    }

    [Fact]
    public async Task BeforeAndAfterEventsBracketSearchAndActiveDecorationCanBeCleared()
    {
        await using var terminal = CreateTerminal(10, 2);
        using SearchAddon addon = LoadAddon(terminal);
        var calls = new List<string>();
        addon.BeforeSearch += (_, _) => calls.Add("before");
        addon.AfterSearch += (_, _) => calls.Add("after");
        await terminal.WriteAsync("abc", TestContext.Current.CancellationToken);

        Assert.True(addon.FindNext("a", new SearchOptions { Decorations = Decorations }));
        Assert.Equal(["before", "after"], calls);
        Assert.Single(terminal.Decorations, decoration => decoration.Layer == TerminalDecorationLayer.Top);

        addon.ClearActiveDecoration();

        Assert.DoesNotContain(terminal.Decorations, decoration => decoration.Layer == TerminalDecorationLayer.Top);
        Assert.NotNull(terminal.Selection);
    }

    [Fact]
    public async Task RendererOrdersBottomHighlightSelectionAndActiveHighlight()
    {
        await using var terminal = CreateTerminal(4, 1);
        using SearchAddon addon = LoadAddon(terminal);
        await terminal.WriteAsync("\x1b[?25lA", TestContext.Current.CancellationToken);
        Assert.True(addon.FindNext("A", new SearchOptions { Decorations = Decorations }));
        using var controller = new TerminalRenderController(terminal, new FixedMetrics());

        TerminalRenderFrame frame = await controller.PrepareFrameAsync(
            new TerminalViewport(40, 10),
            TestContext.Current.CancellationToken);
        TerminalFillRectangleCommand[] fills = frame.DisplayList.Rows[0].Commands
            .OfType<TerminalFillRectangleCommand>()
            .ToArray();

        Assert.Equal(4, fills.Length);
        Assert.Equal(new TerminalRgbaColor(255, 0, 0), fills[1].Color);
        Assert.Equal(new TerminalRgbaColor(255, 255, 255, 76), fills[2].Color);
        Assert.Equal(new TerminalRgbaColor(0, 0, 255), fills[3].Color);
        Assert.Equal(2, frame.DisplayList.Rows[0].Commands.OfType<TerminalStrokeRectangleCommand>().Count());
    }

    [Fact]
    public async Task Issue2444FindsEveryWrappedOccurrenceInBothDirections()
    {
        string fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "issue-2444");
        string fixture = await File.ReadAllTextAsync(fixturePath, TestContext.Current.CancellationToken);
        if (!OperatingSystem.IsWindows())
        {
            fixture = fixture.Replace("\n", "\n\r", StringComparison.Ordinal);
        }
        await using var terminal = CreateTerminal(80, 24);
        using SearchAddon addon = LoadAddon(terminal);
        await terminal.WriteAsync(fixture, TestContext.Current.CancellationToken);
        (int Column, int Row)[] forward =
        [
            (24, 53), (24, 76), (24, 96), (1, 114), (11, 115),
            (1, 126), (11, 127), (1, 135), (11, 136), (24, 53)
        ];
        foreach ((int column, int row) in forward)
        {
            Assert.True(addon.FindNext("opencv"));
            AssertSelection(terminal, column, row, column + 6, row);
        }

        terminal.ClearSelection();
        (int Column, int Row)[] reverse =
        [
            (11, 136), (1, 135), (11, 127), (1, 126), (11, 115),
            (1, 114), (24, 96), (24, 76), (24, 53), (11, 136)
        ];
        foreach ((int column, int row) in reverse)
        {
            Assert.True(addon.FindPrevious("opencv"));
            AssertSelection(terminal, column, row, column + 6, row);
        }
    }

    private static Terminal CreateTerminal(int columns, int rows) =>
        new(new TerminalOptions { Columns = columns, Rows = rows });

    private static SearchAddon LoadAddon(Terminal terminal)
    {
        var addon = new SearchAddon();
        terminal.LoadAddon(addon);
        return addon;
    }

    private static void AssertSelection(
        Terminal terminal,
        int startColumn,
        int startLine,
        int endColumn,
        int endLine) =>
        Assert.Equal(
            new TerminalSelectionRange(startColumn, startLine, endColumn, endLine),
            terminal.Selection);
}

using XtermSharp.Links;

namespace XtermSharp.Tests.Headless;

public sealed class OscHyperlinkTests
{
    [Fact]
    public async Task SnapshotExposesImmutableUriIdAndParameters()
    {
        await using var terminal = CreateTerminal(20, 3);
        await terminal.WriteAsync(
            "\x1b]8;id=alpha:foo=bar:baz=quux;https://example.com/path\x1b\\label\x1b]8;;\x1b\\",
            TestContext.Current.CancellationToken);

        TerminalSnapshot snapshot = await terminal.GetSnapshotAsync(
            SnapshotScope.ActiveBuffer,
            TestContext.Current.CancellationToken);
        int linkId = snapshot.ActiveBuffer.Lines[0].Cells[0].HyperlinkId;
        TerminalHyperlinkMetadata metadata = Assert.IsType<TerminalHyperlinkMetadata>(snapshot.GetHyperlink(linkId));

        Assert.Equal("https://example.com/path", metadata.Uri);
        Assert.Equal("alpha", metadata.Id);
        Assert.Equal("alpha", metadata.GetParameter("id"));
        Assert.Equal("bar", metadata.GetParameter("foo"));
        Assert.Equal("quux", metadata.GetParameter("baz"));
        Assert.Equal(["id", "foo", "baz"], metadata.Parameters.Select(parameter => parameter.Name));

        await terminal.ResetAsync(TestContext.Current.CancellationToken);
        TerminalSnapshot afterReset = terminal.GetCurrentSnapshot(SnapshotScope.ActiveBuffer);

        Assert.Empty(afterReset.Hyperlinks);
        Assert.Equal(metadata, snapshot.GetHyperlink(linkId));
    }

    [Fact]
    public async Task SnapshotScopeIncludesOnlyHyperlinksReferencedByItsLines()
    {
        await using var terminal = CreateTerminal(8, 2, scrollback: 4);
        await terminal.WriteAsync(
            "\x1b]8;id=old;https://example.com\x1b\\old\x1b]8;;\x1b\\\r\nline1\r\nline2",
            TestContext.Current.CancellationToken);

        TerminalSnapshot viewport = await terminal.GetSnapshotAsync(
            SnapshotScope.Viewport,
            TestContext.Current.CancellationToken);
        TerminalSnapshot activeBuffer = await terminal.GetSnapshotAsync(
            SnapshotScope.ActiveBuffer,
            TestContext.Current.CancellationToken);

        Assert.Empty(viewport.Hyperlinks);
        Assert.Single(activeBuffer.Hyperlinks);
        Assert.Equal("https://example.com", Assert.Single(activeBuffer.Hyperlinks).Value.Uri);
    }

    [Fact]
    public async Task ExplicitLinksReuseIdsWhileAnonymousLinksRemainDistinct()
    {
        await using var terminal = CreateTerminal(20, 3);
        await terminal.WriteAsync(
            "\x1b]8;id=same;https://one.example\x1b\\A\x1b]8;;\x1b\\ " +
            "\x1b]8;id=same;https://one.example\x1b\\B\x1b]8;;\x1b\\ " +
            "\x1b]8;id=same;https://two.example\x1b\\C\x1b]8;;\x1b\\ " +
            "\x1b]8;;https://one.example\x1b\\D\x1b]8;;\x1b\\ " +
            "\x1b]8;;https://one.example\x1b\\E\x1b]8;;\x1b\\",
            TestContext.Current.CancellationToken);
        TerminalSnapshot snapshot = terminal.GetCurrentSnapshot(SnapshotScope.ActiveBuffer);
        TerminalLineSnapshot line = snapshot.ActiveBuffer.Lines[0];
        int first = line.Cells[0].HyperlinkId;
        int second = line.Cells[2].HyperlinkId;
        int differentUri = line.Cells[4].HyperlinkId;
        int firstAnonymous = line.Cells[6].HyperlinkId;
        int secondAnonymous = line.Cells[8].HyperlinkId;

        Assert.Equal(first, second);
        Assert.Equal(4, new[] { first, differentUri, firstAnonymous, secondAnonymous }.Distinct().Count());
        Assert.Equal(4, snapshot.Hyperlinks.Count);
        Assert.Null(snapshot.GetHyperlink(firstAnonymous)!.Id);
        IReadOnlyList<TerminalLink> links = await terminal.GetLinksAsync(
            1,
            TestContext.Current.CancellationToken);
        Assert.Equal(5, links.Count);
        Assert.Equal(new TerminalLinkPosition(1, 1), links[0].Range.Start);
        Assert.Equal(new TerminalLinkPosition(3, 1), links[1].Range.Start);
        Assert.Equal(links[0].Hyperlink!.LinkId, links[1].Hyperlink!.LinkId);
    }

    [Fact]
    public async Task MetadataTracksNormalAndAlternateBufferLifetime()
    {
        await using var terminal = CreateTerminal(10, 3);
        await terminal.WriteAsync(
            "\x1b]8;id=normal;https://normal.example\x1b\\N\x1b]8;;\x1b\\",
            TestContext.Current.CancellationToken);
        int normalId = terminal.GetCurrentSnapshot(SnapshotScope.ActiveBuffer).ActiveBuffer.Lines[0].Cells[0].HyperlinkId;

        await terminal.WriteAsync(
            "\x1b[?1049h\x1b]8;id=alternate;https://alternate.example\x1b\\A\x1b]8;;\x1b\\",
            TestContext.Current.CancellationToken);
        TerminalSnapshot alternate = terminal.GetCurrentSnapshot(SnapshotScope.ActiveBuffer);
        int alternateId = alternate.ActiveBuffer.Lines[0].Cells[0].HyperlinkId;
        TerminalSnapshot both = terminal.GetCurrentSnapshot(SnapshotScope.AllBuffers);

        Assert.Single(alternate.Hyperlinks);
        Assert.NotEqual(normalId, alternateId);
        Assert.Equal(2, both.Hyperlinks.Count);

        await terminal.WriteAsync("\x1b[?1049l", TestContext.Current.CancellationToken);
        TerminalSnapshot normal = terminal.GetCurrentSnapshot(SnapshotScope.AllBuffers);

        Assert.Equal(TerminalBufferKind.Normal, normal.ActiveBuffer.Kind);
        Assert.NotNull(normal.NormalBuffer);
        Assert.Equal("N", normal.NormalBuffer.Lines[0].Cells[0].Text);
        Assert.Single(normal.Hyperlinks);
        Assert.NotNull(normal.GetHyperlink(normalId));
        Assert.Null(normal.GetHyperlink(alternateId));
    }

    [Fact]
    public async Task BuiltInProviderMapsWrappedRangeAndRaisesSafeApplicationEvents()
    {
        await using var terminal = CreateTerminal(4, 4, scrollback: 4);
        var events = new List<string>();
        terminal.HyperlinkHovered += (_, args) => events.Add($"hover:{args.Hyperlink.Uri}");
        terminal.HyperlinkLeft += (_, args) => events.Add($"leave:{args.Hyperlink.Uri}");
        terminal.HyperlinkActivated += (_, args) => events.Add($"activate:{args.Hyperlink.Uri}");
        await terminal.WriteAsync(
            "\x1b]8;id=wrapped;custom+safe://target\x1b\\ABCDEFG\x1b]8;;\x1b\\\r\nZ",
            TestContext.Current.CancellationToken);

        TerminalLink link = Assert.IsType<TerminalLink>(await terminal.GetLinkAtAsync(
            2,
            2,
            TestContext.Current.CancellationToken));
        Assert.Equal(
            new TerminalLinkRange(new TerminalLinkPosition(1, 1), new TerminalLinkPosition(3, 2)),
            link.Range);
        Assert.Equal("custom+safe://target", link.Text);
        Assert.Equal("wrapped", link.Hyperlink!.Id);
        var terminalEvent = new TerminalLinkEvent(
            2,
            2,
            10,
            20,
            TerminalMouseButton.Left,
            TerminalMouseAction.Up);

        link.Hover?.Invoke(terminalEvent, link.Text);
        link.Leave?.Invoke(terminalEvent, link.Text);
        link.Activate(terminalEvent, link.Text);

        Assert.Equal(
            ["hover:custom+safe://target", "leave:custom+safe://target", "activate:custom+safe://target"],
            events);

        await terminal.ResizeAsync(8, 4, TestContext.Current.CancellationToken);
        TerminalLink grown = Assert.IsType<TerminalLink>(await terminal.GetLinkAtAsync(
            7,
            1,
            TestContext.Current.CancellationToken));
        Assert.Equal(
            new TerminalLinkRange(new TerminalLinkPosition(1, 1), new TerminalLinkPosition(7, 1)),
            grown.Range);
    }

    [Fact]
    public async Task TrimmedHyperlinkDisappearsWithoutMutatingOlderSnapshot()
    {
        await using var terminal = CreateTerminal(4, 2, scrollback: 0);
        await terminal.WriteAsync(
            "\x1b]8;id=trim;https://example.com\x1b\\AB\x1b]8;;\x1b\\",
            TestContext.Current.CancellationToken);
        TerminalSnapshot before = terminal.GetCurrentSnapshot(SnapshotScope.ActiveBuffer);
        int linkId = before.ActiveBuffer.Lines[0].Cells[0].HyperlinkId;

        await terminal.WriteAsync("\r\nx\r\ny", TestContext.Current.CancellationToken);
        TerminalSnapshot after = terminal.GetCurrentSnapshot(SnapshotScope.ActiveBuffer);

        Assert.Empty(after.Hyperlinks);
        Assert.Equal("https://example.com", before.GetHyperlink(linkId)!.Uri);
    }

    private static Terminal CreateTerminal(int columns, int rows, int scrollback = 1000) =>
        new(new TerminalOptions { Columns = columns, Rows = rows, Scrollback = scrollback });
}

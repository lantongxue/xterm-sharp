using System.Text.RegularExpressions;
using XtermSharp.Addons.WebLinks.Tests.Support;

namespace XtermSharp.Addons.WebLinks.Tests;

public sealed class WebLinksAddonTests
{
    private static readonly string[] CountryTlds =
    [
        ".ac", ".ad", ".ae", ".af", ".ag", ".ai", ".al", ".am", ".ao", ".aq", ".ar", ".as", ".at",
        ".au", ".aw", ".ax", ".az", ".ba", ".bb", ".bd", ".be", ".bf", ".bg", ".bh", ".bi", ".bj",
        ".bm", ".bn", ".bo", ".bq", ".br", ".bs", ".bt", ".bw", ".by", ".bz", ".ca", ".cc", ".cd",
        ".cf", ".cg", ".ch", ".ci", ".ck", ".cl", ".cm", ".cn", ".co", ".cr", ".cu", ".cv", ".cw",
        ".cx", ".cy", ".cz", ".de", ".dj", ".dk", ".dm", ".do", ".dz", ".ec", ".ee", ".eg", ".eh",
        ".er", ".es", ".et", ".eu", ".fi", ".fj", ".fk", ".fm", ".fo", ".fr", ".ga", ".gd", ".ge",
        ".gf", ".gg", ".gh", ".gi", ".gl", ".gm", ".gn", ".gp", ".gq", ".gr", ".gs", ".gt", ".gu",
        ".gw", ".gy", ".hk", ".hm", ".hn", ".hr", ".ht", ".hu", ".id", ".ie", ".il", ".im", ".in",
        ".io", ".iq", ".ir", ".is", ".it", ".je", ".jm", ".jo", ".jp", ".ke", ".kg", ".kh", ".ki",
        ".km", ".kn", ".kp", ".kr", ".kw", ".ky", ".kz", ".la", ".lb", ".lc", ".li", ".lk", ".lr",
        ".ls", ".lt", ".lu", ".lv", ".ly", ".ma", ".mc", ".md", ".me", ".mg", ".mh", ".mk", ".ml",
        ".mm", ".mn", ".mo", ".mp", ".mq", ".mr", ".ms", ".mt", ".mu", ".mv", ".mw", ".mx", ".my",
        ".mz", ".na", ".nc", ".ne", ".nf", ".ng", ".ni", ".nl", ".no", ".np", ".nr", ".nu", ".nz",
        ".om", ".pa", ".pe", ".pf", ".pg", ".ph", ".pk", ".pl", ".pm", ".pn", ".pr", ".ps", ".pt",
        ".pw", ".py", ".qa", ".re", ".ro", ".rs", ".ru", ".rw", ".sa", ".sb", ".sc", ".sd", ".se",
        ".sg", ".sh", ".si", ".sk", ".sl", ".sm", ".sn", ".so", ".sr", ".ss", ".st", ".su", ".sv",
        ".sx", ".sy", ".sz", ".tc", ".td", ".tf", ".tg", ".th", ".tj", ".tk", ".tl", ".tm", ".tn",
        ".to", ".tr", ".tt", ".tv", ".tw", ".tz", ".ua", ".ug", ".uk", ".us", ".uy", ".uz", ".va",
        ".vc", ".ve", ".vg", ".vi", ".vn", ".vu", ".wf", ".ws", ".ye", ".yt", ".za", ".zm", ".zw"
    ];

    [Fact]
    public void DefaultMatcherAcceptsAllUpstreamCountryTlds()
    {
        foreach (string tld in CountryTlds)
        {
            AssertDefaultMatch($"foo{tld}");
            AssertDefaultMatch($"foo.com{tld}");
        }
        AssertDefaultMatch("foo.com");
    }

    [Fact]
    public async Task StrictMatcherPreservesUpstreamFinalPunctuationRules()
    {
        await using var terminal = CreateTerminal(80, 7);
        using var addon = LoadAddon(terminal);
        await terminal.WriteAsync(
            "  http://foo.com  \r\n" +
            "  http://foo.com/a~b#c~d?e~f  \r\n" +
            "  http://foo.com/colon:test  \r\n" +
            "  http://foo.com/colon:test:  \r\n" +
            "\"http://foo.com/\"\r\n" +
            "'http://foo.com/'\r\n" +
            "http://foo.com/subpath/+/id",
            TestContext.Current.CancellationToken);

        await AssertLinkAtAsync(terminal, 3, 0, "http://foo.com");
        await AssertLinkAtAsync(terminal, 3, 1, "http://foo.com/a~b#c~d?e~f");
        await AssertLinkAtAsync(terminal, 3, 2, "http://foo.com/colon:test");
        await AssertLinkAtAsync(terminal, 3, 3, "http://foo.com/colon:test");
        await AssertLinkAtAsync(terminal, 2, 4, "http://foo.com/");
        await AssertLinkAtAsync(terminal, 2, 5, "http://foo.com/");
        await AssertLinkAtAsync(terminal, 1, 6, "http://foo.com/subpath/+/id");
    }

    [Fact]
    public async Task ComputesHalfWidthWrappedRanges()
    {
        await using var terminal = CreateTerminal(40, 4);
        using var addon = LoadAddon(terminal);
        await terminal.WriteAsync(
            "aaa http://example.com aaa http://example.com aaa",
            TestContext.Current.CancellationToken);

        TerminalLink first = await GetLinkAtAsync(terminal, 5, 0);
        AssertLink(first, "http://example.com", 5, 1, 22, 1);
        TerminalLink second = await GetLinkAtAsync(terminal, 1, 1);
        AssertLink(second, "http://example.com", 28, 1, 5, 2);
    }

    [Fact]
    public async Task ComputesRangesAfterWideCharacters()
    {
        await using var terminal = CreateTerminal(40, 4);
        using var addon = LoadAddon(terminal);
        await terminal.WriteAsync(
            "￥￥￥ http://example.com ￥￥￥ http://example.com aaa",
            TestContext.Current.CancellationToken);

        TerminalLink first = await GetLinkAtAsync(terminal, 8, 0);
        AssertLink(first, "http://example.com", 8, 1, 25, 1);
        TerminalLink second = await GetLinkAtAsync(terminal, 1, 1);
        AssertLink(second, "http://example.com", 34, 1, 11, 2);
    }

    [Fact]
    public async Task ComputesRangesWithWideCharactersInsideUrls()
    {
        await using var terminal = CreateTerminal(40, 5);
        using var addon = LoadAddon(terminal);
        const string uri = "https://ko.wikipedia.org/wiki/위키백과:대문";
        await terminal.WriteAsync(
            $"￥￥￥ {uri} aaa {uri} ￥￥￥",
            TestContext.Current.CancellationToken);

        TerminalLink first = await GetLinkAtAsync(terminal, 8, 0);
        AssertLink(first, uri, 8, 1, 11, 2);
        AssertLink(await GetLinkAtAsync(terminal, 1, 1), uri, 8, 1, 11, 2);
        TerminalLink second = await GetLinkAtAsync(terminal, 17, 1);
        AssertLink(second, uri, 17, 2, 19, 3);
    }

    [Theory]
    [InlineData("￥￥￥cafe\u0301 http://test:password@example.com/some_path", "http://test:password@example.com/some_path", 13)]
    [InlineData("￥￥￥cafe\u0301 http://test:password@example.com/some_path?param=1%202%3", "http://test:password@example.com/some_path?param=1%202%3", 27)]
    public async Task ComputesRangesAfterCombiningCharacters(string input, string uri, int endColumn)
    {
        await using var terminal = CreateTerminal(40, 4);
        using var addon = LoadAddon(terminal);
        await terminal.WriteAsync(input, TestContext.Current.CancellationToken);

        TerminalLink link = await GetLinkAtAsync(terminal, 12, 0);
        AssertLink(link, uri, 12, 1, endColumn, 2);
        AssertLink(await GetLinkAtAsync(terminal, 5, 1), uri, 12, 1, endColumn, 2);
    }

    [Fact]
    public async Task AcceptsUppercaseProtocolHostAndDefaultPorts()
    {
        await using var terminal = CreateTerminal(40, 6);
        using var addon = LoadAddon(terminal);
        string[] expected =
        [
            "HTTP://EXAMPLE.COM",
            "HTTPS://Example.com",
            "HTTP://Example.com:80",
            "HTTP://Example.com:80/staysUpper",
            "HTTP://Ab:xY@abc.com:80/staysUpper"
        ];
        await terminal.WriteAsync(
            string.Join("  \r\n", expected.Select(value => $"  {value}")),
            TestContext.Current.CancellationToken);

        for (int index = 0; index < expected.Length; index++)
        {
            await AssertLinkAtAsync(terminal, 3, index, expected[index]);
        }
    }

    [Fact]
    public async Task CustomRegexAndCallbacksAreApplied()
    {
        var callbacks = new List<string>();
        var options = new WebLinkProviderOptions
        {
            UrlRegex = new Regex(@"https://custom\.test/[a-z]+", RegexOptions.CultureInvariant),
            Hover = (_, text, range) => callbacks.Add($"hover:{text}:{range.Start.X}"),
            Leave = (_, text) => callbacks.Add($"leave:{text}")
        };
        await using var terminal = CreateTerminal(40, 2);
        using var addon = new WebLinksAddon((_, text) => callbacks.Add($"activate:{text}"), options);
        terminal.LoadAddon(addon);
        await terminal.WriteAsync("https://custom.test/path https://ignored.test", TestContext.Current.CancellationToken);

        TerminalLink link = await GetLinkAtAsync(terminal, 1, 0);
        var terminalEvent = new TerminalLinkEvent(
            1,
            1,
            0,
            0,
            TerminalMouseButton.Left,
            TerminalMouseAction.Up);
        link.Hover?.Invoke(terminalEvent, link.Text);
        link.Leave?.Invoke(terminalEvent, link.Text);
        link.Activate(terminalEvent, link.Text);

        Assert.Equal(
            [
                "hover:https://custom.test/path:1",
                "leave:https://custom.test/path",
                "activate:https://custom.test/path"
            ],
            callbacks);
        Assert.Null(await terminal.GetLinkAtAsync(30, 1, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DisposingAddonUnregistersItsProvider()
    {
        await using var terminal = CreateTerminal(40, 2);
        var addon = LoadAddon(terminal);
        await terminal.WriteAsync("http://example.com", TestContext.Current.CancellationToken);
        Assert.NotNull(await terminal.GetLinkAtAsync(1, 1, TestContext.Current.CancellationToken));

        addon.Dispose();

        Assert.Null(await terminal.GetLinkAtAsync(1, 1, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LinkProvidersUseRegistrationOrderAndDisposableRegistration()
    {
        await using var terminal = CreateTerminal(40, 2);
        var fixedLink = new TerminalLink(
            new TerminalLinkRange(new TerminalLinkPosition(1, 1), new TerminalLinkPosition(18, 1)),
            "first-provider",
            static (_, _) => { });
        using IDisposable registration = terminal.RegisterLinkProvider(new FixedLinkProvider(fixedLink));
        using var addon = LoadAddon(terminal);
        await terminal.WriteAsync("http://example.com", TestContext.Current.CancellationToken);

        Assert.Same(fixedLink, await terminal.GetLinkAtAsync(1, 1, TestContext.Current.CancellationToken));
        registration.Dispose();
        TerminalLink webLink = await GetLinkAtAsync(terminal, 0, 0);
        Assert.Equal("http://example.com", webLink.Text);
    }

    [Fact]
    public async Task RendererUnderlinesOnlyTheHoveredLinkRange()
    {
        await using var terminal = CreateTerminal(20, 1);
        using var addon = LoadAddon(terminal);
        await terminal.WriteAsync("\x1b[?25lx http://a.co y", TestContext.Current.CancellationToken);
        TerminalLink link = await GetLinkAtAsync(terminal, 4, 0);
        using var controller = new TerminalRenderController(terminal, new FixedMetrics());
        var viewport = new TerminalViewport(200, 10);

        TerminalRenderFrame plain = await controller.PrepareFrameAsync(
            viewport,
            TestContext.Current.CancellationToken);
        Assert.Empty(plain.DisplayList.Rows[0].Commands.OfType<TerminalLineCommand>());

        controller.SetHoveredLink(link.Range);
        TerminalRenderFrame hovered = await controller.PrepareFrameAsync(
            viewport,
            TestContext.Current.CancellationToken);
        TerminalLineCommand[] underlines = hovered.DisplayList.Rows[0].Commands
            .OfType<TerminalLineCommand>()
            .ToArray();
        Assert.Equal(link.Text.Length, underlines.Length);
        Assert.All(underlines, command => Assert.Equal(TerminalUnderlineStyle.Single, command.Style));
        Assert.Equal(20, underlines[0].Rectangle.X);
        Assert.Equal(130, underlines[^1].Rectangle.Right);

        controller.SetHoveredLink(null);
        TerminalRenderFrame cleared = await controller.PrepareFrameAsync(
            viewport,
            TestContext.Current.CancellationToken);
        Assert.Empty(cleared.DisplayList.Rows[0].Commands.OfType<TerminalLineCommand>());
    }

    private static Terminal CreateTerminal(int columns, int rows) =>
        new(new TerminalOptions { Columns = columns, Rows = rows });

    private static WebLinksAddon LoadAddon(Terminal terminal)
    {
        var addon = new WebLinksAddon(static (_, _) => { });
        terminal.LoadAddon(addon);
        return addon;
    }

    private static async ValueTask<TerminalLink> GetLinkAtAsync(
        Terminal terminal,
        int zeroBasedColumn,
        int zeroBasedLine)
    {
        TerminalLink? link = await terminal.GetLinkAtAsync(
            zeroBasedColumn + 1,
            zeroBasedLine + 1,
            TestContext.Current.CancellationToken);
        Assert.NotNull(link);
        return link;
    }

    private static async ValueTask AssertLinkAtAsync(
        Terminal terminal,
        int zeroBasedColumn,
        int zeroBasedLine,
        string expected)
    {
        TerminalLink link = await GetLinkAtAsync(terminal, zeroBasedColumn, zeroBasedLine);
        Assert.Equal(expected, link.Text);
    }

    private static void AssertLink(
        TerminalLink link,
        string text,
        int startX,
        int startY,
        int endX,
        int endY)
    {
        Assert.Equal(text, link.Text);
        Assert.Equal(
            new TerminalLinkRange(
                new TerminalLinkPosition(startX, startY),
                new TerminalLinkPosition(endX, endY)),
            link.Range);
    }

    private static void AssertDefaultMatch(string hostname)
    {
        string uri = $"http://{hostname}";
        Match match = WebLinksAddon.DefaultUrlRegex.Match($"  {uri}  ");
        Assert.True(match.Success);
        Assert.Equal(uri, match.Value);
        Assert.True(WebLinkComputer.IsUrl(uri));
    }
}

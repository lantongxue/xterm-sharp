using XtermSharp.Internal;
using XtermSharp.Internal.Parser;

namespace XtermSharp.Tests.InputHandler;

public sealed class OscColorAndLinkTests
{
    [UpstreamFact("XTJS-0952", "InputHandler OSC 4: query color events")]
    public async Task Osc4_EmitsIndexedColorReportRequests()
    {
        await using var terminal = new Terminal();
        List<IReadOnlyList<TerminalColorRequest>> events = CaptureColorRequests(terminal);

        await terminal.WriteAsync("\x1b]4;0;?\x07");
        AssertRequests(events, [Report(0)]);
        events.Clear();

        await terminal.WriteAsync("\x1b]4;123;?\x07");
        AssertRequests(events, [Report(123)]);
        events.Clear();

        await terminal.WriteAsync("\x1b]4;0;?;123;?\x07");
        AssertRequests(events, [Report(0), Report(123)]);
    }

    [UpstreamFact("XTJS-0953", "InputHandler OSC 4: set color events")]
    public async Task Osc4_EmitsIndexedColorSetRequests()
    {
        await using var terminal = new Terminal();
        List<IReadOnlyList<TerminalColorRequest>> events = CaptureColorRequests(terminal);

        await terminal.WriteAsync("\x1b]4;0;rgb:01/02/03\x07");
        AssertRequests(events, [Set(0, 1, 2, 3)]);
        events.Clear();

        await terminal.WriteAsync("\x1b]4;123;#aabbcc\x07");
        AssertRequests(events, [Set(123, 170, 187, 204)]);
        events.Clear();

        await terminal.WriteAsync("\x1b]4;0;rgb:aa/bb/cc;123;#001122\x07");
        AssertRequests(events, [Set(0, 170, 187, 204), Set(123, 0, 17, 34)]);
    }

    [UpstreamFact("XTJS-0954", "InputHandler OSC 4: should ignore invalid values")]
    public async Task Osc4_IgnoresInvalidColorSpecifications()
    {
        await using var terminal = new Terminal();
        List<IReadOnlyList<TerminalColorRequest>> events = CaptureColorRequests(terminal);

        await terminal.WriteAsync("\x1b]4;0;rgb:aa/bb/cc;45;rgb:1/22/333;123;#001122\x07");

        AssertRequests(events, [Set(0, 170, 187, 204), Set(123, 0, 17, 34)]);
    }

    [UpstreamFact("XTJS-0955", "InputHandler OSC 8: hyperlink with id")]
    public async Task Osc8_RegistersAndFinishesHyperlinkWithId()
    {
        using TerminalEngine engine = CreateEngine();

        await engine.WriteAsync("\x1b]8;id=100;http://localhost:3000\x07x");
        int linkId = engine.CurrentHyperlinkId;

        Assert.NotEqual(0, linkId);
        Assert.Equal(new OscLinkData("http://localhost:3000", "100"), engine.GetLinkData(linkId));
        Assert.Equal(linkId, engine.CreateSnapshot(0, SnapshotScope.Viewport).ActiveBuffer.Lines[0].Cells[0].HyperlinkId);

        await engine.WriteAsync("\x1b]8;;\x07");
        Assert.Equal(0, engine.CurrentHyperlinkId);
    }

    [UpstreamFact("XTJS-0956", "InputHandler OSC 8: hyperlink with semi-colon")]
    public async Task Osc8_PreservesSemicolonsInsideTheUri()
    {
        using TerminalEngine engine = CreateEngine();

        await engine.WriteAsync("\x1b]8;;http://localhost:3000;abc=def\x07x");
        int linkId = engine.CurrentHyperlinkId;

        Assert.NotEqual(0, linkId);
        Assert.Equal(new OscLinkData("http://localhost:3000;abc=def"), engine.GetLinkData(linkId));
        Assert.Equal(linkId, engine.CreateSnapshot(0, SnapshotScope.Viewport).ActiveBuffer.Lines[0].Cells[0].HyperlinkId);

        await engine.WriteAsync("\x1b]8;;\x07");
        Assert.Equal(0, engine.CurrentHyperlinkId);
    }

    [UpstreamFact("XTJS-0957", "InputHandler OSC 104: restore events")]
    public async Task Osc104_EmitsIndexedColorRestoreRequests()
    {
        await using var terminal = new Terminal();
        List<IReadOnlyList<TerminalColorRequest>> events = CaptureColorRequests(terminal);

        await terminal.WriteAsync("\x1b]104;0\x07\x1b]104;43\x07");
        Assert.Equal(2, events.Count);
        Assert.Equal([Restore(0)], events[0]);
        Assert.Equal([Restore(43)], events[1]);
        events.Clear();

        await terminal.WriteAsync("\x1b]104;0;43\x07");
        AssertRequests(events, [Restore(0), Restore(43)]);
        events.Clear();

        await terminal.WriteAsync("\x1b]104\x07");
        AssertRequests(events, [Restore()]);
    }

    [UpstreamFact("XTJS-0958", "InputHandler OSC 10: FG set & query events")]
    public async Task Osc10_EmitsForegroundAndStackedSpecialColorRequests()
    {
        await using var terminal = new Terminal();
        List<IReadOnlyList<TerminalColorRequest>> events = CaptureColorRequests(terminal);
        int foreground = (int)TerminalSpecialColorIndex.Foreground;
        int background = (int)TerminalSpecialColorIndex.Background;
        int cursor = (int)TerminalSpecialColorIndex.Cursor;

        await terminal.WriteAsync("\x1b]10;?\x07");
        AssertRequests(events, [Report(foreground)]);
        events.Clear();

        await terminal.WriteAsync("\x1b]10;?;?;?;?\x07");
        AssertSeparateRequests(events, Report(foreground), Report(background), Report(cursor));
        events.Clear();

        await terminal.WriteAsync("\x1b]10;rgb:01/02/03\x07");
        AssertRequests(events, [Set(foreground, 1, 2, 3)]);
        events.Clear();

        await terminal.WriteAsync("\x1b]10;#aabbcc\x07");
        AssertRequests(events, [Set(foreground, 170, 187, 204)]);
        events.Clear();

        await terminal.WriteAsync("\x1b]10;rgb:aa/bb/cc;#001122;rgb:12/34/56\x07");
        AssertSeparateRequests(
            events,
            Set(foreground, 170, 187, 204),
            Set(background, 0, 17, 34),
            Set(cursor, 18, 52, 86));
    }

    [UpstreamFact("XTJS-0959", "InputHandler OSC 110: restore FG color")]
    public async Task Osc110_RestoresForegroundColor()
    {
        await using var terminal = new Terminal();
        List<IReadOnlyList<TerminalColorRequest>> events = CaptureColorRequests(terminal);

        await terminal.WriteAsync("\x1b]110\x07");

        AssertRequests(events, [Restore((int)TerminalSpecialColorIndex.Foreground)]);
    }

    [UpstreamFact("XTJS-0960", "InputHandler OSC 11: BG set & query events")]
    public async Task Osc11_EmitsBackgroundAndCursorColorRequests()
    {
        await using var terminal = new Terminal();
        List<IReadOnlyList<TerminalColorRequest>> events = CaptureColorRequests(terminal);
        int background = (int)TerminalSpecialColorIndex.Background;
        int cursor = (int)TerminalSpecialColorIndex.Cursor;

        await terminal.WriteAsync("\x1b]11;?\x07");
        AssertRequests(events, [Report(background)]);
        events.Clear();

        await terminal.WriteAsync("\x1b]11;?;?;?;?\x07");
        AssertSeparateRequests(events, Report(background), Report(cursor));
        events.Clear();

        await terminal.WriteAsync("\x1b]11;rgb:01/02/03\x07");
        AssertRequests(events, [Set(background, 1, 2, 3)]);
        events.Clear();

        await terminal.WriteAsync("\x1b]11;#aabbcc\x07");
        AssertRequests(events, [Set(background, 170, 187, 204)]);
        events.Clear();

        await terminal.WriteAsync("\x1b]11;#001122;rgb:12/34/56\x07");
        AssertSeparateRequests(events, Set(background, 0, 17, 34), Set(cursor, 18, 52, 86));
    }

    [UpstreamFact("XTJS-0961", "InputHandler OSC 111: restore BG color")]
    public async Task Osc111_RestoresBackgroundColor()
    {
        await using var terminal = new Terminal();
        List<IReadOnlyList<TerminalColorRequest>> events = CaptureColorRequests(terminal);

        await terminal.WriteAsync("\x1b]111\x07");

        AssertRequests(events, [Restore((int)TerminalSpecialColorIndex.Background)]);
    }

    [UpstreamFact("XTJS-0962", "InputHandler OSC 12: cursor color set & query events")]
    public async Task Osc12_EmitsOnlyCursorColorRequests()
    {
        await using var terminal = new Terminal();
        List<IReadOnlyList<TerminalColorRequest>> events = CaptureColorRequests(terminal);
        int cursor = (int)TerminalSpecialColorIndex.Cursor;

        await terminal.WriteAsync("\x1b]12;?\x07");
        AssertRequests(events, [Report(cursor)]);
        events.Clear();

        await terminal.WriteAsync("\x1b]12;?;?;?;?\x07");
        AssertRequests(events, [Report(cursor)]);
        events.Clear();

        await terminal.WriteAsync("\x1b]12;rgb:01/02/03\x07");
        AssertRequests(events, [Set(cursor, 1, 2, 3)]);
        events.Clear();

        await terminal.WriteAsync("\x1b]12;#aabbcc\x07");
        AssertRequests(events, [Set(cursor, 170, 187, 204)]);
    }

    [UpstreamFact("XTJS-0963", "InputHandler OSC 112: restore cursor color")]
    public async Task Osc112_RestoresCursorColor()
    {
        await using var terminal = new Terminal();
        List<IReadOnlyList<TerminalColorRequest>> events = CaptureColorRequests(terminal);

        await terminal.WriteAsync("\x1b]112\x07");

        AssertRequests(events, [Restore((int)TerminalSpecialColorIndex.Cursor)]);
    }

    private static List<IReadOnlyList<TerminalColorRequest>> CaptureColorRequests(Terminal terminal)
    {
        var result = new List<IReadOnlyList<TerminalColorRequest>>();
        terminal.ColorRequested += (_, args) => result.Add(args.Requests);
        return result;
    }

    private static TerminalColorRequest Report(int index) =>
        new(TerminalColorRequestType.Report, index);

    private static TerminalColorRequest Restore(int? index = null) =>
        new(TerminalColorRequestType.Restore, index);

    private static TerminalColorRequest Set(int index, byte red, byte green, byte blue) =>
        new(TerminalColorRequestType.Set, index, TerminalColor.Rgb(red, green, blue));

    private static void AssertRequests(
        IReadOnlyList<IReadOnlyList<TerminalColorRequest>> actual,
        IReadOnlyList<TerminalColorRequest> expected)
    {
        Assert.Single(actual);
        Assert.Equal(expected, actual[0]);
    }

    private static void AssertSeparateRequests(
        IReadOnlyList<IReadOnlyList<TerminalColorRequest>> actual,
        params TerminalColorRequest[] expected)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (int index = 0; index < expected.Length; index++)
        {
            Assert.Equal([expected[index]], actual[index]);
        }
    }

    private static TerminalEngine CreateEngine()
    {
        TerminalOptions options = new TerminalOptions().ValidateAndClone();
        return new TerminalEngine(
            options,
            new UnicodeRegistry(options.UnicodeVersion),
            new EscapeSequenceParser());
    }
}

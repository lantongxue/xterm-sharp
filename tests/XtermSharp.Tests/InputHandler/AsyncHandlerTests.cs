using XtermSharp.Internal;
using XtermSharp.Internal.Parser;

namespace XtermSharp.Tests.InputHandler;

public sealed class AsyncHandlerTests
{
    [UpstreamFact("XTJS-0987", "InputHandler InputHandler - async handlers async CUP with CPR check")]
    public async Task AsyncCupHandler_CompletesBeforeFollowingCursorReport()
    {
        using TerminalEngine engine = CreateEngine(out ParserRegistry parser);
        var cup = new List<int[]>();
        using IDisposable registration = parser.RegisterCsiHandler(
            new FunctionIdentifier('H'),
            async parameters =>
            {
                cup.Add(parameters.Values.ToArray());
                await Task.Yield();
                return false;
            });

        await engine.WriteAsync("aaa\x1b[3;4H\x1b[6nbbb\x1b[6;8H\x1b[6n");
        int[][] cursorReports = engine.ConsumeEvents(includeWriteParsed: false)
            .Where(value => value.Kind == EngineEventKind.Data)
            .Select(value => ParseCursorReport(value.Text ?? string.Empty))
            .ToArray();

        Assert.Equal(2, cup.Count);
        Assert.Equal(cup[0], cursorReports[0]);
        Assert.Equal(cup[1], cursorReports[1]);
    }

    [UpstreamFact("XTJS-0988", "InputHandler InputHandler - async handlers async OSC between")]
    public async Task AsyncOscHandler_BlocksSubsequentTextUntilCompletion()
    {
        using TerminalEngine engine = CreateEngine(out ParserRegistry parser);
        int calls = 0;
        using IDisposable registration = parser.RegisterOscHandler(
            1000,
            async data =>
            {
                calls++;
                await Task.Yield();
                Assert.Equal(["hello world!", ""], Lines(engine, 2));
                Assert.Equal("some data", data);
                return true;
            });

        await engine.WriteAsync("hello world!\r\n\x1b]1000;some data\x07second line");

        Assert.Equal(1, calls);
        Assert.Equal(["hello world!", "second line"], Lines(engine, 2));
    }

    [UpstreamFact("XTJS-0989", "InputHandler InputHandler - async handlers async DCS between")]
    public async Task AsyncDcsHandler_BlocksSubsequentTextAndReceivesParameters()
    {
        using TerminalEngine engine = CreateEngine(out ParserRegistry parser);
        int calls = 0;
        using IDisposable registration = parser.RegisterDcsHandler(
            new FunctionIdentifier('a'),
            async (data, parameters) =>
            {
                calls++;
                await Task.Yield();
                Assert.Equal(["hello world!", ""], Lines(engine, 2));
                Assert.Equal("some data", data);
                Assert.Equal([1, 2], parameters.Values);
                return true;
            });

        await engine.WriteAsync("hello world!\r\n\x1bP1;2asome data\x1b\\second line");

        Assert.Equal(1, calls);
        Assert.Equal(["hello world!", "second line"], Lines(engine, 2));
    }

    private static TerminalEngine CreateEngine(out ParserRegistry parser)
    {
        TerminalOptions options = new TerminalOptions { Columns = 80, Rows = 30 }.ValidateAndClone();
        var core = new EscapeSequenceParser();
        parser = new ParserRegistry(core);
        return new TerminalEngine(options, new UnicodeRegistry(options.UnicodeVersion), core);
    }

    private static string[] Lines(TerminalEngine engine, int count) =>
        engine.CreateSnapshot(0, SnapshotScope.Viewport)
            .ActiveBuffer.Lines
            .Take(count)
            .Select(line => line.TranslateToString(trimRight: true))
            .ToArray();

    private static int[] ParseCursorReport(string report)
    {
        Assert.StartsWith("\x1b[", report, StringComparison.Ordinal);
        Assert.EndsWith("R", report, StringComparison.Ordinal);
        string[] values = report[2..^1].Split(';');
        Assert.Equal(2, values.Length);
        return [int.Parse(values[0]), int.Parse(values[1])];
    }
}

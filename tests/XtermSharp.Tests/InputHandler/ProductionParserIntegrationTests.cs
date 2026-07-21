using XtermSharp.Internal;
using XtermSharp.Internal.Parser;

namespace XtermSharp.Tests.InputHandler;

public sealed class ProductionParserIntegrationTests
{
    [Fact]
    public async Task SosAndPmStrings_AreIgnoredUntilStringTerminator()
    {
        await using var terminal = NewTerminal();

        await WriteAsync(terminal,
            "A\x1bXignored\x1b\\B" +
            "\x1b^ignored\u009cC" +
            "\u0098ignored\u009cD" +
            "\u009eignored\u009cE");

        Assert.Equal("ABCDE", Line(terminal, 0));
    }

    [Fact]
    public async Task BareEscape_EndsOscAndStartsFollowingCsiSequence()
    {
        await using var terminal = NewTerminal();
        var titles = new List<string>();
        terminal.TitleChanged += (_, args) => titles.Add(args.Title);

        await WriteAsync(terminal, "\x1b]2;finished\x1b[3CX");

        Assert.Equal(["finished"], titles);
        Assert.Equal("   X", Line(terminal, 0));
    }

    [Fact]
    public async Task BareEscape_EndsOscAndStartsFollowingEscapeSequence()
    {
        await using var terminal = NewTerminal();
        var titles = new List<string>();
        terminal.TitleChanged += (_, args) => titles.Add(args.Title);

        await WriteAsync(terminal, "\x1b]2;finished\u001bDX");

        Assert.Equal(["finished"], titles);
        Assert.Equal(string.Empty, Line(terminal, 0));
        Assert.Equal("X", Line(terminal, 1));
    }

    [Fact]
    public async Task C1Osc_TakesOverAnIncompleteCsiSequence()
    {
        await using var terminal = NewTerminal();
        var titles = new List<string>();
        terminal.TitleChanged += (_, args) => titles.Add(args.Title);

        await WriteAsync(terminal, "A\x1b[999\u009d2;taken-over\u009cB");

        Assert.Equal(["taken-over"], titles);
        Assert.Equal("AB", Line(terminal, 0));
    }

    [Fact]
    public async Task C1Csi_TakesOverOscWithoutCompletingItsHandler()
    {
        await using var terminal = NewTerminal();
        var titles = new List<string>();
        terminal.TitleChanged += (_, args) => titles.Add(args.Title);

        await WriteAsync(terminal, "\x1b]2;discarded\u009b3CX");

        Assert.Empty(titles);
        Assert.Equal("   X", Line(terminal, 0));
    }

    [Fact]
    public async Task CsiIgnore_DoesNotDispatchMalformedSequence()
    {
        await using var terminal = NewTerminal();
        bool called = false;
        using IDisposable registration = terminal.Parser.RegisterCsiHandler(
            new FunctionIdentifier('m', '?'),
            _ =>
            {
                called = true;
                return ValueTask.FromResult(true);
            });

        await WriteAsync(terminal, "\x1b[?1?mX");

        Assert.False(called);
        Assert.Equal("X", Line(terminal, 0));
    }

    [Fact]
    public async Task UnknownPrivateCsi_DoesNotInvokeThePlainBuiltInHandler()
    {
        await using var terminal = NewTerminal();

        await WriteAsync(terminal, "A\x1b[?2CB");

        Assert.Equal("AB", Line(terminal, 0));
    }

    [Fact]
    public async Task UnknownEscIntermediate_DoesNotInvokeThePlainBuiltInHandler()
    {
        await using var terminal = NewTerminal();

        await WriteAsync(terminal, "A\x1b%cB");

        Assert.Equal("AB", Line(terminal, 0));
    }

    [Fact]
    public async Task OscIdentifier_AcceptsOnlyDecimalDigits()
    {
        await using var terminal = NewTerminal();
        var payloads = new List<string>();
        var maximumPayloads = new List<string>();
        using IDisposable registration = terminal.Parser.RegisterOscHandler(
            123,
            data =>
            {
                payloads.Add(data);
                return ValueTask.FromResult(true);
            });
        using IDisposable maximumRegistration = terminal.Parser.RegisterOscHandler(
            int.MaxValue,
            data =>
            {
                maximumPayloads.Add(data);
                return ValueTask.FromResult(true);
            });

        await WriteAsync(terminal,
            "\x1b]123x;alphabetic\a" +
            "\x1b]12:3;punctuation\a" +
            "\x1b]123;valid\a" +
            "\x1b]2147483647;maximum\a" +
            "\x1b]2147483648;overflow\a");

        Assert.Equal(["valid"], payloads);
        Assert.Equal(["maximum"], maximumPayloads);
    }

    [Fact]
    public async Task DeviceAttributes_RespondOnlyToZeroOrOmittedParameter()
    {
        await using var terminal = NewTerminal();
        var reports = new List<string>();
        terminal.Data += (_, args) => reports.Add(args.Data);

        await WriteAsync(terminal, "\x1b[c\x1b[0c\x1b[1c\x1b[>c\x1b[>0c\x1b[>1c");

        Assert.Equal(
            ["\x1b[?1;2c", "\x1b[?1;2c", "\x1b[>0;276;0c", "\x1b[>0;276;0c"],
            reports);
    }

    [Fact]
    public async Task OscHandlerContextSendsAnOrderedResponseInTheCurrentCommit()
    {
        await using var terminal = NewTerminal();
        var events = new List<(string Kind, long Revision, string? Data)>();
        terminal.Data += (_, args) => events.Add(("data", args.Revision, args.Data));
        terminal.WriteParsed += (_, args) => events.Add(("parsed", args.Revision, null));
        ITerminalParserContext? escapedContext = null;
        using IDisposable registration = terminal.Parser.RegisterOscHandler(
            777,
            async (data, context) =>
            {
                Assert.Equal("query", data);
                await Task.Yield();
                context.SendResponse("response");
                escapedContext = context;
                return true;
            });

        await WriteAsync(terminal, "\x1b]777;query\a");

        Assert.Equal(
            [("data", terminal.Revision, "response"), ("parsed", terminal.Revision, null)],
            events);
        Assert.NotNull(escapedContext);
        Assert.Throws<InvalidOperationException>(() => escapedContext.SendResponse("late"));
    }

    [Fact]
    public async Task CsiCaret_IsAnAliasForScrollDown()
    {
        await using var caret = NewTerminal(columns: 5, rows: 4);
        await using var standard = NewTerminal(columns: 5, rows: 4);
        const string setup = "0\r\n1\r\n2\r\n3\x1b[2;4r";

        await WriteAsync(caret, setup + "\x1b[2^");
        await WriteAsync(standard, setup + "\x1b[2T");

        Assert.Equal(Lines(standard, 4), Lines(caret, 4));
        Assert.Equal(["0", "", "", "1"], Lines(caret, 4));
    }

    [Theory]
    [InlineData('@')]
    [InlineData('G')]
    public async Task EscPercent_SelectsDefaultG0Charset(char final)
    {
        await using var terminal = NewTerminal();

        await WriteAsync(terminal, $"\x1b)0\x0Eq\x1b%{final}q");

        Assert.Equal("─q", Line(terminal, 0));
    }

    [Fact]
    public async Task DecsetTwo_ResetsAllDesignatedCharsets()
    {
        await using var terminal = NewTerminal();

        await WriteAsync(terminal,
            "\x1b(0\x1b)0\x1b*0\x1b+0" +
            "q\x0Eq\x1bnq\x1boq" +
            "\x1b[?2h" +
            "q\x0Eq\x1bnq\x1boq");

        Assert.Equal("────qqqq", Line(terminal, 0));
    }

    [Theory]
    [InlineData("\0")]
    [InlineData("\a")]
    [InlineData("\x18")]
    [InlineData("\x1a")]
    [InlineData("\x1b]777;payload\x1b\\")]
    [InlineData("\x1bPqpayload\x1b\\")]
    [InlineData("\x1b_Xpayload\x1b\\")]
    public async Task CompleteControlOrStringSequence_ClearsRepState(string intervening)
    {
        await using var terminal = NewTerminal();

        await WriteAsync(terminal, "a" + intervening + "\x1b[2b");

        Assert.Equal("a", Line(terminal, 0));
        Assert.Equal(1, terminal.Buffer.Active.CursorX);
    }

    [Fact]
    public async Task CustomCsiHandler_ClearsRepState()
    {
        await using var terminal = NewTerminal();
        using IDisposable registration = terminal.Parser.RegisterCsiHandler(
            new FunctionIdentifier('z'),
            _ => ValueTask.FromResult(true));

        await WriteAsync(terminal, "a\x1b[z\x1b[2b");

        Assert.Equal("a", Line(terminal, 0));
        Assert.Equal(1, terminal.Buffer.Active.CursorX);
    }

    [Fact]
    public async Task ASecondRep_DoesNotRepeatTheFirstRepResult()
    {
        await using var terminal = NewTerminal();

        await WriteAsync(terminal, "a\x1b[2b\x1b[2b");

        Assert.Equal("aaa", Line(terminal, 0));
        Assert.Equal(3, terminal.Buffer.Active.CursorX);
    }

    [Fact]
    public async Task SynchronousCsiHandlerException_FailsWriteWithoutFallingThroughAndParserRecovers()
    {
        await using var terminal = NewTerminal();
        bool olderHandlerCalled = false;
        var bellRevisions = new List<long>();
        var parsedRevisions = new List<long>();
        terminal.Bell += (_, args) => bellRevisions.Add(args.Revision);
        terminal.WriteParsed += (_, args) => parsedRevisions.Add(args.Revision);
        using IDisposable older = terminal.Parser.RegisterCsiHandler(
            new FunctionIdentifier('z'),
            _ =>
            {
                olderHandlerCalled = true;
                return ValueTask.FromResult(true);
            });
        IDisposable failing = terminal.Parser.RegisterCsiHandler(
            new FunctionIdentifier('z'),
            _ => throw new InvalidOperationException("synchronous handler failure"));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => WriteAsync(terminal, "A\a\x1b[z").AsTask());
        failing.Dispose();

        Assert.Equal("synchronous handler failure", exception.Message);
        Assert.False(olderHandlerCalled);
        Assert.Equal(1, terminal.Revision);
        Assert.Equal("A", Line(terminal, 0));
        Assert.Equal([1], bellRevisions);
        Assert.Empty(parsedRevisions);

        await WriteAsync(terminal, "ok");
        Assert.Equal(2, terminal.Revision);
        Assert.Equal("Aok", Line(terminal, 0));
        Assert.Equal([1], bellRevisions);
        Assert.Equal([2], parsedRevisions);
    }

    [Fact]
    public async Task AsynchronousOscHandlerException_FailsWriteWithoutInvokingBuiltInAndParserRecovers()
    {
        await using var terminal = NewTerminal();
        var titles = new List<string>();
        terminal.TitleChanged += (_, args) => titles.Add(args.Title);
        IDisposable failing = terminal.Parser.RegisterOscHandler(
            2,
            async _ =>
            {
                await Task.Yield();
                throw new InvalidOperationException("asynchronous handler failure");
            });

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => WriteAsync(terminal, "\x1b]2;must-not-apply\x1b\\").AsTask());
        failing.Dispose();

        Assert.Equal("asynchronous handler failure", exception.Message);
        Assert.Empty(titles);
        await WriteAsync(terminal, "\x1b]2;recovered\aOK");
        Assert.Equal(["recovered"], titles);
        Assert.Equal("OK", Line(terminal, 0));
    }

    [Fact]
    public void StringPayloadLimit_DefaultMatchesUpstream()
    {
        Assert.Equal(10_000_000, StringPayloadHandler.DefaultPayloadLimit);
    }

    [Fact]
    public async Task ProductionStringHandlers_RespectInjectedPayloadLimitAndRecoverAfterOverflow()
    {
        var core = new EscapeSequenceParser(payloadLimit: 4);
        TerminalOptions options = new TerminalOptions { Columns = 20, Rows = 2 }.ValidateAndClone();
        using var engine = new TerminalEngine(options, new UnicodeRegistry(options.UnicodeVersion), core);
        var parser = new ParserRegistry(core);
        var osc = new List<string>();
        var dcs = new List<string>();
        var apc = new List<string>();
        using IDisposable oscRegistration = parser.RegisterOscHandler(
            777,
            data =>
            {
                osc.Add(data);
                return ValueTask.FromResult(true);
            });
        using IDisposable dcsRegistration = parser.RegisterDcsHandler(
            new FunctionIdentifier('z'),
            (data, _) =>
            {
                dcs.Add(data);
                return ValueTask.FromResult(true);
            });
        using IDisposable apcRegistration = parser.RegisterApcHandler(
            new FunctionIdentifier('X'),
            data =>
            {
                apc.Add(data);
                return ValueTask.FromResult(true);
            });

        await engine.WriteAsync(
            "\x1b]777;abcd\a" +
            "\x1bPzabcd\x1b\\" +
            "\x1b_Xabcd\x1b\\" +
            "\x1b]2;abcd\a");

        Assert.Equal(["abcd"], osc);
        Assert.Equal(["abcd"], dcs);
        Assert.Equal(["abcd"], apc);
        Assert.Equal(
            ["abcd"],
            engine.ConsumeEvents(includeWriteParsed: false)
                .Where(value => value.Kind == EngineEventKind.TitleChanged)
                .Select(value => value.Text));

        await engine.WriteAsync(
            "\x1b]777;abcde\a" +
            "\x1bPzabcde\x1b\\" +
            "\x1b_Xabcde\x1b\\" +
            "\x1b]2;abcde\a" +
            "OK");

        Assert.Equal(["abcd"], osc);
        Assert.Equal(["abcd"], dcs);
        Assert.Equal(["abcd"], apc);
        Assert.DoesNotContain(
            engine.ConsumeEvents(includeWriteParsed: false),
            value => value.Kind == EngineEventKind.TitleChanged);
        Assert.Equal(
            "OK",
            engine.CreateSnapshot(0, SnapshotScope.Viewport)
                .ActiveBuffer.Lines[0].TranslateToString(trimRight: true));
    }

    private static Terminal NewTerminal(int columns = 20, int rows = 4) =>
        new(new TerminalOptions { Columns = columns, Rows = rows });

    private static ValueTask WriteAsync(Terminal terminal, string data) =>
        terminal.WriteAsync(data, TestContext.Current.CancellationToken);

    private static string Line(Terminal terminal, int row) =>
        terminal.Buffer.Active.GetLine(row)!.TranslateToString(trimRight: true);

    private static string[] Lines(Terminal terminal, int count) =>
        terminal.Buffer.Active.Lines.Take(count).Select(line => line.TranslateToString(trimRight: true)).ToArray();
}

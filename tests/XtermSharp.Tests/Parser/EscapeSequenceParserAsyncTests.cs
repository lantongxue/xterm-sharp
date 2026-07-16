using System.Text;
using XtermSharp.Internal;
using XtermSharp.Internal.Parser;
using static XtermSharp.Tests.Parser.EscapeParserTestSupport;

namespace XtermSharp.Tests.Parser;

public sealed class EscapeSequenceParserAsyncTests
{
    private const string Input = "\x1b[1;31mhello \x1b%Gwor\u001bEld!\x1b[0m\r\n$>\x1bP1;2axyz\x1b\\\x1b]1;foo=bar\x1b\\\x1b_Xabc\x1b\\FIN";

    public static TheoryData<string> Cases { get; } = UpstreamParserRows.ForFile(UpstreamFile, 1181, 1194);

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Matches_upstream_async_parser_cases(string upstreamId) =>
        await AssertCaseAsync(int.Parse(upstreamId.AsSpan(5), System.Globalization.CultureInfo.InvariantCulture));

    [Fact]
    public async Task PauseRecords_AreScopedToTheLatestParseOperation()
    {
        using var parser = new EscapeSequenceParser();
        parser.RegisterCsiHandler(new FunctionIdentifier('m'), async _ =>
        {
            await Task.Yield();
            return true;
        });

        for (int index = 0; index < 100; index++)
        {
            await parser.ParseAsync(CodePoints("\x1b[m"));
            Assert.Single(parser.PauseRecords);
        }

        await parser.ParseAsync(CodePoints("x"));
        Assert.Empty(parser.PauseRecords);
    }

    [Fact]
    public async Task ResetDuringAsyncStringCompletionDoesNotReenterThePausedHandler()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        foreach (EscapeHandlerKind kind in StringHandlerKinds)
        {
            using var parser = new EscapeSequenceParser();
            var printed = new List<string>();
            var lowerCalls = new List<bool>();
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int highCalls = 0;

            parser.SetPrintHandler(data => printed.Add(TextDecoder.Utf32ToString(data)));
            RegisterStringHandler(parser, kind, new DelegatingStringHandler(success =>
            {
                lowerCalls.Add(success);
                return ValueTask.FromResult(false);
            }));
            RegisterStringHandler(parser, kind, new DelegatingStringHandler(async success =>
            {
                Interlocked.Increment(ref highCalls);
                if (success)
                {
                    entered.TrySetResult(true);
                }
                return await release.Task.ConfigureAwait(false);
            }));

            ValueTask parse = parser.ParseAsync(CodePoints(StringSequence(kind) + "X"));
            await entered.Task;
            Task reset = Task.Run(parser.Reset, cancellationToken);
            bool resetCompletedPromptly;
            try
            {
                resetCompletedPromptly = await Task.WhenAny(
                    reset,
                    Task.Delay(TimeSpan.FromSeconds(1), cancellationToken)) == reset;
            }
            finally
            {
                release.TrySetResult(false);
            }

            await reset;
            await parse;
            Assert.True(resetCompletedPromptly, $"{kind} reset waited for the paused handler.");
            Assert.Equal(1, highCalls);
            Assert.Equal([false], lowerCalls);
            Assert.Equal(["X"], printed);
        }
    }

    [Fact]
    public async Task StringHandlerFailureAbortsRemainingHandlersAndRecoversParser()
    {
        foreach (EscapeHandlerKind kind in StringHandlerKinds)
        {
            using var parser = new EscapeSequenceParser();
            var lowerCalls = new List<bool>();
            int highCalls = 0;

            RegisterStringHandler(parser, kind, new DelegatingStringHandler(success =>
            {
                lowerCalls.Add(success);
                return ValueTask.FromResult(false);
            }));
            RegisterStringHandler(parser, kind, new DelegatingStringHandler(_ =>
            {
                Interlocked.Increment(ref highCalls);
                return ValueTask.FromException<bool>(new InvalidOperationException("handler failed"));
            }));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => parser.ParseAsync(CodePoints(StringSequence(kind))).AsTask());
            Assert.Equal(1, highCalls);
            Assert.Equal([false], lowerCalls);
            Assert.Equal(EscapeParserState.Ground, parser.CurrentState);

            await parser.ParseAsync(CodePoints("ok"));
            Assert.Equal(EscapeParserState.Ground, parser.CurrentState);
        }
    }

    private static async Task AssertCaseAsync(int id)
    {
        switch (id)
        {
            case 1181:
            {
                var calls = new List<string>();
                using EscapeSequenceParser parser = CreateConfiguredParser(calls, asynchronous: false);
                ValueTask parse = parser.ParseAsync(CodePoints(Input));
                Assert.True(parse.IsCompletedSuccessfully);
                parse.GetAwaiter().GetResult();
                Assert.Empty(parser.PauseRecords);
                break;
            }
            case 1182:
            {
                var calls = new List<string>();
                using EscapeSequenceParser parser = CreateConfiguredParser(calls, asynchronous: false);
                parser.ParseSynchronously(CodePoints(Input));
                Assert.Equal(ExpectedCalls, calls);
                break;
            }
            case 1183:
            {
                var calls = new List<string>();
                using EscapeSequenceParser parser = CreateConfiguredParser(calls, asynchronous: false);
                await parser.ParseAsync(CodePoints(Input));
                Assert.Equal(ExpectedCalls, calls);
                Assert.Empty(parser.PauseRecords);
                break;
            }
            case 1184:
                await AssertAsyncOperationCannotCompleteSynchronously();
                break;
            case 1185:
                await AssertConcurrentContinuationRejected();
                break;
            case 1186:
                await AssertResetDuringPauseContinuesAtNextCodePoint();
                break;
            case 1187:
            {
                var calls = new List<string>();
                using EscapeSequenceParser parser = CreateConfiguredParser(calls, asynchronous: true);
                await parser.ParseAsync(CodePoints(Input));
                Assert.Equal(ExpectedCalls, calls);
                Assert.Contains(parser.PauseRecords, record => record.Kind == ParserPauseKind.Csi);
                Assert.Contains(parser.PauseRecords, record => record.Kind == ParserPauseKind.Esc);
                Assert.Contains(parser.PauseRecords, record => record.Kind == ParserPauseKind.Dcs);
                Assert.Contains(parser.PauseRecords, record => record.Kind == ParserPauseKind.Osc);
                Assert.Contains(parser.PauseRecords, record => record.Kind == ParserPauseKind.Apc);
                break;
            }
            case 1188:
                await AssertChunkedAsyncParsing();
                break;
            case 1189:
                await AssertAsyncHandlerStack(EscapeHandlerKind.Csi);
                break;
            case 1190:
                await AssertAsyncHandlerStack(EscapeHandlerKind.Esc);
                break;
            case 1191:
                await AssertMixedHandlerStack();
                break;
            case 1192:
                await AssertAsyncHandlerStack(EscapeHandlerKind.Osc);
                break;
            case 1193:
                await AssertAsyncHandlerStack(EscapeHandlerKind.Dcs);
                break;
            case 1194:
                await AssertAsyncHandlerStack(EscapeHandlerKind.Apc);
                break;
            default:
                throw new InvalidOperationException($"Unexpected upstream test ID XTJS-{id:0000}.");
        }
    }

    private static readonly string[] ExpectedCalls =
    [
        "SGR:1,31", "PRINT:hello ", "ESC:%G", "PRINT:wor", "ESC:E", "PRINT:ld!",
        "SGR:0", "EXE:\r", "EXE:\n", "PRINT:$>", "DCS:xyz:1,2", "OSC:foo=bar",
        "APC:abc", "PRINT:FIN"
    ];

    private static EscapeSequenceParser CreateConfiguredParser(ICollection<string> calls, bool asynchronous)
    {
        var parser = new EscapeSequenceParser();
        parser.SetPrintHandler(data => calls.Add("PRINT:" + TextDecoder.Utf32ToString(data)));
        parser.SetExecuteHandler('\r', () => { calls.Add("EXE:\r"); return true; });
        parser.SetExecuteHandler('\n', () => { calls.Add("EXE:\n"); return true; });
        if (asynchronous)
        {
            parser.RegisterCsiHandler(new FunctionIdentifier('m'), async parameters =>
            {
                await Task.Yield();
                calls.Add("SGR:" + FormatParameters(parameters));
                return true;
            });
            parser.RegisterEscHandler(new FunctionIdentifier('G', null, "%"), async () =>
            {
                await Task.Yield();
                calls.Add("ESC:%G");
                return true;
            });
            parser.RegisterEscHandler(new FunctionIdentifier('E'), async () =>
            {
                await Task.Yield();
                calls.Add("ESC:E");
                return true;
            });
            parser.RegisterDcsHandler(new FunctionIdentifier('a'), async (value, parameters) =>
            {
                await Task.Yield();
                calls.Add($"DCS:{value}:{FormatParameters(parameters)}");
                return true;
            });
            parser.RegisterOscHandler(1, async value =>
            {
                await Task.Yield();
                calls.Add("OSC:" + value);
                return true;
            });
            parser.RegisterApcHandler(new FunctionIdentifier('X'), async value =>
            {
                await Task.Yield();
                calls.Add("APC:" + value);
                return true;
            });
        }
        else
        {
            parser.RegisterCsiHandler(new FunctionIdentifier('m'), parameters =>
            {
                calls.Add("SGR:" + FormatParameters(parameters));
                return true;
            });
            parser.RegisterEscHandler(new FunctionIdentifier('G', null, "%"), () => { calls.Add("ESC:%G"); return true; });
            parser.RegisterEscHandler(new FunctionIdentifier('E'), () => { calls.Add("ESC:E"); return true; });
            parser.RegisterDcsHandler(new FunctionIdentifier('a'), (value, parameters) =>
            {
                calls.Add($"DCS:{value}:{FormatParameters(parameters)}");
                return true;
            });
            parser.RegisterOscHandler(1, value => { calls.Add("OSC:" + value); return true; });
            parser.RegisterApcHandler(new FunctionIdentifier('X'), value => { calls.Add("APC:" + value); return true; });
        }
        return parser;
    }

    private static async Task AssertAsyncOperationCannotCompleteSynchronously()
    {
        using var parser = new EscapeSequenceParser();
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        parser.RegisterCsiHandler(new FunctionIdentifier('m'), async _ => await release.Task);
        ValueTask operation = parser.ParseAsync(CodePoints("\x1b[1mX"));
        Assert.False(operation.IsCompletedSuccessfully);
        Assert.True(parser.IsParsing);
        release.SetResult(true);
        await operation;
        Assert.False(parser.IsParsing);
    }

    private static async Task AssertConcurrentContinuationRejected()
    {
        using var parser = new EscapeSequenceParser();
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        parser.RegisterCsiHandler(new FunctionIdentifier('m'), async _ => await release.Task);
        ValueTask first = parser.ParseAsync(CodePoints("\x1b[1mX"));
        Assert.Throws<InvalidOperationException>(() => parser.ParseAsync(CodePoints("random")));
        Assert.Throws<InvalidOperationException>(() => parser.ParseAsync(CodePoints("more")));
        parser.Reset();
        release.SetResult(true);
        await first;
        await parser.ParseAsync(CodePoints("ok"));
        Assert.Equal(EscapeParserState.Ground, parser.CurrentState);
    }

    private static async Task AssertResetDuringPauseContinuesAtNextCodePoint()
    {
        using var parser = new EscapeSequenceParser();
        var calls = new List<string>();
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        parser.SetPrintHandler(data => calls.Add(TextDecoder.Utf32ToString(data)));
        parser.RegisterCsiHandler(new FunctionIdentifier('m'), async _ => await release.Task);
        ValueTask operation = parser.ParseAsync(CodePoints("\x1b[1mXY"));
        Assert.False(operation.IsCompletedSuccessfully);
        parser.Reset();
        release.SetResult(true);
        await operation;
        Assert.Equal(["XY"], calls);
        Assert.Equal(EscapeParserState.Ground, parser.CurrentState);
    }

    private static async Task AssertChunkedAsyncParsing()
    {
        var calls = new List<string>();
        using EscapeSequenceParser parser = CreateConfiguredParser(calls, asynchronous: true);
        foreach (Rune rune in Input.EnumerateRunes())
        {
            await parser.ParseAsync(new uint[] { (uint)rune.Value });
        }
        Assert.Contains("SGR:1,31", calls);
        Assert.Contains("SGR:0", calls);
        Assert.Contains("DCS:xyz:1,2", calls);
        Assert.Contains("OSC:foo=bar", calls);
        Assert.Contains("APC:abc", calls);
        string printed = string.Concat(calls.Where(call => call.StartsWith("PRINT:", StringComparison.Ordinal)).Select(call => call[6..]));
        Assert.Equal("hello world!$>FIN", printed);
    }

    private static async Task AssertAsyncHandlerStack(EscapeHandlerKind kind)
    {
        using var parser = new EscapeSequenceParser();
        var order = new List<string>();
        IDisposable original = RegisterAsync(kind, parser, "A", true, order);
        IDisposable fallback = RegisterAsync(kind, parser, "B", false, order);
        await parser.ParseAsync(CodePoints(Sequence(kind)));
        Assert.Equal(["B", "A"], order);

        fallback.Dispose();
        order.Clear();
        await parser.ParseAsync(CodePoints(Sequence(kind)));
        Assert.Equal(["A"], order);

        IDisposable stopping = RegisterAsync(kind, parser, "C", true, order);
        order.Clear();
        await parser.ParseAsync(CodePoints(Sequence(kind)));
        Assert.Equal(["C"], order);
        stopping.Dispose();
        original.Dispose();
    }

    private static async Task AssertMixedHandlerStack()
    {
        using var parser = new EscapeSequenceParser();
        var order = new List<string>();
        parser.RegisterCsiHandler(new FunctionIdentifier('m'), _ => { order.Add("A"); return true; });
        IDisposable sync = parser.RegisterCsiHandler(new FunctionIdentifier('m'), _ => { order.Add("B"); return false; });
        IDisposable asyncHandler = parser.RegisterCsiHandler(new FunctionIdentifier('m'), async _ =>
        {
            await Task.Yield();
            order.Add("C");
            return false;
        });
        await parser.ParseAsync(CodePoints("\x1b[1m"));
        Assert.Equal(["C", "B", "A"], order);
        sync.Dispose();
        order.Clear();
        await parser.ParseAsync(CodePoints("\x1b[1m"));
        Assert.Equal(["C", "A"], order);
        asyncHandler.Dispose();
    }

    private static IDisposable RegisterAsync(
        EscapeHandlerKind kind,
        EscapeSequenceParser parser,
        string label,
        bool result,
        ICollection<string> order) => kind switch
    {
        EscapeHandlerKind.Csi => parser.RegisterCsiHandler(new FunctionIdentifier('m'), async _ =>
        {
            await Task.Yield();
            order.Add(label);
            return result;
        }),
        EscapeHandlerKind.Esc => parser.RegisterEscHandler(new FunctionIdentifier('E'), async () =>
        {
            await Task.Yield();
            order.Add(label);
            return result;
        }),
        EscapeHandlerKind.Osc => parser.RegisterOscHandler(1, async _ =>
        {
            await Task.Yield();
            order.Add(label);
            return result;
        }),
        EscapeHandlerKind.Dcs => parser.RegisterDcsHandler(new FunctionIdentifier('p', null, "+"), async (_, _) =>
        {
            await Task.Yield();
            order.Add(label);
            return result;
        }),
        EscapeHandlerKind.Apc => parser.RegisterApcHandler(new FunctionIdentifier('p', null, "+"), async _ =>
        {
            await Task.Yield();
            order.Add(label);
            return result;
        }),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static string Sequence(EscapeHandlerKind kind) => kind switch
    {
        EscapeHandlerKind.Csi => "\x1b[1m",
        EscapeHandlerKind.Esc => "\u001bE",
        EscapeHandlerKind.Osc => "\x1b]1;foo\x1b\\",
        EscapeHandlerKind.Dcs => "\x1bP1;2+pabc\x1b\\",
        EscapeHandlerKind.Apc => "\x1b_+pabc\x1b\\",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static readonly EscapeHandlerKind[] StringHandlerKinds =
    [
        EscapeHandlerKind.Osc,
        EscapeHandlerKind.Dcs,
        EscapeHandlerKind.Apc
    ];

    private static IDisposable RegisterStringHandler(
        EscapeSequenceParser parser,
        EscapeHandlerKind kind,
        DelegatingStringHandler handler) => kind switch
    {
        EscapeHandlerKind.Osc => parser.RegisterOscHandler(1, (IOscParserHandler)handler),
        EscapeHandlerKind.Dcs => parser.RegisterDcsHandler(new FunctionIdentifier('p', null, "+"), handler),
        EscapeHandlerKind.Apc => parser.RegisterApcHandler(new FunctionIdentifier('p', null, "+"), handler),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static string StringSequence(EscapeHandlerKind kind) => kind switch
    {
        EscapeHandlerKind.Osc => "\x1b]1;payload\u009c",
        EscapeHandlerKind.Dcs => "\x1bP+pPayload\u009c",
        EscapeHandlerKind.Apc => "\x1b_+pPayload\u009c",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private sealed class DelegatingStringHandler(Func<bool, ValueTask<bool>> complete) :
        IOscParserHandler,
        IDcsParserHandler,
        IApcParserHandler
    {
        public void Start()
        {
        }

        public void Hook(CsiParameters parameters)
        {
        }

        public void Put(ReadOnlySpan<uint> data)
        {
        }

        public ValueTask<bool> EndAsync(bool success) => complete(success);

        public ValueTask<bool> UnhookAsync(bool success) => complete(success);
    }
}

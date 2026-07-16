using XtermSharp.Internal;
using XtermSharp.Internal.Parser;
using static XtermSharp.Tests.Parser.EscapeParserTestSupport;

namespace XtermSharp.Tests.Parser;

public sealed class EscapeSequenceParserHandlerTests
{
    private const string Input = "\x1b[1;31mhello \x1b%Gwor\u001bEld!\x1b[0m\r\n$>\x1b]1;foo=bar\x1b\\";

    public static TheoryData<string> Cases { get; } = UpstreamParserRows.ForFile(UpstreamFile, 1129, 1180);

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Matches_upstream_handler_and_identifier_cases(string upstreamId) =>
        await AssertCaseAsync(int.Parse(upstreamId.AsSpan(5), System.Globalization.CultureInfo.InvariantCulture));

    private static async Task AssertCaseAsync(int id)
    {
        switch (id)
        {
            case 1129:
                await AssertPrintHandlerAsync();
                return;
            case 1130:
                await AssertEscHandlerAsync();
                return;
            case 1131:
                await AssertCsiHandlerAsync();
                return;
            case 1132:
                await AssertExecuteHandlerAsync();
                return;
            case 1133:
                await AssertOscHandlerAsync();
                return;
            case 1134:
                await AssertDcsHandlerAsync();
                return;
            case 1135:
                await AssertApcHandlerAsync();
                return;
            case 1136:
                await AssertErrorHandlerAsync();
                return;
        }

        if (id is >= 1137 and <= 1143)
        {
            await AssertHandlerStackAsync(EscapeHandlerKind.Esc, id - 1137);
            return;
        }
        if (id is >= 1144 and <= 1150)
        {
            await AssertHandlerStackAsync(EscapeHandlerKind.Csi, id - 1144);
            return;
        }
        if (id is >= 1151 and <= 1157)
        {
            await AssertHandlerStackAsync(EscapeHandlerKind.Osc, id - 1151);
            return;
        }
        if (id is >= 1158 and <= 1164)
        {
            await AssertHandlerStackAsync(EscapeHandlerKind.Dcs, id - 1158);
            return;
        }
        if (id is >= 1165 and <= 1171)
        {
            await AssertHandlerStackAsync(EscapeHandlerKind.Apc, id - 1165);
            return;
        }

        switch (id)
        {
            case 1172:
                AssertPrefixLimits();
                break;
            case 1173:
                AssertIntermediateLimits();
                break;
            case 1174:
                AssertFinalLimits();
                break;
            case 1175:
                AssertEscAndApcFinalLimits();
                break;
            case 1176:
                AssertIdentifierStacking();
                break;
            case 1177:
                await AssertIdentifierInvocationAsync(EscapeHandlerKind.Esc);
                break;
            case 1178:
                await AssertIdentifierInvocationAsync(EscapeHandlerKind.Csi);
                break;
            case 1179:
                await AssertIdentifierInvocationAsync(EscapeHandlerKind.Dcs);
                break;
            case 1180:
                await AssertIdentifierInvocationAsync(EscapeHandlerKind.Apc);
                break;
            default:
                throw new InvalidOperationException($"Unexpected upstream test ID XTJS-{id:0000}.");
        }
    }

    private static async Task AssertPrintHandlerAsync()
    {
        using var parser = new EscapeSequenceParser();
        var printed = new System.Text.StringBuilder();
        parser.SetPrintHandler(data => printed.Append(TextDecoder.Utf32ToString(data)));
        await ParseAsync(parser, Input);
        Assert.Equal("hello world!$>", printed.ToString());
        parser.ClearPrintHandler();
        parser.ClearPrintHandler();
        printed.Clear();
        await ParseAsync(parser, Input);
        Assert.Empty(printed.ToString());
    }

    private static async Task AssertEscHandlerAsync()
    {
        using var parser = new EscapeSequenceParser();
        var calls = new List<string>();
        parser.RegisterEscHandler(new FunctionIdentifier('G', null, "%"), () => { calls.Add("%G"); return true; });
        parser.RegisterEscHandler(new FunctionIdentifier('E'), () => { calls.Add("E"); return true; });
        await ParseAsync(parser, Input);
        Assert.Equal(["%G", "E"], calls);
        parser.ClearEscHandler(new FunctionIdentifier('G', null, "%"));
        parser.ClearEscHandler(new FunctionIdentifier('G', null, "%"));
        calls.Clear();
        await ParseAsync(parser, Input);
        Assert.Equal(["E"], calls);
        parser.ClearEscHandler(new FunctionIdentifier('E'));
        calls.Clear();
        await ParseAsync(parser, Input);
        Assert.Empty(calls);
    }

    private static async Task AssertCsiHandlerAsync()
    {
        using var parser = new EscapeSequenceParser();
        var calls = new List<string>();
        parser.RegisterCsiHandler(new FunctionIdentifier('m'), parameters =>
        {
            calls.Add(FormatParameters(parameters));
            return true;
        });
        await ParseAsync(parser, Input);
        Assert.Equal(["1,31", "0"], calls);
        parser.ClearCsiHandler(new FunctionIdentifier('m'));
        parser.ClearCsiHandler(new FunctionIdentifier('m'));
        calls.Clear();
        await ParseAsync(parser, Input);
        Assert.Empty(calls);
    }

    private static async Task AssertExecuteHandlerAsync()
    {
        using var parser = new EscapeSequenceParser();
        var calls = new List<char>();
        parser.SetExecuteHandler('\n', () => { calls.Add('\n'); return true; });
        parser.SetExecuteHandler('\r', () => { calls.Add('\r'); return true; });
        await ParseAsync(parser, Input);
        Assert.Equal(['\r', '\n'], calls);
        parser.ClearExecuteHandler('\r');
        parser.ClearExecuteHandler('\r');
        calls.Clear();
        await ParseAsync(parser, Input);
        Assert.Equal(['\n'], calls);
    }

    private static async Task AssertOscHandlerAsync()
    {
        using var parser = new EscapeSequenceParser();
        var calls = new List<string>();
        parser.RegisterOscHandler(1, value => { calls.Add(value); return true; });
        await ParseAsync(parser, Input);
        Assert.Equal(["foo=bar"], calls);
        parser.ClearOscHandler(1);
        parser.ClearOscHandler(1);
        calls.Clear();
        await ParseAsync(parser, Input);
        Assert.Empty(calls);
    }

    private static async Task AssertDcsHandlerAsync()
    {
        using var parser = new EscapeSequenceParser();
        var calls = new List<string>();
        parser.RegisterDcsHandler(new FunctionIdentifier('p', null, "+"), (value, parameters) =>
        {
            calls.Add($"{FormatParameters(parameters)}:{value}");
            return true;
        });
        await ParseAsync(parser, "\x1bP1;2;3+pabc");
        await ParseAsync(parser, ";de\u009c");
        Assert.Equal(["1,2,3:abc;de"], calls);
        parser.ClearDcsHandler(new FunctionIdentifier('p', null, "+"));
        parser.ClearDcsHandler(new FunctionIdentifier('p', null, "+"));
        calls.Clear();
        await ParseAsync(parser, "\x1bP1;2;3+pabc;de\u009c");
        Assert.Empty(calls);
    }

    private static async Task AssertApcHandlerAsync()
    {
        using var parser = new EscapeSequenceParser();
        var calls = new List<string>();
        parser.RegisterApcHandler(new FunctionIdentifier('p', null, "+"), value => { calls.Add(value); return true; });
        await ParseAsync(parser, "\x1b_+pabc");
        await ParseAsync(parser, ";de\u009c");
        Assert.Equal(["abc;de"], calls);
        parser.ClearApcHandler(new FunctionIdentifier('p', null, "+"));
        parser.ClearApcHandler(new FunctionIdentifier('p', null, "+"));
        calls.Clear();
        await ParseAsync(parser, "\x1b_+pabc;de\u009c");
        Assert.Empty(calls);
    }

    private static async Task AssertErrorHandlerAsync()
    {
        using var parser = new EscapeSequenceParser();
        EscapeParserErrorState? captured = null;
        parser.SetErrorHandler(state =>
        {
            captured = state;
            return state;
        });
        await ParseAsync(parser, "\x1b[1;2;€;3m");
        Assert.NotNull(captured);
        Assert.Equal(6, captured.Position);
        Assert.Equal((uint)'€', captured.Code);
        Assert.Equal(EscapeParserState.CsiParameter, captured.CurrentState);
        Assert.Equal("1,2,0", FormatParameters(captured.Parameters));
        parser.ClearErrorHandler();
        parser.ClearErrorHandler();
        captured = null;
        await ParseAsync(parser, "\x1b[1;2;a;3m");
        Assert.Null(captured);
    }

    private static async Task AssertHandlerStackAsync(EscapeHandlerKind kind, int scenario)
    {
        using var parser = new EscapeSequenceParser();
        var order = new List<string>();
        IDisposable first = Register(kind, parser, "A", true, order);
        IDisposable? second = null;
        IDisposable? third = null;

        switch (scenario)
        {
            case 0:
                second = Register(kind, parser, "B", true, order);
                break;
            case 1:
                second = Register(kind, parser, "B", false, order);
                break;
            case 2:
                second = Register(kind, parser, "B", true, order);
                third = Register(kind, parser, "C", false, order);
                break;
            case 3:
                second = Register(kind, parser, "B", true, order);
                third = Register(kind, parser, "C", true, order);
                break;
            case 4:
                first.Dispose();
                first = Register(kind, parser, "A", true, order);
                second = Register(kind, parser, "B", false, order);
                third = Register(kind, parser, "C", false, order);
                break;
            case 5:
                second = Register(kind, parser, "B", true, order);
                second.Dispose();
                break;
            case 6:
                second = Register(kind, parser, "B", true, order);
                second.Dispose();
                second.Dispose();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario));
        }

        await ParseAsync(parser, Sequence(kind));
        string[] expected = scenario switch
        {
            0 => ["B"],
            1 => ["B", "A"],
            2 => ["C", "B"],
            3 => ["C"],
            4 => ["C", "B", "A"],
            5 or 6 => ["A"],
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        Assert.Equal(expected, order);

        first.Dispose();
        second?.Dispose();
        third?.Dispose();
    }

    private static IDisposable Register(
        EscapeHandlerKind kind,
        EscapeSequenceParser parser,
        string label,
        bool result,
        ICollection<string> order) => kind switch
    {
        EscapeHandlerKind.Esc => parser.RegisterEscHandler(new FunctionIdentifier('G', null, "%"), () =>
        {
            order.Add(label);
            return result;
        }),
        EscapeHandlerKind.Csi => parser.RegisterCsiHandler(new FunctionIdentifier('m'), _ =>
        {
            order.Add(label);
            return result;
        }),
        EscapeHandlerKind.Osc => parser.RegisterOscHandler(1, _ =>
        {
            order.Add(label);
            return result;
        }),
        EscapeHandlerKind.Dcs => parser.RegisterDcsHandler(new FunctionIdentifier('p', null, "+"), (_, _) =>
        {
            order.Add(label);
            return result;
        }),
        EscapeHandlerKind.Apc => parser.RegisterApcHandler(new FunctionIdentifier('p', null, "+"), _ =>
        {
            order.Add(label);
            return result;
        }),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static string Sequence(EscapeHandlerKind kind) => kind switch
    {
        EscapeHandlerKind.Esc => "\x1b%G",
        EscapeHandlerKind.Csi => "\x1b[1;31m",
        EscapeHandlerKind.Osc => "\x1b]1;foo\x1b\\",
        EscapeHandlerKind.Dcs => "\x1bP1;2+pabc\x1b\\",
        EscapeHandlerKind.Apc => "\x1b_+pabc\x1b\\",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static void AssertPrefixLimits()
    {
        for (int value = 0x3C; value <= 0x3F; value++)
        {
            char prefix = (char)value;
            Assert.Equal($"{prefix}z", EscapeSequenceParser.IdentifierToString(
                EscapeSequenceParser.Identifier(new FunctionIdentifier('z', prefix))));
        }
        Assert.Throws<ArgumentException>(() => EscapeSequenceParser.Identifier(new FunctionIdentifier('z', '\x3B')));
        Assert.Throws<ArgumentException>(() => EscapeSequenceParser.Identifier(new FunctionIdentifier('z', '\x40')));
    }

    private static void AssertIntermediateLimits()
    {
        for (int value = 0x20; value <= 0x2F; value++)
        {
            string intermediates = new((char)value, 2);
            Assert.Equal(intermediates + "z", EscapeSequenceParser.IdentifierToString(
                EscapeSequenceParser.Identifier(new FunctionIdentifier('z', null, intermediates))));
        }
        Assert.Throws<ArgumentException>(() => EscapeSequenceParser.Identifier(new FunctionIdentifier('z', null, "\x1f")));
        Assert.Throws<ArgumentException>(() => EscapeSequenceParser.Identifier(new FunctionIdentifier('z', null, "0")));
        Assert.Throws<ArgumentException>(() => EscapeSequenceParser.Identifier(new FunctionIdentifier('z', null, "!!!")));
    }

    private static void AssertFinalLimits()
    {
        for (int value = 0x40; value <= 0x7E; value++)
        {
            char final = (char)value;
            Assert.Equal(final.ToString(), EscapeSequenceParser.IdentifierToString(
                EscapeSequenceParser.Identifier(new FunctionIdentifier(final))));
        }
        Assert.Throws<ArgumentException>(() => EscapeSequenceParser.Identifier(new FunctionIdentifier('\x3F')));
        Assert.Throws<ArgumentException>(() => EscapeSequenceParser.Identifier(new FunctionIdentifier('\x7F')));
    }

    private static void AssertEscAndApcFinalLimits()
    {
        using var parser = new EscapeSequenceParser();
        for (int value = 0x30; value <= 0x7E; value++)
        {
            char final = (char)value;
            parser.RegisterEscHandler(new FunctionIdentifier(final), () => true).Dispose();
            parser.RegisterApcHandler(new FunctionIdentifier(final), _ => true).Dispose();
        }
        Assert.Throws<ArgumentException>(() => parser.RegisterEscHandler(new FunctionIdentifier('\x2F'), () => true));
        Assert.Throws<ArgumentException>(() => parser.RegisterEscHandler(new FunctionIdentifier('\x7F'), () => true));
        Assert.Throws<ArgumentException>(() => parser.RegisterApcHandler(new FunctionIdentifier('\x2F'), _ => true));
        Assert.Throws<ArgumentException>(() => parser.RegisterApcHandler(new FunctionIdentifier('\x7F'), _ => true));
    }

    private static void AssertIdentifierStacking()
    {
        Assert.Equal("z", IdString(new FunctionIdentifier('z')));
        Assert.Equal("?z", IdString(new FunctionIdentifier('z', '?')));
        Assert.Equal("!z", IdString(new FunctionIdentifier('z', null, "!")));
        Assert.Equal("?!z", IdString(new FunctionIdentifier('z', '?', "!")));
        Assert.Equal("?!!z", IdString(new FunctionIdentifier('z', '?', "!!")));
    }

    private static async Task AssertIdentifierInvocationAsync(EscapeHandlerKind kind)
    {
        using var parser = new EscapeSequenceParser();
        var calls = new List<string>();
        var disposables = new List<IDisposable>();
        FunctionIdentifier[] identifiers = kind is EscapeHandlerKind.Esc or EscapeHandlerKind.Apc
            ? [new('z'), new('z', null, "!"), new('z', null, "!!")]
            : [new('z'), new('z', null, "!"), new('z', null, "!!"), new('z', '?'), new('z', '?', "!"), new('z', '?', "!!")];

        foreach (FunctionIdentifier identifier in identifiers)
        {
            string label = IdString(identifier);
            disposables.Add(kind switch
            {
                EscapeHandlerKind.Esc => parser.RegisterEscHandler(identifier, () => { calls.Add(label); return true; }),
                EscapeHandlerKind.Csi => parser.RegisterCsiHandler(identifier, _ => { calls.Add(label); return true; }),
                EscapeHandlerKind.Dcs => parser.RegisterDcsHandler(identifier, (_, _) => { calls.Add(label); return true; }),
                EscapeHandlerKind.Apc => parser.RegisterApcHandler(identifier, _ => { calls.Add(label); return true; }),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            });
        }

        string sequence = kind switch
        {
            EscapeHandlerKind.Esc => "\x1bz\x1b!z\x1b!!z",
            EscapeHandlerKind.Csi => "\x1b[1;z\x1b[1;!z\x1b[1;!!z\x1b[?1;z\x1b[?1;!z\x1b[?1;!!z",
            EscapeHandlerKind.Dcs => "\x1bP1;zAB\x1b\\\x1bP1;!zAB\x1b\\\x1bP1;!!zAB\x1b\\\x1bP?1;zAB\x1b\\\x1bP?1;!zAB\x1b\\\x1bP?1;!!zAB\x1b\\",
            EscapeHandlerKind.Apc => "\x1b_zAB\x1b\\\x1b_!zAB\x1b\\\x1b_!!zAB\x1b\\",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        await ParseAsync(parser, sequence);
        Assert.Equal(identifiers.Select(IdString), calls);
        foreach (IDisposable disposable in disposables)
        {
            disposable.Dispose();
        }
        await ParseAsync(parser, sequence);
        Assert.Equal(identifiers.Select(IdString), calls);
    }

    private static string IdString(FunctionIdentifier identifier) =>
        EscapeSequenceParser.IdentifierToString(EscapeSequenceParser.Identifier(
            identifier,
            identifier.Final < '@' ? 0x30 : 0x40));
}

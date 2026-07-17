using XtermSharp.Internal.Parser;
using static XtermSharp.Tests.Parser.Support.EscapeParserTestSupport;

namespace XtermSharp.Tests.Parser;

public sealed class EscapeSequenceParserBehaviorTests
{
    public static TheoryData<string> Cases { get; } = UpstreamParserRows.ForFile(UpstreamFile, 1104, 1128);

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Matches_upstream_escape_sequence_examples(string upstreamId) =>
        await AssertCaseAsync(int.Parse(upstreamId.AsSpan(5), System.Globalization.CultureInfo.InvariantCulture));

    private static async Task AssertCaseAsync(int id)
    {
        var recorder = new EscapeParserRecorder();
        using EscapeSequenceParser parser = recorder.CreateParser();
        switch (id)
        {
            case 1104:
                await ParseAsync(parser, "\x1b[<31;5mHello World! öäü€\nabc");
                Assert.Equal(
                    ["csi:<m:31,5", "print:Hello World! öäü€", "execute:10", "print:abc"],
                    recorder.Calls);
                break;
            case 1105:
                await ParseAsync(parser, "\x1b]0;abc123€öäü\a");
                Assert.Equal(
                    ["osc:0:Start:", "osc:0:Put:abc123€öäü", "osc:0:End:true"],
                    recorder.Calls);
                break;
            case 1106:
                await ParseAsync(parser, "\x1bP1;2;3+$aäbc;däe\u009c");
                Assert.Equal(
                    ["dcs:+$a:Hook:1,2,3", "dcs:+$a:Put:äbc;däe", "dcs:+$a:Unhook:true"],
                    recorder.Calls);
                break;
            case 1107:
                await ParseAsync(parser, "\x1bP1;2;3+$abc;de");
                await ParseAsync(parser, "abc\u009c");
                Assert.Equal(
                    ["dcs:+$a:Hook:1,2,3", "dcs:+$a:Put:bc;de", "dcs:+$a:Put:abc", "dcs:+$a:Unhook:true"],
                    recorder.Calls);
                break;
            case 1108:
                await ParseAsync(parser, "abc\u00901;2;3+$abc;de\u009c");
                Assert.Equal("print:abc", recorder.Calls[0]);
                Assert.Contains("dcs:+$a:Hook:1,2,3", recorder.Calls);
                Assert.Contains("dcs:+$a:Put:bc;de", recorder.Calls);
                Assert.Contains("dcs:+$a:Unhook:true", recorder.Calls);
                break;
            case 1109:
                await ParseAsync(parser, "abc\u0098123tzf\u009cdefg");
                Assert.Equal(["print:abc", "print:defg"], recorder.Calls);
                break;
            case 1110:
                await ParseAsync(parser, "abc\u009d123;tzf\u009cdefg");
                Assert.Equal("print:abc", recorder.Calls[0]);
                Assert.Contains("osc:123:Put:tzf", recorder.Calls);
                Assert.Contains("osc:123:End:true", recorder.Calls);
                Assert.Equal("print:defg", recorder.Calls[^1]);
                break;
            case 1111:
                await ParseAsync(parser, "\x1b_X3+$aäbc;däe\u009c");
                Assert.Equal(
                    ["apc:X:Start:", "apc:X:Put:3+$aäbc;däe", "apc:X:End:true"],
                    recorder.Calls);
                break;
            case 1112:
                await ParseAsync(parser, "\x1b_Xabc;de");
                await ParseAsync(parser, "abc\u009c");
                Assert.Equal(
                    ["apc:X:Start:", "apc:X:Put:abc;de", "apc:X:Put:abc", "apc:X:End:true"],
                    recorder.Calls);
                break;
            case 1113:
                await ParseAsync(parser, "abc\u009fAbc;de\u009cxyz");
                Assert.Equal(
                    ["print:abc", "apc:A:Start:", "apc:A:Put:bc;de", "apc:A:End:true", "print:xyz"],
                    recorder.Calls);
                break;
            case 1114:
                await ParseAsync(parser, "abc\x1b_Abc;de\x1b\\xyz");
                Assert.Equal(
                    ["print:abc", "apc:A:Start:", "apc:A:Put:bc;de", "apc:A:End:true", "print:xyz"],
                    recorder.Calls);
                break;
            case 1115:
                await ParseAsync(parser, "\x1b[1€abcdefg\u009b<;c");
                Assert.Contains("print:abcdefg", recorder.Calls);
                Assert.Contains("csi:<c:0,0", recorder.Calls);
                Assert.Equal(EscapeParserState.Ground, parser.CurrentState);
                break;
            case 1116:
                await ParseAsync(parser, "abc\u009d123;tzf\x1b\\defg");
                Assert.DoesNotContain(recorder.Calls, call => call.StartsWith("esc:", StringComparison.Ordinal));
                Assert.Equal("print:abc", recorder.Calls[0]);
                Assert.Contains("osc:123:End:true", recorder.Calls);
                Assert.Equal("print:defg", recorder.Calls[^1]);
                break;
            case 1117:
                await ParseAsync(parser, "\x1b[<31;5::123:;8m");
                Assert.Contains("csi:<m:31,5:[-1,123,-1],8", recorder.Calls);
                break;
            case 1118:
                await ParseAsync(parser, "abc\u00901;2::55;3+$abc;de\u009c");
                Assert.Contains("dcs:+$a:Hook:1,2:[-1,55],3", recorder.Calls);
                Assert.Contains("dcs:+$a:Unhook:true", recorder.Calls);
                break;
            case 1119:
                await AssertDcsAbortAsync(parser, recorder, '\x18');
                break;
            case 1120:
                await AssertDcsAbortAsync(parser, recorder, '\x1A');
                break;
            case 1121:
                await AssertApcAbortAsync(parser, recorder, '\x18');
                break;
            case 1122:
                await AssertApcAbortAsync(parser, recorder, '\x1A');
                break;
            case 1123:
                await AssertOscAbortAsync(parser, recorder, '\x18');
                break;
            case 1124:
                await AssertOscAbortAsync(parser, recorder, '\x1A');
                break;
            case 1125:
                parser.CurrentState = EscapeParserState.CsiIgnore;
                await ParseAsync(parser, "€öäü");
                Assert.Equal(EscapeParserState.CsiIgnore, parser.CurrentState);
                Assert.Empty(recorder.Calls);
                break;
            case 1126:
                parser.CurrentState = EscapeParserState.DcsIgnore;
                await ParseAsync(parser, "€öäü");
                Assert.Equal(EscapeParserState.DcsIgnore, parser.CurrentState);
                Assert.Empty(recorder.Calls);
                break;
            case 1127:
                parser.CurrentState = EscapeParserState.DcsPassthrough;
                await ParseAsync(parser, "\u00901;2;3+$a€öäü");
                Assert.Equal(EscapeParserState.DcsPassthrough, parser.CurrentState);
                Assert.Contains("dcs:+$a:Hook:1,2,3", recorder.Calls);
                Assert.Contains("dcs:+$a:Put:€öäü", recorder.Calls);
                break;
            case 1128:
                await ParseAsync(parser, "\u009c");
                Assert.Equal(EscapeParserState.Ground, parser.CurrentState);
                Assert.Empty(recorder.Calls);
                break;
            default:
                throw new InvalidOperationException($"Unexpected upstream test ID XTJS-{id:0000}.");
        }
    }

    private static async Task AssertDcsAbortAsync(EscapeSequenceParser parser, EscapeParserRecorder recorder, char terminator)
    {
        await ParseAsync(parser, "abc\u00901;2::55;3+$abc;de" + terminator);
        Assert.Contains("dcs:+$a:Hook:1,2:[-1,55],3", recorder.Calls);
        Assert.Contains("dcs:+$a:Unhook:false", recorder.Calls);
    }

    private static async Task AssertApcAbortAsync(EscapeSequenceParser parser, EscapeParserRecorder recorder, char terminator)
    {
        await ParseAsync(parser, "abc\u009fXbc;de" + terminator);
        Assert.Contains("apc:X:End:false", recorder.Calls);
    }

    private static async Task AssertOscAbortAsync(EscapeSequenceParser parser, EscapeParserRecorder recorder, char terminator)
    {
        await ParseAsync(parser, "\x1b]0;abc123€öäü" + terminator);
        Assert.Contains("osc:0:End:false", recorder.Calls);
    }
}

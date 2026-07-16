using XtermSharp.Internal.Parser;
using static XtermSharp.Tests.Parser.EscapeParserTestSupport;

namespace XtermSharp.Tests.Parser;

public sealed class EscapeSequenceParserStateTests
{
    private static readonly EscapeParserState[] States = Enum.GetValues<EscapeParserState>()
        .Where(state => state != EscapeParserState.StateLength)
        .ToArray();

    public static TheoryData<string> Cases { get; } = UpstreamParserRows.ForFile(UpstreamFile, 1034, 1103);

    [Theory]
    [MemberData(nameof(Cases))]
    public void Matches_upstream_state_machine_cases(string upstreamId) =>
        AssertCase(int.Parse(upstreamId.AsSpan(5), System.Globalization.CultureInfo.InvariantCulture));

    private static void AssertCase(int id)
    {
        EscapeTransitionTable table = EscapeSequenceParser.Vt500TransitionTable;
        switch (id)
        {
            case 1034:
            {
                var custom = new EscapeTransitionTable(4257);
                using var defaultParser = new EscapeSequenceParser();
                using var explicitParser = new EscapeSequenceParser(table);
                using var customParser = new EscapeSequenceParser(custom);
                Assert.Same(table, defaultParser.Transitions);
                Assert.Same(table, explicitParser.Transitions);
                Assert.Same(custom, customParser.Transitions);
                break;
            }
            case 1035:
            {
                using var parser = new EscapeSequenceParser();
                Assert.Equal(EscapeParserState.Ground, parser.InitialState);
                Assert.Equal(EscapeParserState.Ground, parser.CurrentState);
                AssertParameters(parser.Parameters, P(0));
                Assert.Equal(string.Empty, parser.Collected);
                break;
            }
            case 1036:
            {
                using var parser = new EscapeSequenceParser();
                parser.CurrentState = EscapeParserState.CsiParameter;
                parser.Parameters.ResetZeroDefault();
                parser.Parameters.AddDigit(3);
                parser.SetCollected("#");
                parser.PrecedingJoinState = 9;
                parser.Reset();
                Assert.Equal(EscapeParserState.Ground, parser.CurrentState);
                AssertParameters(parser.Parameters, P(0));
                Assert.Equal(string.Empty, parser.Collected);
                Assert.Equal(0, parser.PrecedingJoinState);
                break;
            }
            case 1037:
                AssertTransitions(table, EscapeParserState.Ground, Executables, EscapeParserAction.Execute, EscapeParserState.Ground);
                break;
            case 1038:
                AssertTransitions(table, EscapeParserState.Ground, Printables, EscapeParserAction.Print, EscapeParserState.Ground);
                break;
            case 1039:
                AssertAnywhereGround(table);
                break;
            case 1040:
                AssertAnywhereEscape();
                break;
            case 1041:
                AssertTransitions(table, EscapeParserState.Escape, Executables, EscapeParserAction.Execute, EscapeParserState.Escape);
                break;
            case 1042:
                AssertTransitions(table, EscapeParserState.Escape, [0x7F], EscapeParserAction.Ignore, EscapeParserState.Escape);
                break;
            case 1043:
                AssertEscapeDispatch(table);
                break;
            case 1044:
                AssertTransitions(table, EscapeParserState.Escape, Range(0x20, 0x30), EscapeParserAction.Collect, EscapeParserState.EscapeIntermediate);
                break;
            case 1045:
                AssertTransitions(table, EscapeParserState.EscapeIntermediate, Executables, EscapeParserAction.Execute, EscapeParserState.EscapeIntermediate);
                break;
            case 1046:
                AssertTransitions(table, EscapeParserState.EscapeIntermediate, [0x7F], EscapeParserAction.Ignore, EscapeParserState.EscapeIntermediate);
                break;
            case 1047:
                AssertTransitions(table, EscapeParserState.EscapeIntermediate, Range(0x20, 0x30), EscapeParserAction.Collect, EscapeParserState.EscapeIntermediate);
                break;
            case 1048:
                AssertTransitions(table, EscapeParserState.EscapeIntermediate, Range(0x30, 0x7F), EscapeParserAction.EscDispatch, EscapeParserState.Ground);
                break;
            case 1049:
                AssertCsiEntryFromAnywhere();
                break;
            case 1050:
                AssertTransitions(table, EscapeParserState.CsiEntry, Executables, EscapeParserAction.Execute, EscapeParserState.CsiEntry);
                break;
            case 1051:
                AssertTransitions(table, EscapeParserState.CsiEntry, [0x7F], EscapeParserAction.Ignore, EscapeParserState.CsiEntry);
                break;
            case 1052:
                AssertTransitions(table, EscapeParserState.CsiEntry, Range(0x40, 0x7F), EscapeParserAction.CsiDispatch, EscapeParserState.Ground);
                break;
            case 1053:
                AssertEntryParameters(EscapeParserState.CsiEntry);
                break;
            case 1054:
                AssertTransitions(table, EscapeParserState.CsiParameter, Executables, EscapeParserAction.Execute, EscapeParserState.CsiParameter);
                break;
            case 1055:
                AssertParameterActions(EscapeParserState.CsiParameter);
                break;
            case 1056:
                AssertTransitions(table, EscapeParserState.CsiParameter, [0x7F], EscapeParserAction.Ignore, EscapeParserState.CsiParameter);
                break;
            case 1057:
                AssertTransitions(table, EscapeParserState.CsiParameter, Range(0x40, 0x7F), EscapeParserAction.CsiDispatch, EscapeParserState.Ground);
                break;
            case 1058:
                AssertTransitions(table, EscapeParserState.CsiEntry, Range(0x20, 0x30), EscapeParserAction.Collect, EscapeParserState.CsiIntermediate);
                break;
            case 1059:
                AssertTransitions(table, EscapeParserState.CsiParameter, Range(0x20, 0x30), EscapeParserAction.Collect, EscapeParserState.CsiIntermediate);
                break;
            case 1060:
                AssertTransitions(table, EscapeParserState.CsiIntermediate, Executables, EscapeParserAction.Execute, EscapeParserState.CsiIntermediate);
                break;
            case 1061:
                AssertTransitions(table, EscapeParserState.CsiIntermediate, Range(0x20, 0x30), EscapeParserAction.Collect, EscapeParserState.CsiIntermediate);
                break;
            case 1062:
                AssertTransitions(table, EscapeParserState.CsiIntermediate, [0x7F], EscapeParserAction.Ignore, EscapeParserState.CsiIntermediate);
                break;
            case 1063:
                AssertTransitions(table, EscapeParserState.CsiIntermediate, Range(0x40, 0x7F), EscapeParserAction.CsiDispatch, EscapeParserState.Ground);
                break;
            case 1064:
                AssertColonAction(EscapeParserState.CsiEntry, EscapeParserState.CsiParameter);
                break;
            case 1065:
            case 1066:
                AssertTransitions(table, EscapeParserState.CsiParameter, [0x3C, 0x3D, 0x3E, 0x3F], EscapeParserAction.Ignore, EscapeParserState.CsiIgnore);
                break;
            case 1067:
                AssertTransitions(table, EscapeParserState.CsiIntermediate, Range(0x30, 0x40), EscapeParserAction.Ignore, EscapeParserState.CsiIgnore);
                break;
            case 1068:
                AssertTransitions(table, EscapeParserState.CsiIgnore, Executables, EscapeParserAction.Execute, EscapeParserState.CsiIgnore);
                break;
            case 1069:
                AssertTransitions(table, EscapeParserState.CsiIgnore, [.. Range(0x20, 0x40), 0x7F], EscapeParserAction.Ignore, EscapeParserState.CsiIgnore);
                break;
            case 1070:
                AssertTransitions(table, EscapeParserState.CsiIgnore, Range(0x40, 0x7F), EscapeParserAction.Ignore, EscapeParserState.Ground);
                break;
            case 1071:
                AssertSosPmEntry(table);
                break;
            case 1072:
                AssertTransitions(table, EscapeParserState.SosPmString, [.. Executables, .. Printables, 0x7F], EscapeParserAction.Ignore, EscapeParserState.SosPmString);
                break;
            case 1073:
                AssertOscEntry(table);
                break;
            case 1074:
                AssertTransitions(table, EscapeParserState.OscString, [.. Range(0x00, 0x07), .. Range(0x08, 0x18), 0x19, .. Range(0x1C, 0x20)], EscapeParserAction.Ignore, EscapeParserState.OscString);
                break;
            case 1075:
                AssertTransitions(table, EscapeParserState.OscString, Range(0x20, 0x80), EscapeParserAction.OscPut, EscapeParserState.OscString);
                break;
            case 1076:
                AssertStringEntry(table, 0x50, 0x90, EscapeParserState.DcsEntry);
                break;
            case 1077:
                AssertTransitions(table, EscapeParserState.DcsEntry, [.. Executables, 0x7F], EscapeParserAction.Ignore, EscapeParserState.DcsEntry);
                break;
            case 1078:
                AssertEntryParameters(EscapeParserState.DcsEntry);
                break;
            case 1079:
                AssertTransitions(table, EscapeParserState.DcsParameter, [.. Executables, 0x7F], EscapeParserAction.Ignore, EscapeParserState.DcsParameter);
                break;
            case 1080:
                AssertParameterActions(EscapeParserState.DcsParameter);
                break;
            case 1081:
                AssertColonAction(EscapeParserState.DcsEntry, EscapeParserState.DcsParameter);
                break;
            case 1082:
                AssertTransitions(table, EscapeParserState.DcsParameter, [0x3C, 0x3D, 0x3E, 0x3F], EscapeParserAction.Ignore, EscapeParserState.DcsIgnore);
                break;
            case 1083:
                AssertTransitions(table, EscapeParserState.DcsIntermediate, Range(0x30, 0x40), EscapeParserAction.Ignore, EscapeParserState.DcsIgnore);
                break;
            case 1084:
                AssertTransitions(table, EscapeParserState.DcsIgnore, [.. Executables, .. Range(0x20, 0x80), 0x7F], EscapeParserAction.Ignore, EscapeParserState.DcsIgnore);
                break;
            case 1085:
                AssertTransitions(table, EscapeParserState.DcsEntry, Range(0x20, 0x30), EscapeParserAction.Collect, EscapeParserState.DcsIntermediate);
                break;
            case 1086:
                AssertTransitions(table, EscapeParserState.DcsParameter, Range(0x20, 0x30), EscapeParserAction.Collect, EscapeParserState.DcsIntermediate);
                break;
            case 1087:
                AssertTransitions(table, EscapeParserState.DcsIntermediate, [.. Executables, 0x7F], EscapeParserAction.Ignore, EscapeParserState.DcsIntermediate);
                break;
            case 1088:
                AssertTransitions(table, EscapeParserState.DcsIntermediate, Range(0x20, 0x30), EscapeParserAction.Collect, EscapeParserState.DcsIntermediate);
                break;
            case 1089:
                AssertTransitions(table, EscapeParserState.DcsIntermediate, Range(0x30, 0x40), EscapeParserAction.Ignore, EscapeParserState.DcsIgnore);
                break;
            case 1090:
                AssertTransitions(table, EscapeParserState.DcsEntry, Range(0x40, 0x7F), EscapeParserAction.DcsHook, EscapeParserState.DcsPassthrough);
                break;
            case 1091:
                AssertTransitions(table, EscapeParserState.DcsParameter, Range(0x40, 0x7F), EscapeParserAction.DcsHook, EscapeParserState.DcsPassthrough);
                break;
            case 1092:
                AssertTransitions(table, EscapeParserState.DcsIntermediate, Range(0x40, 0x7F), EscapeParserAction.DcsHook, EscapeParserState.DcsPassthrough);
                break;
            case 1093:
                AssertTransitions(table, EscapeParserState.DcsPassthrough, [.. Executables, .. Printables], EscapeParserAction.DcsPut, EscapeParserState.DcsPassthrough);
                break;
            case 1094:
                AssertTransitions(table, EscapeParserState.DcsPassthrough, [0x7F], EscapeParserAction.Ignore, EscapeParserState.DcsPassthrough);
                break;
            case 1095:
                AssertStringEntry(table, 0x5F, 0x9F, EscapeParserState.ApcEntry);
                break;
            case 1096:
                AssertTransitions(table, EscapeParserState.ApcEntry, [.. Executables, 0x7F], EscapeParserAction.Ignore, EscapeParserState.ApcEntry);
                break;
            case 1097:
                AssertTransitions(table, EscapeParserState.ApcEntry, Range(0x20, 0x30), EscapeParserAction.Collect, EscapeParserState.ApcIntermediate);
                break;
            case 1098:
                AssertTransitions(table, EscapeParserState.ApcEntry, Range(0x30, 0x7F), EscapeParserAction.ApcStart, EscapeParserState.ApcPassthrough);
                break;
            case 1099:
                AssertTransitions(table, EscapeParserState.ApcIntermediate, Range(0x30, 0x7F), EscapeParserAction.ApcStart, EscapeParserState.ApcPassthrough);
                break;
            case 1100:
                AssertTransitions(table, EscapeParserState.ApcIntermediate, [.. Executables, 0x7F], EscapeParserAction.Ignore, EscapeParserState.ApcIntermediate);
                break;
            case 1101:
                AssertTransitions(table, EscapeParserState.ApcPassthrough, [.. Range(0x08, 0x0E), .. Printables], EscapeParserAction.ApcPut, EscapeParserState.ApcPassthrough);
                break;
            case 1102:
                AssertTransitions(table, EscapeParserState.ApcPassthrough, [.. Range(0x00, 0x08), .. Range(0x0E, 0x18), 0x19, .. Range(0x1C, 0x20), 0x7F], EscapeParserAction.Ignore, EscapeParserState.ApcPassthrough);
                break;
            case 1103:
                AssertApcEnd(table);
                break;
            default:
                throw new InvalidOperationException($"Unexpected upstream test ID XTJS-{id:0000}.");
        }
    }

    private static void AssertAnywhereGround(EscapeTransitionTable table)
    {
        int[] codes = [0x18, 0x1A, .. Range(0x80, 0x90), .. Range(0x91, 0x98), 0x99, 0x9A];
        foreach (EscapeParserState state in States)
        {
            foreach (int code in codes)
            {
                EscapeParserAction action = state switch
                {
                    EscapeParserState.OscString when code is 0x18 or 0x1A => EscapeParserAction.OscEnd,
                    EscapeParserState.DcsPassthrough when code is 0x18 or 0x1A => EscapeParserAction.DcsUnhook,
                    EscapeParserState.ApcPassthrough when code is 0x18 or 0x1A => EscapeParserAction.ApcEnd,
                    _ => EscapeParserAction.Execute
                };
                AssertTransitions(table, state, [code], action, EscapeParserState.Ground);
            }
        }
    }

    private static void AssertAnywhereEscape()
    {
        foreach (EscapeParserState state in States)
        {
            using var parser = new EscapeSequenceParser();
            parser.CurrentState = state;
            parser.Parameters.ResetZeroDefault();
            parser.Parameters.AddDigit(3);
            parser.SetCollected("#");
            Parse(parser, "\x1b");
            Assert.Equal(EscapeParserState.Escape, parser.CurrentState);
            AssertParameters(parser.Parameters, P(0));
            Assert.Equal(string.Empty, parser.Collected);
        }
    }

    private static void AssertEscapeDispatch(EscapeTransitionTable table)
    {
        int[] codes = [.. Range(0x30, 0x50), .. Range(0x51, 0x58), 0x59, 0x5A, 0x5C, .. Range(0x60, 0x7F)];
        AssertTransitions(table, EscapeParserState.Escape, codes, EscapeParserAction.EscDispatch, EscapeParserState.Ground);
    }

    private static void AssertCsiEntryFromAnywhere()
    {
        using (var parser = new EscapeSequenceParser())
        {
            Parse(parser, "\x1b[");
            Assert.Equal(EscapeParserState.CsiEntry, parser.CurrentState);
        }
        foreach (EscapeParserState state in States)
        {
            using var parser = new EscapeSequenceParser();
            parser.CurrentState = state;
            Parse(parser, "\u009b");
            Assert.Equal(EscapeParserState.CsiEntry, parser.CurrentState);
            AssertParameters(parser.Parameters, P(0));
            Assert.Equal(string.Empty, parser.Collected);
        }
    }

    private static void AssertEntryParameters(EscapeParserState state)
    {
        for (int digit = 0; digit <= 9; digit++)
        {
            using var parser = new EscapeSequenceParser();
            parser.CurrentState = state;
            Parse(parser, digit.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(state == EscapeParserState.CsiEntry ? EscapeParserState.CsiParameter : EscapeParserState.DcsParameter, parser.CurrentState);
            AssertParameters(parser.Parameters, P(digit));
        }
        using (var parser = new EscapeSequenceParser())
        {
            parser.CurrentState = state;
            Parse(parser, ";");
            AssertParameters(parser.Parameters, P(0), P(0));
        }
        EscapeTransitionTable table = EscapeSequenceParser.Vt500TransitionTable;
        AssertTransitions(table, state, [0x3C, 0x3D, 0x3E, 0x3F], EscapeParserAction.Collect,
            state == EscapeParserState.CsiEntry ? EscapeParserState.CsiParameter : EscapeParserState.DcsParameter);
    }

    private static void AssertParameterActions(EscapeParserState state)
    {
        for (int digit = 0; digit <= 9; digit++)
        {
            using var parser = new EscapeSequenceParser();
            parser.CurrentState = state;
            Parse(parser, digit.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(state, parser.CurrentState);
            AssertParameters(parser.Parameters, P(digit));
        }
        using var semicolonParser = new EscapeSequenceParser();
        semicolonParser.CurrentState = state;
        Parse(semicolonParser, ";");
        AssertParameters(semicolonParser.Parameters, P(0), P(0));
    }

    private static void AssertColonAction(EscapeParserState state, EscapeParserState expectedState)
    {
        using var parser = new EscapeSequenceParser();
        parser.CurrentState = state;
        Parse(parser, ":");
        Assert.Equal(expectedState, parser.CurrentState);
        AssertParameters(parser.Parameters, P(0, -1));
    }

    private static void AssertSosPmEntry(EscapeTransitionTable table)
    {
        AssertTransitions(table, EscapeParserState.Escape, [0x58, 0x5E], EscapeParserAction.Ignore, EscapeParserState.SosPmString);
        foreach (EscapeParserState state in States)
        {
            AssertTransitions(table, state, [0x98, 0x9E], EscapeParserAction.Ignore, EscapeParserState.SosPmString);
        }
    }

    private static void AssertOscEntry(EscapeTransitionTable table)
    {
        AssertTransitions(table, EscapeParserState.Escape, [0x5D], EscapeParserAction.OscStart, EscapeParserState.OscString);
        foreach (EscapeParserState state in States)
        {
            AssertTransitions(table, state, [0x9D], EscapeParserAction.OscStart, EscapeParserState.OscString);
        }
    }

    private static void AssertStringEntry(EscapeTransitionTable table, int sevenBitFinal, int c1, EscapeParserState target)
    {
        AssertTransitions(table, EscapeParserState.Escape, [sevenBitFinal], EscapeParserAction.Clear, target);
        foreach (EscapeParserState state in States)
        {
            AssertTransitions(table, state, [c1], EscapeParserAction.Clear, target);
        }
    }

    private static void AssertApcEnd(EscapeTransitionTable table)
    {
        AssertTransitions(table, EscapeParserState.ApcPassthrough, [0x9C, 0x18, 0x1A], EscapeParserAction.ApcEnd, EscapeParserState.Ground);
        AssertTransitions(table, EscapeParserState.ApcPassthrough, [0x1B], EscapeParserAction.ApcEnd, EscapeParserState.Ground);
        using var parser = new EscapeSequenceParser();
        parser.CurrentState = EscapeParserState.ApcPassthrough;
        Parse(parser, "\x1b");
        Assert.Equal(EscapeParserState.Escape, parser.CurrentState);
    }
}

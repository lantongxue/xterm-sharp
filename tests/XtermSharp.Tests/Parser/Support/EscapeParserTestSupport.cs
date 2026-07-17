using System.Text;
using XtermSharp.Internal;
using XtermSharp.Internal.Parser;

namespace XtermSharp.Tests.Parser;

internal static class EscapeParserTestSupport
{
    internal const string UpstreamFile = "src/common/parser/EscapeSequenceParser.test.ts";

    internal static int[] Executables { get; } = [.. Range(0x00, 0x18), 0x19, .. Range(0x1C, 0x20)];
    internal static int[] Printables { get; } = Range(0x20, 0x7F);

    internal static int[] Range(int start, int end) => Enumerable.Range(start, end - start).ToArray();

    internal static uint[] CodePoints(string value) =>
        value.EnumerateRunes().Select(static rune => (uint)rune.Value).ToArray();

    internal static void Parse(EscapeSequenceParser parser, string value) =>
        parser.ParseAsync(CodePoints(value)).AsTask().GetAwaiter().GetResult();

    internal static Task ParseAsync(EscapeSequenceParser parser, string value) =>
        parser.ParseAsync(CodePoints(value)).AsTask();

    internal static void AssertTransitions(
        EscapeTransitionTable table,
        EscapeParserState state,
        IEnumerable<int> codes,
        EscapeParserAction action,
        EscapeParserState next)
    {
        ushort expected = EscapeTransitionTable.Encode(action, next);
        foreach (int code in codes)
        {
            Assert.Equal(expected, table.Table[((int)state << 8) | code]);
        }
    }

    internal static string FormatParameters(CsiParameters parameters)
    {
        var values = new List<string>(parameters.Values.Length);
        for (int index = 0; index < parameters.Values.Length; index++)
        {
            string value = parameters.Values[index].ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (parameters.HasSubParameters(index))
            {
                value += ":[" + string.Join(',', parameters.GetSubParameters(index)) + "]";
            }
            values.Add(value);
        }
        return string.Join(',', values);
    }

    internal static void AssertParameters(ParserParameters parameters, params ParserParameter[] expected) =>
        Assert.Equal(expected, parameters.ToImmutableArray());

    internal static ParserParameter P(int value, params int[] subParameters) =>
        new(value, System.Collections.Immutable.ImmutableArray.Create(subParameters));
}

using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using XtermSharp.Internal.Utilities;

namespace XtermSharp.Internal.Parser;

internal sealed class EscapeTransitionTable
{
    private const int ActionShift = 8;
    private const int StateShift = 8;

    internal EscapeTransitionTable(int length)
    {
        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }
        Table = new ushort[length];
    }

    internal ushort[] Table { get; }

    internal void SetDefault(EscapeParserAction action, EscapeParserState next) =>
        Array.Fill(Table, Encode(action, next));

    internal void Add(int code, EscapeParserState state, EscapeParserAction action, EscapeParserState next) =>
        Table[((int)state << StateShift) | code] = Encode(action, next);

    internal void AddMany(IEnumerable<int> codes, EscapeParserState state, EscapeParserAction action, EscapeParserState next)
    {
        foreach (int code in codes)
        {
            Add(code, state, action, next);
        }
    }

    internal static ushort Encode(EscapeParserAction action, EscapeParserState next) =>
        (ushort)(((int)action << ActionShift) | (int)next);
}

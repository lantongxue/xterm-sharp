using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
namespace XtermSharp.Internal.Parser.State;

internal sealed record EscapeParserErrorState(
    int Position,
    uint Code,
    EscapeParserState CurrentState,
    int Collect,
    CsiParameters Parameters,
    bool Abort = false);

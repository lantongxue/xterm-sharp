using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using XtermSharp.Internal.Utilities;

namespace XtermSharp.Internal.Parser;

internal sealed record EscapeParserErrorState(
    int Position,
    uint Code,
    EscapeParserState CurrentState,
    int Collect,
    CsiParameters Parameters,
    bool Abort = false);

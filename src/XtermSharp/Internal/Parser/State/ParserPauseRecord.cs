using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using XtermSharp.Internal.Utilities;

namespace XtermSharp.Internal.Parser;

internal readonly record struct ParserPauseRecord(int Position, ParserPauseKind Kind, int HandlerIndex);

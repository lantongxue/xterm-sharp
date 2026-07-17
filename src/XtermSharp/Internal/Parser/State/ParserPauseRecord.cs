using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
namespace XtermSharp.Internal.Parser.State;

internal readonly record struct ParserPauseRecord(int Position, ParserPauseKind Kind, int HandlerIndex);

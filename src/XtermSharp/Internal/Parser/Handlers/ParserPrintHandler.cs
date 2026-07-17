using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
namespace XtermSharp.Internal.Parser.Handlers;

internal delegate void ParserPrintHandler(ReadOnlySpan<uint> data);

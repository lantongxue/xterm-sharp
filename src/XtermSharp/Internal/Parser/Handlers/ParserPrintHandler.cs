using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using XtermSharp.Internal.Utilities;

namespace XtermSharp.Internal.Parser;

internal delegate void ParserPrintHandler(ReadOnlySpan<uint> data);

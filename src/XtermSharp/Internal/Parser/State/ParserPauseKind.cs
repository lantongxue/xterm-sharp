using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using XtermSharp.Internal.Utilities;

namespace XtermSharp.Internal.Parser;

internal enum ParserPauseKind : byte
{
    Csi,
    Esc,
    Osc,
    Dcs,
    Apc
}

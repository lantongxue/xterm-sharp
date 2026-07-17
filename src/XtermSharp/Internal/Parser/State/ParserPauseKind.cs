using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
namespace XtermSharp.Internal.Parser.State;

internal enum ParserPauseKind : byte
{
    Csi,
    Esc,
    Osc,
    Dcs,
    Apc
}

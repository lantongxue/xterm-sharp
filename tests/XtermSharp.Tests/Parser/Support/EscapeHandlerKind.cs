using System.Text;
using XtermSharp.Internal;
using XtermSharp.Internal.Parser;

namespace XtermSharp.Tests.Parser;

internal enum EscapeHandlerKind
{
    Esc,
    Csi,
    Osc,
    Dcs,
    Apc
}

using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using XtermSharp.Internal.Utilities;

namespace XtermSharp.Internal.Parser;

internal enum EscapeParserAction : byte
{
    Ignore,
    Error,
    Print,
    Execute,
    OscStart,
    OscPut,
    OscEnd,
    CsiDispatch,
    Parameter,
    Collect,
    EscDispatch,
    Clear,
    DcsHook,
    DcsPut,
    DcsUnhook,
    ApcStart,
    ApcPut,
    ApcEnd
}

using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
namespace XtermSharp.Internal.Parser.State;

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

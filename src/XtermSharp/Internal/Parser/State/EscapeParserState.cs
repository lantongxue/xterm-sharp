using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using XtermSharp.Internal.Utilities;

namespace XtermSharp.Internal.Parser;

internal enum EscapeParserState : byte
{
    Ground,
    Escape,
    EscapeIntermediate,
    CsiEntry,
    CsiParameter,
    CsiIntermediate,
    CsiIgnore,
    SosPmString,
    OscString,
    DcsEntry,
    DcsParameter,
    DcsIgnore,
    DcsIntermediate,
    DcsPassthrough,
    ApcEntry,
    ApcIntermediate,
    ApcPassthrough,
    StateLength
}

using System.Collections.Immutable;
using System.Text;
using XtermSharp.Internal.Input;
using XtermSharp.Internal.Parser;

namespace XtermSharp.Internal;

internal enum EngineEventKind
{
    Bell,
    Data,
    Binary,
    CursorMoved,
    LineFeed,
    Render,
    Resize,
    Scroll,
    TitleChanged,
    ColorRequest,
    OptionsChanged,
    WriteParsed
}

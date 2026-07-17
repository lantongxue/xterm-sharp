namespace XtermSharp.Internal.Parser;

internal enum OscParserState
{
    Start,
    Identifier,
    Payload,
    Abort
}

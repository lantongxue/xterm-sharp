namespace XtermSharp.Internal.Parser.State;

internal enum OscParserState
{
    Start,
    Identifier,
    Payload,
    Abort
}

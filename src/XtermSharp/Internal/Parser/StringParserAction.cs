namespace XtermSharp.Internal.Parser;

internal enum StringParserAction
{
    Start,
    Put,
    End,
    Hook,
    Unhook
}

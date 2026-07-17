namespace XtermSharp.Internal.Input.Encoding;

[Flags]
internal enum KittyKeyboardFlags : byte
{
    None = 0,
    DisambiguateEscapeCodes = 1 << 0,
    ReportEventTypes = 1 << 1,
    ReportAlternateKeys = 1 << 2,
    ReportAllKeysAsEscapeCodes = 1 << 3,
    ReportAssociatedText = 1 << 4
}

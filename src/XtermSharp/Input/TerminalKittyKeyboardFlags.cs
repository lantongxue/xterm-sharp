namespace XtermSharp.Input;

[Flags]
public enum TerminalKittyKeyboardFlags : byte
{
    None = 0,
    DisambiguateEscapeCodes = 1 << 0,
    ReportEventTypes = 1 << 1,
    ReportAlternateKeys = 1 << 2,
    ReportAllKeysAsEscapeCodes = 1 << 3,
    ReportAssociatedText = 1 << 4
}

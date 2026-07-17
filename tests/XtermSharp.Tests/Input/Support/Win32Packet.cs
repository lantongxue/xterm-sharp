namespace XtermSharp.Tests.Input.Support;

internal readonly record struct Win32Packet(
    int VirtualKey,
    int ScanCode,
    int UnicodeCharacter,
    int KeyDown,
    int ControlState,
    int RepeatCount);

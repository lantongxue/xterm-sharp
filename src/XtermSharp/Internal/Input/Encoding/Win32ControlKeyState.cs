namespace XtermSharp.Internal.Input;

[Flags]
internal enum Win32ControlKeyState : ushort
{
    None = 0,
    RightAltPressed = 1 << 0,
    LeftAltPressed = 1 << 1,
    RightControlPressed = 1 << 2,
    LeftControlPressed = 1 << 3,
    ShiftPressed = 1 << 4,
    NumLockOn = 1 << 5,
    ScrollLockOn = 1 << 6,
    CapsLockOn = 1 << 7,
    EnhancedKey = 1 << 8
}

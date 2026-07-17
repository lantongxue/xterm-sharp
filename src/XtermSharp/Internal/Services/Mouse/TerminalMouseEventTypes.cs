namespace XtermSharp.Internal;

[Flags]
internal enum TerminalMouseEventTypes
{
    None = 0,
    Down = 1,
    Up = 2,
    Drag = 4,
    Move = 8,
    Wheel = 16
}

namespace XtermSharp.Internal;

internal readonly record struct BufferResizeOptions(
    bool IsWindowsPty = false,
    int WindowsBuildNumber = 0,
    bool ReflowCursorLine = false);

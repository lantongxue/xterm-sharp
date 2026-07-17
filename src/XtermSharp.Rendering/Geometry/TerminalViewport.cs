namespace XtermSharp.Rendering;

public readonly record struct TerminalViewport(
    double Width,
    double Height,
    double RenderScale = 1,
    TerminalThickness Padding = default);

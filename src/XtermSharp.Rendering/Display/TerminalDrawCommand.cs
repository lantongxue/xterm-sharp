namespace XtermSharp.Rendering.Display;

public abstract record TerminalDrawCommand(TerminalDrawCommandKind Kind, TerminalRect Bounds);

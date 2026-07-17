namespace XtermSharp.Rendering;

public abstract record TerminalDrawCommand(TerminalDrawCommandKind Kind, TerminalRect Bounds);

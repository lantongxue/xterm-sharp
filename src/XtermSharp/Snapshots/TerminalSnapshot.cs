namespace XtermSharp;

public sealed record TerminalSnapshot(
    long Revision,
    int Columns,
    int Rows,
    TerminalBufferKind ActiveBufferKind,
    TerminalModes Modes,
    TerminalBufferSnapshot ActiveBuffer,
    TerminalBufferSnapshot? NormalBuffer,
    TerminalBufferSnapshot? AlternateBuffer);

namespace XtermSharp;

public sealed record TerminalBufferCollection(
    TerminalBufferSnapshot Active,
    TerminalBufferSnapshot Normal,
    TerminalBufferSnapshot Alternate);

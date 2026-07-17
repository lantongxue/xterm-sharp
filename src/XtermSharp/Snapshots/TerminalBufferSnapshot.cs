using System.Collections.Immutable;

namespace XtermSharp.Snapshots;

public sealed record TerminalBufferSnapshot(
    TerminalBufferKind Kind,
    int CursorX,
    int CursorY,
    int ViewportY,
    int BaseY,
    ImmutableArray<TerminalLineSnapshot> Lines)
{
    public int Length => Lines.Length;

    public TerminalLineSnapshot? GetLine(int line) =>
        (uint)line < (uint)Lines.Length ? Lines[line] : null;
}

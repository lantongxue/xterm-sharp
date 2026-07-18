using System.Collections.Immutable;

namespace XtermSharp.Snapshots;

public sealed record TerminalSnapshot(
    long Revision,
    int Columns,
    int Rows,
    TerminalBufferKind ActiveBufferKind,
    TerminalModes Modes,
    TerminalBufferSnapshot ActiveBuffer,
    TerminalBufferSnapshot? NormalBuffer,
    TerminalBufferSnapshot? AlternateBuffer,
    ImmutableDictionary<int, TerminalHyperlinkMetadata> Hyperlinks)
{
    public TerminalHyperlinkMetadata? GetHyperlink(int linkId) =>
        Hyperlinks.TryGetValue(linkId, out TerminalHyperlinkMetadata? hyperlink) ? hyperlink : null;
}

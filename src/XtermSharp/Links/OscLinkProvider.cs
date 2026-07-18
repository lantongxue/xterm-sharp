namespace XtermSharp.Links;

internal sealed class OscLinkProvider(Terminal terminal) : ITerminalLinkProvider
{
    public async ValueTask<IReadOnlyList<TerminalLink>?> ProvideLinksAsync(
        int bufferLineNumber,
        CancellationToken cancellationToken = default)
    {
        TerminalSnapshot snapshot = await terminal.GetSnapshotAsync(
            SnapshotScope.ActiveBuffer,
            cancellationToken).ConfigureAwait(false);
        return ComputeLinks(snapshot, bufferLineNumber);
    }

    private IReadOnlyList<TerminalLink> ComputeLinks(TerminalSnapshot snapshot, int requestedLine)
    {
        if (requestedLine <= 0 || requestedLine > snapshot.ActiveBuffer.Lines.Length ||
            snapshot.Hyperlinks.Count == 0)
        {
            return Array.Empty<TerminalLink>();
        }

        var links = new List<TerminalLink>();
        int runId = 0;
        int startX = 0;
        int startY = 0;
        int lastX = -1;
        int lastY = -1;

        for (int y = 0; y < snapshot.ActiveBuffer.Lines.Length; y++)
        {
            TerminalLineSnapshot line = snapshot.ActiveBuffer.Lines[y];
            for (int x = 0; x < line.Cells.Length; x++)
            {
                int linkId = line.Cells[x].HyperlinkId;
                if (linkId != 0 && !snapshot.Hyperlinks.ContainsKey(linkId))
                {
                    linkId = 0;
                }
                bool continues = runId != 0 && linkId == runId &&
                    (y == lastY && x == lastX + 1 ||
                     y == lastY + 1 && x == 0 && line.IsWrapped && lastX == snapshot.Columns - 1);
                if (!continues)
                {
                    AddRun(links, snapshot, requestedLine, runId, startX, startY, lastX, lastY);
                    runId = 0;
                }
                if (linkId == 0)
                {
                    continue;
                }
                if (runId == 0)
                {
                    runId = linkId;
                    startX = x;
                    startY = y;
                }
                lastX = x;
                lastY = y;
            }
        }
        AddRun(links, snapshot, requestedLine, runId, startX, startY, lastX, lastY);
        return links;
    }

    private void AddRun(
        List<TerminalLink> links,
        TerminalSnapshot snapshot,
        int requestedLine,
        int linkId,
        int startX,
        int startY,
        int endX,
        int endY)
    {
        if (linkId == 0 || requestedLine < startY + 1 || requestedLine > endY + 1 ||
            !snapshot.Hyperlinks.TryGetValue(linkId, out TerminalHyperlinkMetadata? metadata))
        {
            return;
        }

        var link = new TerminalLink(
            new TerminalLinkRange(
                new TerminalLinkPosition(startX + 1, startY + 1),
                new TerminalLinkPosition(endX + 1, endY + 1)),
            metadata.Uri,
            (terminalEvent, _) => terminal.NotifyHyperlinkActivated(terminalEvent, metadata))
        {
            Hyperlink = metadata,
            Hover = (terminalEvent, _) => terminal.NotifyHyperlinkHovered(terminalEvent, metadata),
            Leave = (terminalEvent, _) => terminal.NotifyHyperlinkLeft(terminalEvent, metadata)
        };
        links.Add(link);
    }
}

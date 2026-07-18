using System.Collections.Immutable;

namespace XtermSharp.Internal.Services.Links;

/// <summary>Tracks OSC 8 hyperlink metadata for every buffer line on which it appears.</summary>
internal sealed class OscLinkService
{
    private readonly Func<TerminalBuffer> _getBuffer;
    private readonly Dictionary<string, OscLinkEntry> _entriesWithId = new(StringComparer.Ordinal);
    private readonly Dictionary<int, OscLinkEntry> _entriesByLinkId = [];
    private readonly Dictionary<TerminalMarker, TerminalBuffer> _markerBuffers = [];
    private int _nextId = 1;
    private bool _deferEmptyEntryRemoval;

    public OscLinkService(BufferService bufferService)
        : this(() => bufferService.Buffer)
    {
        ArgumentNullException.ThrowIfNull(bufferService);
    }

    public OscLinkService(Func<TerminalBuffer> getBuffer)
    {
        _getBuffer = getBuffer ?? throw new ArgumentNullException(nameof(getBuffer));
    }

    public int RegisterLink(OscLinkData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        TerminalBuffer buffer = _getBuffer();
        int line = buffer.YBase + buffer.CursorY;

        if (data.Id is null)
        {
            OscLinkEntry anonymous = CreateEntry(data, null, line);
            return anonymous.LinkId;
        }

        string key = CreateKey(data.Id, data.Uri);
        if (_entriesWithId.TryGetValue(key, out OscLinkEntry? existing))
        {
            AddLineToLink(existing.LinkId, line);
            return existing.LinkId;
        }

        OscLinkEntry entry = CreateEntry(data, key, line);
        _entriesWithId.Add(key, entry);
        return entry.LinkId;
    }

    public void AddLineToLink(int linkId, int line)
    {
        TerminalBuffer buffer = _getBuffer();
        if (!_entriesByLinkId.TryGetValue(linkId, out OscLinkEntry? entry) ||
            entry.Lines.Any(marker =>
                _markerBuffers.TryGetValue(marker, out TerminalBuffer? markerBuffer) &&
                ReferenceEquals(markerBuffer, buffer) &&
                marker.Line == line))
        {
            return;
        }
        AddMarker(entry, buffer, line);
    }

    public OscLinkData? GetLinkData(int linkId) =>
        _entriesByLinkId.TryGetValue(linkId, out OscLinkEntry? entry) ? entry.Data : null;

    public ImmutableDictionary<int, TerminalHyperlinkMetadata> CreateSnapshotMetadata(
        params TerminalBufferSnapshot?[] buffers)
    {
        var linkIds = new HashSet<int>();
        foreach (TerminalBufferSnapshot? buffer in buffers)
        {
            if (buffer is null)
            {
                continue;
            }
            foreach (TerminalLineSnapshot line in buffer.Lines)
            {
                foreach (TerminalCellSnapshot cell in line.Cells)
                {
                    if (cell.HyperlinkId != 0)
                    {
                        linkIds.Add(cell.HyperlinkId);
                    }
                }
            }
        }

        var metadata = ImmutableDictionary.CreateBuilder<int, TerminalHyperlinkMetadata>();
        foreach (int linkId in linkIds)
        {
            if (_entriesByLinkId.TryGetValue(linkId, out OscLinkEntry? entry))
            {
                metadata.Add(linkId, entry.Data.ToMetadata(linkId));
            }
        }
        return metadata.ToImmutable();
    }

    public void BeginBufferReflow() => _deferEmptyEntryRemoval = true;

    public void CompleteBufferReflow(TerminalBuffer normal, TerminalBuffer alternate, int activeLinkId)
    {
        try
        {
            RestoreMarkersForReferencedCells(normal);
            RestoreMarkersForReferencedCells(alternate);
            if (activeLinkId != 0 &&
                _entriesByLinkId.TryGetValue(activeLinkId, out OscLinkEntry? activeEntry) &&
                activeEntry.Lines.Count == 0)
            {
                TerminalBuffer activeBuffer = _getBuffer();
                AddMarker(activeEntry, activeBuffer, activeBuffer.YBase + activeBuffer.CursorY);
            }
        }
        finally
        {
            _deferEmptyEntryRemoval = false;
            RemoveEmptyEntries();
        }
    }

    public void CancelBufferReflow(int activeLinkId)
    {
        if (activeLinkId != 0 &&
            _entriesByLinkId.TryGetValue(activeLinkId, out OscLinkEntry? activeEntry) &&
            activeEntry.Lines.Count == 0)
        {
            TerminalBuffer activeBuffer = _getBuffer();
            AddMarker(activeEntry, activeBuffer, activeBuffer.YBase + activeBuffer.CursorY);
        }
        _deferEmptyEntryRemoval = false;
        RemoveEmptyEntries();
    }

    private OscLinkEntry CreateEntry(OscLinkData data, string? key, int line)
    {
        var entry = new OscLinkEntry
        {
            LinkId = _nextId++,
            Data = data,
            Key = key
        };
        _entriesByLinkId.Add(entry.LinkId, entry);
        AddMarker(entry, _getBuffer(), line);
        return entry;
    }

    private void AddMarker(OscLinkEntry entry, TerminalBuffer buffer, int line)
    {
        TerminalMarker marker = buffer.AddMarker(line);
        entry.Lines.Add(marker);
        _markerBuffers.Add(marker, buffer);
        marker.Disposed += (_, _) => RemoveMarker(entry, marker);
    }

    private void RemoveMarker(OscLinkEntry entry, TerminalMarker marker)
    {
        _markerBuffers.Remove(marker);
        if (!entry.Lines.Remove(marker) || entry.Lines.Count != 0 || _deferEmptyEntryRemoval)
        {
            return;
        }
        RemoveEntry(entry);
    }

    private void RestoreMarkersForReferencedCells(TerminalBuffer buffer)
    {
        for (int lineIndex = 0; lineIndex < buffer.LineCount; lineIndex++)
        {
            BufferLine line = buffer.GetLine(lineIndex);
            HashSet<int>? referencedIds = null;
            for (int column = 0; column < line.Length; column++)
            {
                int linkId = line.GetCell(column).Style.HyperlinkId;
                if (linkId != 0)
                {
                    (referencedIds ??= []).Add(linkId);
                }
            }
            if (referencedIds is null)
            {
                continue;
            }
            foreach (int linkId in referencedIds)
            {
                if (!_entriesByLinkId.TryGetValue(linkId, out OscLinkEntry? entry) ||
                    entry.Lines.Any(marker =>
                        _markerBuffers.TryGetValue(marker, out TerminalBuffer? markerBuffer) &&
                        ReferenceEquals(markerBuffer, buffer) &&
                        marker.Line == lineIndex))
                {
                    continue;
                }
                AddMarker(entry, buffer, lineIndex);
            }
        }
    }

    private void RemoveEmptyEntries()
    {
        foreach (OscLinkEntry entry in _entriesByLinkId.Values.Where(entry => entry.Lines.Count == 0).ToArray())
        {
            RemoveEntry(entry);
        }
    }

    private void RemoveEntry(OscLinkEntry entry)
    {
        if (entry.Key is not null)
        {
            _entriesWithId.Remove(entry.Key);
        }
        _entriesByLinkId.Remove(entry.LinkId);
    }

    private static string CreateKey(string id, string uri) => string.Concat(id, ";;", uri);
}

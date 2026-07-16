namespace XtermSharp.Internal;

internal sealed record OscLinkData(string Uri, string? Id = null);

/// <summary>Tracks OSC 8 hyperlink metadata for every buffer line on which it appears.</summary>
internal sealed class OscLinkService
{
    private sealed class LinkEntry
    {
        public required int LinkId { get; init; }
        public required OscLinkData Data { get; init; }
        public string? Key { get; init; }
        public List<TerminalMarker> Lines { get; } = [];
    }

    private readonly Func<TerminalBuffer> _getBuffer;
    private readonly Dictionary<string, LinkEntry> _entriesWithId = new(StringComparer.Ordinal);
    private readonly Dictionary<int, LinkEntry> _entriesByLinkId = [];
    private int _nextId = 1;

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
            LinkEntry anonymous = CreateEntry(data, null, line);
            return anonymous.LinkId;
        }

        string key = CreateKey(data.Id, data.Uri);
        if (_entriesWithId.TryGetValue(key, out LinkEntry? existing))
        {
            AddLineToLink(existing.LinkId, line);
            return existing.LinkId;
        }

        LinkEntry entry = CreateEntry(data, key, line);
        _entriesWithId.Add(key, entry);
        return entry.LinkId;
    }

    public void AddLineToLink(int linkId, int line)
    {
        if (!_entriesByLinkId.TryGetValue(linkId, out LinkEntry? entry) ||
            entry.Lines.Any(marker => marker.Line == line))
        {
            return;
        }
        AddMarker(entry, line);
    }

    public OscLinkData? GetLinkData(int linkId) =>
        _entriesByLinkId.TryGetValue(linkId, out LinkEntry? entry) ? entry.Data : null;

    private LinkEntry CreateEntry(OscLinkData data, string? key, int line)
    {
        var entry = new LinkEntry
        {
            LinkId = _nextId++,
            Data = data,
            Key = key
        };
        _entriesByLinkId.Add(entry.LinkId, entry);
        AddMarker(entry, line);
        return entry;
    }

    private void AddMarker(LinkEntry entry, int line)
    {
        TerminalMarker marker = _getBuffer().AddMarker(line);
        entry.Lines.Add(marker);
        marker.Disposed += (_, _) => RemoveMarker(entry, marker);
    }

    private void RemoveMarker(LinkEntry entry, TerminalMarker marker)
    {
        if (!entry.Lines.Remove(marker) || entry.Lines.Count != 0)
        {
            return;
        }
        if (entry.Key is not null)
        {
            _entriesWithId.Remove(entry.Key);
        }
        _entriesByLinkId.Remove(entry.LinkId);
    }

    private static string CreateKey(string id, string uri) => string.Concat(id, ";;", uri);
}

namespace XtermSharp.Internal.Services.Links;

/// <summary>Tracks OSC 8 hyperlink metadata for every buffer line on which it appears.</summary>
internal sealed class OscLinkService
{
    private readonly Func<TerminalBuffer> _getBuffer;
    private readonly Dictionary<string, OscLinkEntry> _entriesWithId = new(StringComparer.Ordinal);
    private readonly Dictionary<int, OscLinkEntry> _entriesByLinkId = [];
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
        if (!_entriesByLinkId.TryGetValue(linkId, out OscLinkEntry? entry) ||
            entry.Lines.Any(marker => marker.Line == line))
        {
            return;
        }
        AddMarker(entry, line);
    }

    public OscLinkData? GetLinkData(int linkId) =>
        _entriesByLinkId.TryGetValue(linkId, out OscLinkEntry? entry) ? entry.Data : null;

    private OscLinkEntry CreateEntry(OscLinkData data, string? key, int line)
    {
        var entry = new OscLinkEntry
        {
            LinkId = _nextId++,
            Data = data,
            Key = key
        };
        _entriesByLinkId.Add(entry.LinkId, entry);
        AddMarker(entry, line);
        return entry;
    }

    private void AddMarker(OscLinkEntry entry, int line)
    {
        TerminalMarker marker = _getBuffer().AddMarker(line);
        entry.Lines.Add(marker);
        marker.Disposed += (_, _) => RemoveMarker(entry, marker);
    }

    private void RemoveMarker(OscLinkEntry entry, TerminalMarker marker)
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

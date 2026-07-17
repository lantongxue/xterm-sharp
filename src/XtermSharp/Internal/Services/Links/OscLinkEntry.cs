namespace XtermSharp.Internal;

internal sealed class OscLinkEntry
{
    public required int LinkId { get; init; }
    public required OscLinkData Data { get; init; }
    public string? Key { get; init; }
    public List<TerminalMarker> Lines { get; } = [];
}

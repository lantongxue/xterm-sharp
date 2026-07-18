using System.Collections.Immutable;

namespace XtermSharp.Internal.Services.Links;

internal sealed record OscLinkData
{
    public OscLinkData(
        string uri,
        string? id = null,
        ImmutableArray<TerminalHyperlinkParameter> parameters = default)
    {
        Uri = uri;
        Id = id;
        Parameters = parameters.IsDefault ? ImmutableArray<TerminalHyperlinkParameter>.Empty : parameters;
    }

    public string Uri { get; }
    public string? Id { get; }
    public ImmutableArray<TerminalHyperlinkParameter> Parameters { get; }

    public TerminalHyperlinkMetadata ToMetadata(int linkId) => new(linkId, Uri, Id, Parameters);
}

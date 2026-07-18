using System.Collections.Immutable;

namespace XtermSharp.Links;

/// <summary>Immutable metadata for an OSC 8 hyperlink referenced by a terminal snapshot.</summary>
public sealed record TerminalHyperlinkMetadata
{
    public TerminalHyperlinkMetadata(
        int linkId,
        string uri,
        string? id = null,
        ImmutableArray<TerminalHyperlinkParameter> parameters = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(linkId);
        ArgumentNullException.ThrowIfNull(uri);
        LinkId = linkId;
        Uri = uri;
        Id = id;
        Parameters = parameters.IsDefault ? ImmutableArray<TerminalHyperlinkParameter>.Empty : parameters;
    }

    /// <summary>Gets the snapshot-local numeric ID stored by linked cells.</summary>
    public int LinkId { get; }

    /// <summary>Gets the unmodified OSC 8 URI.</summary>
    public string Uri { get; }

    /// <summary>Gets the optional explicit <c>id=</c> value.</summary>
    public string? Id { get; }

    /// <summary>Gets all well-formed key/value parameters in their original order.</summary>
    public ImmutableArray<TerminalHyperlinkParameter> Parameters { get; }

    public string? GetParameter(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        foreach (TerminalHyperlinkParameter parameter in Parameters)
        {
            if (string.Equals(parameter.Name, name, StringComparison.Ordinal))
            {
                return parameter.Value;
            }
        }
        return null;
    }
}

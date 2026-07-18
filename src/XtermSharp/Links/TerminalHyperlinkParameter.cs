namespace XtermSharp.Links;

/// <summary>An immutable OSC 8 key/value parameter.</summary>
public sealed record TerminalHyperlinkParameter
{
    public TerminalHyperlinkParameter(string name, string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        Name = name;
        Value = value;
    }

    public string Name { get; }
    public string Value { get; }
}

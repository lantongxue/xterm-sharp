namespace XtermSharp.Links;

/// <summary>A link detected within the active terminal buffer.</summary>
public sealed class TerminalLink
{
    public TerminalLink(
        TerminalLinkRange range,
        string text,
        Action<TerminalLinkEvent, string> activate)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(activate);
        Range = range;
        Text = text;
        Activate = activate;
    }

    public TerminalLinkRange Range { get; }
    public string Text { get; }
    public Action<TerminalLinkEvent, string> Activate { get; }
    public Action<TerminalLinkEvent, string>? Hover { get; set; }
    public Action<TerminalLinkEvent, string>? Leave { get; set; }
    public TerminalLinkDecorations Decorations { get; init; } = new();
}

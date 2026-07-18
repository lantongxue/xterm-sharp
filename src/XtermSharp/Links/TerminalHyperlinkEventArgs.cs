namespace XtermSharp.Links;

/// <summary>Describes pointer interaction with an OSC 8 hyperlink.</summary>
public sealed class TerminalHyperlinkEventArgs(
    TerminalLinkEvent terminalEvent,
    TerminalHyperlinkMetadata hyperlink) : EventArgs
{
    public TerminalLinkEvent TerminalEvent { get; } = terminalEvent;
    public TerminalHyperlinkMetadata Hyperlink { get; } = hyperlink ?? throw new ArgumentNullException(nameof(hyperlink));
}

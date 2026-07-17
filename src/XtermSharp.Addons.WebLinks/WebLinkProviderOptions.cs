using System.Text.RegularExpressions;

namespace XtermSharp.Addons.WebLinks;

/// <summary>Options used by <see cref="WebLinksAddon"/>.</summary>
public sealed class WebLinkProviderOptions
{
    public Action<TerminalLinkEvent, string, TerminalLinkRange>? Hover { get; set; }
    public Action<TerminalLinkEvent, string>? Leave { get; set; }
    public Regex? UrlRegex { get; set; }
}

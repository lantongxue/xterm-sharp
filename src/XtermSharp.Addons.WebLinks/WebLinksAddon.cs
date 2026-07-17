using System.Diagnostics;
using System.Text.RegularExpressions;

namespace XtermSharp.Addons.WebLinks;

/// <summary>An addon that detects and activates web links in the active terminal buffer.</summary>
public sealed class WebLinksAddon : ITerminalAddon
{
    internal static readonly Regex DefaultUrlRegex = new(
        @"(https?|HTTPS?)://[^\s""'!*(){}|\\\^<>`]*[^\s""':,.!?{}|\\\^~\[\]`()<>]",
        RegexOptions.CultureInvariant);

    private readonly Action<TerminalLinkEvent, string> _handler;
    private readonly WebLinkProviderOptions _options;
    private IDisposable? _registration;

    public WebLinksAddon(
        Action<TerminalLinkEvent, string>? handler = null,
        WebLinkProviderOptions? options = null)
    {
        _handler = handler ?? OpenLink;
        _options = options ?? new WebLinkProviderOptions();
    }

    public void Activate(Terminal terminal)
    {
        ArgumentNullException.ThrowIfNull(terminal);
        _registration?.Dispose();
        Regex regex = _options.UrlRegex ?? DefaultUrlRegex;
        _registration = terminal.RegisterLinkProvider(
            new WebLinkProvider(terminal, regex, _handler, _options));
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _registration, null)?.Dispose();
    }

    private static void OpenLink(TerminalLinkEvent terminalEvent, string uri)
    {
        _ = terminalEvent;
        Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
    }
}

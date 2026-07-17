using System.Text.RegularExpressions;

namespace XtermSharp.Addons.WebLinks;

internal sealed class WebLinkProvider(
    Terminal terminal,
    Regex regex,
    Action<TerminalLinkEvent, string> handler,
    WebLinkProviderOptions options) : ITerminalLinkProvider
{
    public async ValueTask<IReadOnlyList<TerminalLink>?> ProvideLinksAsync(
        int bufferLineNumber,
        CancellationToken cancellationToken = default)
    {
        TerminalSnapshot snapshot = await terminal.GetSnapshotAsync(
            SnapshotScope.ActiveBuffer,
            cancellationToken).ConfigureAwait(false);
        IReadOnlyList<TerminalLink> links = WebLinkComputer.ComputeLinks(
            bufferLineNumber,
            regex,
            snapshot,
            handler);
        foreach (TerminalLink link in links)
        {
            link.Leave = options.Leave;
            link.Hover = (terminalEvent, text) =>
                options.Hover?.Invoke(terminalEvent, text, link.Range);
        }
        return links;
    }
}

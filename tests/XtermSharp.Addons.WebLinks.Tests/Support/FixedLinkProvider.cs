namespace XtermSharp.Addons.WebLinks.Tests.Support;

internal sealed class FixedLinkProvider(TerminalLink link) : ITerminalLinkProvider
{
    public ValueTask<IReadOnlyList<TerminalLink>?> ProvideLinksAsync(
        int bufferLineNumber,
        CancellationToken cancellationToken = default)
    {
        _ = bufferLineNumber;
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<IReadOnlyList<TerminalLink>?>([link]);
    }
}

namespace XtermSharp.Links;

/// <summary>Provides links for a one-based line in the active terminal buffer.</summary>
public interface ITerminalLinkProvider
{
    ValueTask<IReadOnlyList<TerminalLink>?> ProvideLinksAsync(
        int bufferLineNumber,
        CancellationToken cancellationToken = default);
}

# OSC 8 hyperlinks

OSC 8 cells expose immutable metadata through `TerminalSnapshot.Hyperlinks`. The numeric
`TerminalCellSnapshot.HyperlinkId` resolves only within the same snapshot:

```csharp
TerminalSnapshot snapshot = await terminal.GetSnapshotAsync(SnapshotScope.ActiveBuffer);
TerminalCellSnapshot cell = snapshot.ActiveBuffer.Lines[0].Cells[0];
TerminalHyperlinkMetadata? hyperlink = snapshot.GetHyperlink(cell.HyperlinkId);
```

`TerminalHyperlinkMetadata` preserves the URI, optional explicit `id=` value and every well-formed
key/value OSC parameter in input order. Snapshot scopes include metadata only for cells present in
that snapshot, so an older snapshot remains internally consistent after trimming, buffer switches
or later writes.

The core terminal automatically provides OSC 8 links through `GetLinksAsync` and `GetLinkAtAsync`.
Contiguous linked cells form one range, including wrapped ranges. Explicit OSC 8 links are resolved
before registered text-detection providers, so a displayed label is activated using its OSC URI
rather than any URL-like text inside the label. `TerminalView` consumes this provider through its
normal cancellable hover pipeline and supplies underline, pointer, leave and click behavior.

## Activation security

Terminal output is untrusted. XtermSharp and `TerminalView` never open OSC 8 URIs automatically.
Activation raises `Terminal.HyperlinkActivated`; the application owns all navigation decisions and
should parse the URI, allowlist schemes and apply any host/path policy before invoking a browser or
operating-system shell:

```csharp
terminal.HyperlinkActivated += (_, args) =>
{
    if (Uri.TryCreate(args.Hyperlink.Uri, UriKind.Absolute, out Uri? uri) &&
        uri.Scheme is "https" or "http")
    {
        OpenTrustedUri(uri);
    }
};
```

`HyperlinkHovered` and `HyperlinkLeft` provide application hooks for status text or previews. Event
subscriber exceptions are isolated and logged. The optional `WebLinksAddon` has a separate default
handler that opens detected web URLs; pass a custom handler when that behavior is not appropriate.

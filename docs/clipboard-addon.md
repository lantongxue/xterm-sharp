# Clipboard addon

`XtermSharp.Addons.Clipboard` ports the pinned xterm.js 6.0.0 `addon-clipboard` OSC 52 behavior to
.NET. The addon is platform-neutral: applications supply an `IClipboardProvider`, while the
optional Avalonia and MAUI packages supply adapters for the system clipboard.

## Usage

Clipboard access is denied by default. Enable only the operations that the terminal session needs:

```csharp
using XtermSharp.Addons.Clipboard;

var addon = new ClipboardAddon(provider, new ClipboardAddonOptions
{
    AllowRead = false,
    AllowWrite = true
});
terminal.LoadAddon(addon);
```

OSC 52 writes decode strict Base64 as UTF-8 and pass the resulting text to the provider. An empty
payload or `!` explicitly clears the clipboard. Queries read provider text, encode it as UTF-8
Base64 and send `ESC ] 52 ; <selection> ; <payload> BEL` toward the backing session in the same
ordered terminal commit.

The supported selection parameter is empty or a combination of `c`, `p`, `q`, `s` and `0` through
`7`. Providers that expose only one system clipboard may ignore the selection, as the Avalonia
provider does.

## Avalonia provider

Create the provider from the view's top-level clipboard on the Avalonia UI thread:

```csharp
using XtermSharp.Addons.Clipboard;
using XtermSharp.Avalonia.Clipboard;
using Avalonia.Controls;
using Avalonia.Input.Platform;

IClipboard clipboard = TopLevel.GetTopLevel(view)!.Clipboard!;
var provider = new AvaloniaClipboardProvider(clipboard);
var addon = new ClipboardAddon(provider, new ClipboardAddonOptions
{
    AllowWrite = true
});
terminal.LoadAddon(addon);
```

`AvaloniaClipboardProvider` dispatches clipboard calls to the Avalonia UI dispatcher. It maps all
OSC 52 selections to the platform clipboard because Avalonia does not expose separate primary or
secondary selections.

## .NET MAUI provider

`MauiClipboardProvider` accepts the MAUI `IClipboard` service and an optional UI dispatcher.
When no clipboard is supplied it uses `Clipboard.Default`:

```csharp
using XtermSharp.Addons.Clipboard;
using XtermSharp.Maui.Clipboard;

var provider = new MauiClipboardProvider();
var addon = new ClipboardAddon(provider, new ClipboardAddonOptions
{
    AllowWrite = true
});
terminal.LoadAddon(addon);
```

Like the Avalonia adapter, it maps every OSC 52 selection to the one system clipboard exposed by
the platform.

## Security policy

OSC 52 content is controlled by applications running in the terminal, including remote processes
over SSH. Read permission can disclose passwords, tokens or other host clipboard content to that
process. Write permission can replace clipboard content and support clipboard-confusion attacks.

Keep both permissions disabled for untrusted sessions. Prefer write-only access when applications
only need to copy text; enable reads only when clipboard query support is explicitly required.
Permissions are independent and must be opted into for each addon instance.

Decoded UTF-8 payloads are limited to 1 MiB by default. `MaxPayloadBytes` can lower or raise that
limit. Oversized payloads, invalid selections, invalid Base64 and invalid UTF-8 are consumed without
calling the provider or disturbing subsequent parser input.

These controls deliberately harden the pinned upstream behavior. Upstream has no addon-level
permission or payload limit, forwards arbitrary selection strings, replaces malformed UTF-8 and
clears the clipboard for arbitrary invalid Base64. XtermSharp requires explicit permissions,
validates selections, rejects malformed UTF-8 and rejects invalid Base64 without changing host
state. Empty payloads and `!` remain explicit clear operations.

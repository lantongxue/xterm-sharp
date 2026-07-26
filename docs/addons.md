---
layout: doc
title: Addon overview
description: Extend XtermSharp with URL detection, buffer search, progress state, and explicit clipboard policy.
category: Addons
search: true
permalink: /addons/
---

# Addon overview

Addons attach optional behavior to a terminal without adding UI or graphics dependencies to the core.
Load an addon into the same externally owned terminal used by a renderer or native control.

## Load an addon

```csharp
using XtermSharp.Addons.Search;

var search = new SearchAddon();
terminal.LoadAddon(search);

search.FindNext("error");
```

Dispose the addon when its behavior is no longer needed. Handler registration and event subscriptions
are removed with the addon lifecycle.

## Web links

`XtermSharp.Addons.WebLinks` detects validated URLs in the active buffer, including text spanning
wrapped lines and ranges adjacent to wide or combined cells.

```csharp
using XtermSharp.Addons.WebLinks;

terminal.LoadAddon(new WebLinksAddon());
```

Applications can replace activation, hover, leave, and URL matching behavior. The detailed
[web links guide](web-links-addon.md) covers custom callbacks and headless link queries.

## Search

`XtermSharp.Addons.Search` provides forward and reverse literal or regex search over the latest
committed active-buffer snapshot. It supports case sensitivity, whole words, incremental search,
selection, active-result tracking, and backend-neutral match decorations.

Search itself remains headless. Renderers consume the decoration provider automatically when match
highlighting is enabled. See the [search guide](search-addon.md).

## Progress

`XtermSharp.Addons.Progress` parses ConEmu OSC 9;4 sequences and exposes normalized `Remove`, `Set`,
`Error`, `Indeterminate`, and `Pause` state. It does not decide how an application displays progress.

Use its change event to update an application-owned taskbar, badge, or progress control. See the
[progress guide](progress-addon.md) for state preservation rules.

## Clipboard

`XtermSharp.Addons.Clipboard` implements OSC 52 with explicit read and write permissions, strict UTF-8
Base64 processing, and payload limits. Access is denied by default.

```csharp
var addon = new ClipboardAddon(provider, new ClipboardAddonOptions
{
    AllowRead = false,
    AllowWrite = true
});
terminal.LoadAddon(addon);
```

Prefer write-only access unless a trusted terminal application requires queries. The
[clipboard addon guide](clipboard-addon.md) documents providers and security policy.

## OSC 8 needs no addon

Explicit OSC 8 hyperlinks are part of the core terminal model. Snapshot-scoped metadata and the
built-in link provider preserve the URI and parameters, but XtermSharp never opens the URI
automatically. Applications validate and handle `HyperlinkActivated`; see
[OSC 8 hyperlinks](osc-hyperlinks.md).

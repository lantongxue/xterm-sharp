# Web links addon

`XtermSharp.Addons.WebLinks` ports the pinned xterm.js 6.0.0 `addon-web-links` behavior to .NET.
It detects validated URLs in the active buffer, including links that wrap across rows and links
surrounded by wide or combining characters.

Reference the addon package and load it into the same externally owned terminal used by the view:

```csharp
using XtermSharp;
using XtermSharp.Addons.WebLinks;

var terminal = new Terminal();
terminal.LoadAddon(new WebLinksAddon());
```

The default activation handler opens the URL through the operating-system shell. Applications can
provide their own handler and callbacks:

```csharp
var options = new WebLinkProviderOptions
{
    Hover = (_, uri, range) => ShowTooltip(uri, range),
    Leave = (_, uri) => HideTooltip(uri),
    UrlRegex = new Regex(@"https://docs\.example\.com/[^\s]+")
};

terminal.LoadAddon(new WebLinksAddon(
    (_, uri) => NavigateInsideApplication(uri),
    options));
```

`TerminalView` resolves registered providers without blocking the terminal processor task. Hovered
links receive an underline and pointer cursor, and activation occurs only when pointer press and
release target the same link. Headless consumers can use `Terminal.GetLinksAsync` or
`Terminal.GetLinkAtAsync` directly.

Link positions and ranges are one-based active-buffer coordinates with an inclusive end, matching
the upstream addon implementation and tests.

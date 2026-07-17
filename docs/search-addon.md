# Search addon

`XtermSharp.Addons.Search` ports the pinned xterm.js 6.0.0 `addon-search` behavior to .NET. It
searches the latest committed active-buffer snapshot, selects the current result and can decorate
all matches without introducing a rendering dependency into the addon package.

Reference the addon package and load one instance into the terminal:

```csharp
using XtermSharp;
using XtermSharp.Addons.Search;
using XtermSharp.Snapshots;

var search = new SearchAddon();
terminal.LoadAddon(search);

search.FindNext("error", new SearchOptions
{
    CaseSensitive = false,
    WholeWord = true,
    Decorations = new SearchDecorationOptions
    {
        MatchBackground = TerminalColor.Rgb(80, 50, 0),
        ActiveMatchBackground = TerminalColor.Rgb(0, 80, 140),
        ActiveMatchBorder = TerminalColor.Rgb(120, 200, 255)
    }
});
```

`FindNext` and `FindPrevious` are synchronous because they search the immutable latest committed
snapshot. If a result is outside the viewport, the addon schedules the existing ordered terminal
scroll operation. Searches support literal or regular-expression matching, case sensitivity,
whole-word boundaries, incremental selection expansion, wrapped rows, wide cells and UTF-16
surrogate pairs.

With `SearchOptions.Decorations` set, all matches are decorated up to
`SearchAddonOptions.HighlightLimit` (1000 by default). Bottom-layer match backgrounds render below
the selection, while the active match uses the top layer. `ResultsChanged` reports the zero-based
active result index and capped result count. `BeforeSearch` and `AfterSearch` bracket explicit
searches.

Writes and resizes schedule a 200 ms debounced recomputation when decorated search state is active.
`ClearActiveDecoration` reveals the selection underneath the active match, while
`ClearDecorations` removes match and active decorations and resets the cached search term.

Overview-ruler colors are preserved in the platform-neutral decoration metadata. The current
Avalonia control has no overview-ruler surface, so it renders match backgrounds and borders only;
other adapters can consume the overview metadata directly.

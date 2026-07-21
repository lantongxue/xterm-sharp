# Code organization

XtermSharp uses one C# type per file, including internal command, cache, state and test-helper
types. File names match their contained type, while directories group related responsibilities.
Namespaces follow the project root namespace plus the physical folder path; for example,
`src/XtermSharp/Options/TerminalOptions.cs` declares `XtermSharp.Options.TerminalOptions`.

The repository enables Roslyn `IDE0130` and Rider/ReSharper `CheckNamespace` in `.editorconfig` so
future file moves must keep namespaces and references synchronized. This convention intentionally
changes the pre-0.1 public namespaces introduced before the responsibility-based directory layout.

## Core package

```text
src/XtermSharp/
├── Addons/                 Public addon contracts
├── Decorations/            Platform-neutral decoration providers and ranges
├── Events/                 Terminal event arguments and color requests
├── Input/                  Platform-neutral keyboard and mouse contracts
├── Links/                  Public link providers, ranges, events and decorations
├── Logging/                Logging contracts and the null logger
├── Markers/                Terminal markers
├── Options/                Construction and runtime option models
├── Parsing/                Public parser contracts and CSI parameters
├── Snapshots/              Immutable terminal, buffer, line and cell snapshots
├── Selection/              Platform-neutral selection ranges
├── Unicode/                Unicode providers and registry
└── Internal/
    ├── Buffers/            Mutable cells, lines, buffers, ranges and reflow
    ├── Colors/             Internal color parsing
    ├── Commands/           Ordered terminal command models
    ├── Concurrency/        Queue admission and pending-byte limiting
    ├── Decoding/           Streaming UTF-8 and UTF-16 decoding
    ├── Engine/             Terminal execution and committed engine events
    ├── Input/              Keyboard encoders and write queue models
    ├── Parser/             VT state machine, handlers and parser state types
    ├── Services/           Buffer, charset, hyperlink, mouse and option services
    └── Utilities/          Collections, disposables, events and text helpers
```

## Rendering and platform packages

```text
src/XtermSharp.Rendering/
├── Backends/               Backend contracts
├── Colors/                 Renderer color values
├── Configuration/          Render options and resolved configuration
├── Controllers/            Snapshot-to-frame orchestration
├── Display/                Backend-neutral draw commands and frames
├── Geometry/               Points, sizes, rectangles and font metrics
├── Selection/              Selection models
└── Themes/                 Terminal themes and palettes

src/XtermSharp.Rendering.Skia/
└── Backends/               SkiaSharp/HarfBuzz implementation

src/XtermSharp.Addons.WebLinks/
└── WebLinksAddon           Upstream-compatible URL detection and link provider

src/XtermSharp.Addons.Search/
├── SearchAddon             Upstream-compatible search lifecycle and public API
├── SearchEngine            Wrapped-line and UTF-16-aware forward/reverse matching
└── SearchDecorationProvider Backend-neutral result and active-match decorations

src/XtermSharp.Addons.Progress/
└── ProgressAddon           Upstream-compatible OSC 9;4 state tracking and notifications

src/XtermSharp.Addons.Clipboard/
├── ClipboardAddon          Policy-controlled OSC 52 parser integration
├── ClipboardBase64         Strict UTF-8 Base64 codec
└── IClipboardProvider      Platform-neutral clipboard boundary

src/XtermSharp.Avalonia/
├── Clipboard/              Avalonia system-clipboard provider
├── Controls/               TerminalView
├── Diagnostics/            Rendering metrics and overlay
└── Input/                  Avalonia keyboard and IME adapters
```

Samples separate application startup, views, models, services, events and exceptions. Test support
separates manifest models from binding discovery, while parser test helpers and string-sequence
suites use dedicated subdirectories. `tests/XtermSharp.Addons.WebLinks.Tests/` covers the upstream
addon cases plus the core provider and backend-neutral hover-decoration integration.
`tests/XtermSharp.Addons.Search.Tests/` covers search cycling, regex and incremental options,
wide/wrapped mapping, the upstream issue-2444 fixture, result tracking, debounce and display-list
decoration ordering. `tests/XtermSharp.Addons.Progress.Tests/` covers every pinned upstream progress
case plus programmatic state and handler lifecycle behavior.
`tests/XtermSharp.Addons.Clipboard.Tests/` covers every pinned upstream clipboard behavior plus
permissions, payload limits, invalid input, cancellation and handler lifecycle.

The pinned `xterm.js/` reference tree is intentionally excluded from this convention and must not
be reorganized locally.

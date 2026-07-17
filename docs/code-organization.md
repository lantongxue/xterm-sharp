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
├── Events/                 Terminal event arguments and color requests
├── Input/                  Platform-neutral keyboard and mouse contracts
├── Logging/                Logging contracts and the null logger
├── Markers/                Terminal markers
├── Options/                Construction and runtime option models
├── Parsing/                Public parser contracts and CSI parameters
├── Snapshots/              Immutable terminal, buffer, line and cell snapshots
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

src/XtermSharp.Avalonia/
├── Controls/               TerminalView
├── Diagnostics/            Rendering metrics and overlay
└── Input/                  Avalonia keyboard and IME adapters
```

Samples separate application startup, views, models, services, events and exceptions. Test support
separates manifest models from binding discovery, while parser test helpers and string-sequence
suites use dedicated subdirectories.

The pinned `xterm.js/` reference tree is intentionally excluded from this convention and must not
be reorganized locally.

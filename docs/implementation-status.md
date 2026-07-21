# Implementation status

## Implemented in 0.1 alpha

- Ordered async writes, resize, reset, clear, scrolling and user-input events.
- Configurable pending-input backpressure with cancellation before admission.
- Streaming UTF-8 and UTF-16 input across write boundaries.
- Normal and alternate buffers, scrollback, delayed wrap and complex-cell resize reflow verified
  against the pinned xterm.js oracle across shrink/grow, cursor-line and scrollback scenarios.
- C0 controls and common ESC/CSI commands for cursor movement, insertion,
  deletion, erase, scrolling, tabs and scroll regions.
- SGR flags, 16/256-color palette values, true color and underline color.
- DEC private modes for cursor, origin, wrapping, alternate screen, mouse
  tracking, focus, bracketed paste and synchronized output.
- OSC title handling plus snapshot-scoped immutable OSC 8 URI, explicit ID and parameter metadata.
- DSR status/cursor responses through the `Data` event.
- Immutable viewport/full-buffer snapshots with cell attributes.
- Custom CSI, ESC, OSC, DCS and APC handlers, including asynchronous handlers.
- A single production VT500 parser with SOS/PM, C1-anywhere transitions, bounded
  OSC/DCS/APC payloads and strict handler failure recovery.
- Effective two-column minimum sizing with a safe low-level fallback for unsupported
  one-column buffers containing wide cells.
- Addon lifecycle, Unicode 6, exact generated Unicode 11, exact generated Unicode 15 width and
  UAX #29 extended-grapheme providers, plus the compatibility .NET grapheme-oriented provider.
- Stable link-provider contracts plus the optional `XtermSharp.Addons.WebLinks`
  port, including strict URL validation, wrapped/wide/combined-cell range mapping,
  custom activation/hover/leave callbacks and Avalonia hover/click interaction.
- Platform-neutral core selection and decoration-provider contracts plus the optional
  `XtermSharp.Addons.Search` port, including forward/reverse literal and regex search,
  case/whole-word/incremental options, wrapped/wide/UTF-16 coordinate mapping, capped result
  highlighting, active-result tracking and debounced updates after writes or resize. Overview-ruler
  colors are exposed as decoration metadata; the current Avalonia adapter has no overview-ruler
  surface.
- The optional `XtermSharp.Addons.Progress` port tracks ConEmu OSC 9;4 remove, percentage, error,
  indeterminate and pause states through the public production parser. It strictly validates
  decimal payloads, clamps percentages, preserves prior values where upstream does, exposes
  programmatic reset/restore and unregisters cleanly on disposal.
- The optional `XtermSharp.Addons.Clipboard` port handles OSC 52 query, set and explicit clear
  operations through a platform-neutral provider. Read and write permissions default to denied,
  decoded UTF-8 payloads are bounded, malformed input is rejected without changing clipboard
  state, and `XtermSharp.Avalonia` supplies a UI-dispatched system clipboard adapter.
- The pinned xterm.js 6.0.0 inventory contains 1,361 concrete upstream cases:
  54 front-end renderer cases are explicitly excluded, while all 1,307
  headless-applicable cases are covered by C# tests (1,306 direct ports and one
  documented architecture-equivalent streaming test).
- All 76 upstream escape-sequence fixtures run in xUnit and are differentially
  checked against the pinned xterm.js headless build.
- Fourteen permanent complex-cell resize/reflow scenarios differentially verify ASCII, wide,
  combined, grapheme, styled, protected and hyperlink cells, including cursor-line suppression,
  delayed wrap, orphan continuations and viewport/scrollback positioning.
- Seven permanent marker and metadata scenarios differentially verify physical-row mapping during
  shrink/grow, discarded-row disposal, scrollback trimming, line insertion/deletion, cursor-line
  suppression and OSC 8 lifetime. Search decorations are recomputed against the reflowed layout.
- A built-in OSC 8 link provider maps contiguous and wrapped cell ranges. `TerminalView` uses the
  normal cancellable link pipeline for hover, leave and activation; activation only raises an
  application event and never launches an untrusted terminal-provided URI automatically.
- Manifest generation and auditing enforce unique upstream-to-C# bindings with
  no pending applicable cases.
- Ordered `PasteAsync` and focus reporting APIs keep UI protocol decisions on
  the terminal processor queue; runtime option changes carry committed revisions.
- Public construction and runtime-update models cover cursor-line reflow, Windows PTY backend/build
  compatibility, stdin suppression and Kitty SGR 221/222 control with upstream defaults. Nested
  option objects are cloned before publication, and each update raises one committed event.
- Unchanged buffer lines reuse immutable line snapshots, avoiding repeated cell
  materialization for full-buffer publication.
- `XtermSharp.Rendering` provides backend-neutral frame coordination, themes,
  selection extraction, layered addon decorations, synchronized-output throttling, damage tracking
  and batched text/background display-list runs.
- `XtermSharp.Rendering.Skia` provides SkiaSharp 3.119.4 and HarfBuzz shaping,
  font fallback, cached fonts/paints and worker-prepared retained row pictures.
- `XtermSharp.Avalonia` provides an externally bound `TerminalView` with DPI-aware
  resizing, worker-side frame preparation, change-only binding notifications, keyboard/mouse
  protocols, local selection, clipboard, focus, IME preedit, registered-link interaction and an
  optional rendering telemetry overlay.
- `XtermSharp.Maui` provides an externally bound `TerminalView` that reuses the SkiaSharp/HarfBuzz
  backend through `SKCanvasView`. It supports device-pixel scaling, resize, touch selection,
  tracked mouse input, link activation, soft-keyboard text/backspace/enter input, scrolling and
  clipboard integration.
- The no-PTY Avalonia demo loads both optional addons and exposes interactive link activation plus
  case-sensitive, whole-word and regex search controls with previous/next result navigation.
- The MAUI SSH demo targets Android, iOS and Mac Catalyst and compiles the same transport source as
  the Avalonia SSH demo for identical authentication, host-key verification and PTY behavior.

## Still required before 1.0

The prioritized implementation and test acceptance criteria are maintained in the
[upstream parity acceptance checklist](upstream-parity-acceptance-checklist.md).

- Keep the manifest and differential oracle synchronized when the pinned
  xterm.js baseline is upgraded.
- Extend differential coverage beyond the current reference scenarios and
  complex-cell reflow, marker/metadata and escape-sequence fixture corpora.
- Add fuzzing for parser chunk boundaries and benchmark-driven packed-cell storage.
- Add WPF/WinUI controls, native Windows rendering backends, accessibility,
  mutable link-decoration notifications and renderer-specific differential fixtures.

Any intentional behavioral difference from xterm.js must be recorded here before
a stable release.

Current intentional difference: after grow-reflow removes the only physical row marker for an OSC
8 link but moves linked cells into a retained row, XtermSharp rebuilds the marker from the surviving
cell references. The pinned xterm.js baseline drops the metadata while leaving cells with the link
ID; XtermSharp preserves it to maintain resolvable buffer state.

The pinned `addon-clipboard` has no addon-level permissions or decoded payload limit, forwards
arbitrary selection strings, replaces malformed UTF-8 and clears the clipboard when an OSC 52
payload contains arbitrary invalid Base64. XtermSharp requires explicit permissions, limits decoded
payloads, validates selections and rejects malformed UTF-8 or Base64 without invoking the provider
because malformed terminal input should not mutate host clipboard state. Empty payloads and `!`
remain explicit clear operations.

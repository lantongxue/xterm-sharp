# Implementation status

## Implemented in 0.1 alpha

- Ordered async writes, resize, reset, clear, scrolling and user-input events.
- Configurable pending-input backpressure with cancellation before admission.
- Streaming UTF-8 and UTF-16 input across write boundaries.
- Normal and alternate buffers, scrollback, delayed wrap and basic resize reflow.
- C0 controls and common ESC/CSI commands for cursor movement, insertion,
  deletion, erase, scrolling, tabs and scroll regions.
- SGR flags, 16/256-color palette values, true color and underline color.
- DEC private modes for cursor, origin, wrapping, alternate screen, mouse
  tracking, focus, bracketed paste and synchronized output.
- OSC title and OSC 8 hyperlink tracking.
- DSR status/cursor responses through the `Data` event.
- Immutable viewport/full-buffer snapshots with cell attributes.
- Custom CSI, ESC, OSC, DCS and APC handlers, including asynchronous handlers.
- A single production VT500 parser with SOS/PM, C1-anywhere transitions, bounded
  OSC/DCS/APC payloads and strict handler failure recovery.
- Effective two-column minimum sizing with a safe low-level fallback for unsupported
  one-column buffers containing wide cells.
- Addon lifecycle and Unicode 6, Unicode 11 and .NET grapheme-oriented providers.
- Stable link-provider contracts plus the optional `XtermSharp.Addons.WebLinks`
  port, including strict URL validation, wrapped/wide/combined-cell range mapping,
  custom activation/hover/leave callbacks and Avalonia hover/click interaction.
- Platform-neutral core selection and decoration-provider contracts plus the optional
  `XtermSharp.Addons.Search` port, including forward/reverse literal and regex search,
  case/whole-word/incremental options, wrapped/wide/UTF-16 coordinate mapping, capped result
  highlighting, active-result tracking and debounced updates after writes or resize. Overview-ruler
  colors are exposed as decoration metadata; the current Avalonia adapter has no overview-ruler
  surface.
- The pinned xterm.js 6.0.0 inventory contains 1,361 concrete upstream cases:
  54 front-end renderer cases are explicitly excluded, while all 1,307
  headless-applicable cases are covered by C# tests (1,306 direct ports and one
  documented architecture-equivalent streaming test).
- All 76 upstream escape-sequence fixtures run in xUnit and are differentially
  checked against the pinned xterm.js headless build.
- Manifest generation and auditing enforce unique upstream-to-C# bindings with
  no pending applicable cases.
- Ordered `PasteAsync` and focus reporting APIs keep UI protocol decisions on
  the terminal processor queue; runtime option changes carry committed revisions.
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
- The no-PTY Avalonia demo loads both optional addons and exposes interactive link activation plus
  case-sensitive, whole-word and regex search controls with previous/next result navigation.

## Still required before 1.0

- Keep the manifest and differential oracle synchronized when the pinned
  xterm.js baseline is upgraded.
- Extend differential coverage beyond the current reference scenarios and
  escape-sequence fixture corpus.
- Match xterm.js reflow behavior for every wide/combined/styled-cell edge case.
- Implement complete Unicode 11 tables and full extended grapheme clustering.
- Add marker tracking through scroll/reflow and richer hyperlink metadata APIs.
- Add fuzzing for parser chunk boundaries and benchmark-driven packed-cell storage.
- Add WPF/WinUI controls, native Windows rendering backends, accessibility,
  OSC 8 interaction, mutable link-decoration notifications and renderer-specific differential fixtures.

Any intentional behavioral difference from xterm.js must be recorded here before
a stable release.

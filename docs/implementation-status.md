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
- The pinned xterm.js 6.0.0 inventory contains 1,361 concrete upstream cases:
  54 front-end renderer cases are explicitly excluded, while all 1,307
  headless-applicable cases are covered by C# tests (1,306 direct ports and one
  documented architecture-equivalent streaming test).
- All 76 upstream escape-sequence fixtures run in xUnit and are differentially
  checked against the pinned xterm.js headless build.
- Manifest generation and auditing enforce unique upstream-to-C# bindings with
  no pending applicable cases.

## Still required before 1.0

- Keep the manifest and differential oracle synchronized when the pinned
  xterm.js baseline is upgraded.
- Extend differential coverage beyond the current reference scenarios and
  escape-sequence fixture corpus.
- Match xterm.js reflow behavior for every wide/combined/styled-cell edge case.
- Implement complete Unicode 11 tables and full extended grapheme clustering.
- Add marker tracking through scroll/reflow and richer hyperlink metadata APIs.
- Add fuzzing for parser chunk boundaries and benchmark-driven packed-cell storage.

Any intentional behavioral difference from xterm.js must be recorded here before
a stable release.

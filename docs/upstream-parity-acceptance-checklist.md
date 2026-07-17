# Upstream parity acceptance checklist

This document is the implementation and acceptance backlog for capabilities that remain outside
XtermSharp's verified xterm.js common/headless test inventory. It is intended to be updated as work
is implemented and accepted, rather than serving only as a point-in-time status report.

## Baseline

- Audit date: 2026-07-17
- Upstream: xterm.js 6.0.0
- Commit: `b1aee19ac6d6f4e4d11e4a10a3731b852956bdb7`
- XtermSharp package version: `0.1.0-alpha.1`

The verified common/headless inventory has no pending applicable cases. This checklist covers
capabilities that are incomplete, represented only by an architectural approximation, excluded
from the inventory, or provided by upstream addons that have not been fully ported.

### Verified starting point

- [x] `tests/upstream-tests.json` contains 1,361 discovered cases.
- [x] 54 front-end renderer cases are excluded from the headless inventory.
- [x] All 1,307 applicable cases have unique C# bindings.
- [x] 1,306 cases are direct ports and `XTJS-0799` is architecture-equivalent.
- [x] The reference scenario reports `MATCH`.
- [x] All 76 escape-sequence fixtures report `MATCH 76/76`.
- [x] The solution test run passes 1,475/1,475 tests across all seven test projects.

## Priority definitions

| Priority | Meaning |
| --- | --- |
| P0 | Terminal-state correctness or public headless compatibility required before stable 1.0. |
| P1 | High-value upstream compatibility required for a broadly usable desktop terminal. |
| P2 | Advanced rendering, protocol or browser-API parity that may ship after the core is stable. |

## Overview

| ID | Priority | Capability | Status |
| --- | --- | --- | --- |
| UPG-001 | P0 | Complete Unicode 11 width tables | Ready for acceptance |
| UPG-002 | P0 | Unicode 15 extended grapheme clustering | Ready for acceptance |
| UPG-003 | P0 | Complex-cell resize/reflow parity | Not started |
| UPG-004 | P0 | Marker, hyperlink and decoration tracking through reflow | Not started |
| UPG-005 | P0 | Public OSC 8 metadata and interaction | Not started |
| UPG-006 | P0 | Remaining public headless option plumbing | Not started |
| UPG-007 | P1 | Renderer color and minimum-contrast behavior | Not started |
| UPG-008 | P1 | Decoration lifecycle and overview-ruler parity | Not started |
| UPG-009 | P1 | Avalonia accessibility and screen-reader support | Not started |
| UPG-010 | P1 | Progress addon | Not started |
| UPG-011 | P1 | Clipboard/OSC 52 addon | Not started |
| UPG-012 | P1 | Serialize addon | Not started |
| UPG-013 | P2 | Inline image addon | Not started |
| UPG-014 | P2 | Ligatures and character joiners | Not started |
| UPG-015 | P2 | Remaining interactive terminal API and option parity | Not started |

## P0: terminal correctness and headless compatibility

### UPG-001: Complete Unicode 11 width tables

Implemented state: `UnicodeV11Provider` consumes generated range data extracted from the pinned
xterm.js Unicode 11 provider. The generation check pins the upstream version, commit and source
hash so behavior does not depend on the installed .NET Unicode data.

- [x] Replace the approximate ranges with generated or directly ported Unicode 11 width data.
- [x] Preserve deterministic Unicode 11 behavior independently of the installed .NET Unicode data.
- [x] Cover every valid Unicode scalar value with a differential comparison against the pinned
      `addon-unicode11` provider.
- [x] Port the upstream addon tests into a dedicated C# test project or clearly identified suite.
- [x] Add regression cases for combining characters and symbols near every range boundary.
- [x] Document the data source, Unicode version and regeneration process.
- [x] Run the complete verification matrix with no regression.

Acceptance result: _Ready for user acceptance. Automated verification completed 2026-07-17:
1,112,064 Unicode scalars verified, solution build completed with zero warnings/errors, and
1,475/1,475 tests passed._

### UPG-002: Unicode 15 extended grapheme clustering

Implemented state: `UnicodeV15Provider` exposes the upstream-compatible `15` and `15-graphemes`
names. Generated data combines the pinned addon width trie with official Unicode 15.0 grapheme
break and extended-pictographic properties, and the stateful provider implements UAX #29 cluster
boundaries across parser calls.

- [x] Implement providers equivalent to upstream `15` and `15-graphemes`, or document a deliberate
      public naming difference.
- [x] Use version-pinned grapheme properties rather than relying solely on adjacent rune categories.
- [x] Pass the upstream `addon-unicode-graphemes` test suite.
- [x] Validate against the official Unicode grapheme break test data for the selected Unicode version.
- [x] Test clusters split across string writes, UTF-8 writes and UTF-16 surrogate boundaries.
- [x] Test REP, wrapping, reflow, selection and search with multi-code-point clusters. Serialization
      coverage is deferred to UPG-012 because the serialize addon does not yet exist.
- [x] Verify that cluster width and cell ownership remain valid after resize in both directions.
- [x] Run the complete verification matrix with no regression.

Acceptance result: _Ready for user acceptance. Automated verification completed 2026-07-17:
1,112,064 Unicode scalars and all 602 official Unicode 15.0 grapheme-break rows verified, solution
build completed with zero warnings/errors, and 1,482/1,482 tests passed._

### UPG-003: Complex-cell resize/reflow parity

Current state: the mapped upstream reflow cases pass, but the project still explicitly calls out
wide, combined and styled-cell edge cases as incomplete hardening work.

- [ ] Add differential scenarios covering ASCII, wide, combining, grapheme, styled, protected and
      hyperlink cells.
- [ ] Exercise shrink and grow operations with the viewport at the bottom and inside scrollback.
- [ ] Cover cursor-line reflow enabled and disabled, delayed wrap and logical `x == cols` states.
- [ ] Cover wide cells at both old and new right margins, including orphan width-0 cells.
- [ ] Verify cursor, `YBase`, `YDisp`, wrapped flags, styles and trimmed content against xterm.js.
- [ ] Convert every discovered mismatch into a stable regression test.
- [ ] Extend the differential runner so the scenarios can be repeated during future baseline upgrades.
- [ ] Run the complete verification matrix with no regression.

Acceptance result: _Pending._

### UPG-004: Marker, hyperlink and decoration tracking through reflow

Current state: basic marker trimming and reflow cases are implemented, but complex wrapped-line
mapping and the lifetime of metadata anchored to those markers are not yet considered complete.

- [ ] Differentially test markers on each physical row of a wrapped logical line during shrink/grow.
- [ ] Test markers during scrollback trimming, line insert/delete and cursor-line reflow suppression.
- [ ] Verify marker disposal when a reflowed row is discarded.
- [ ] Verify OSC 8 entries remain alive while any referenced cell remains in the buffer.
- [ ] Verify decorations and search results remain attached to the intended logical text.
- [ ] Ensure marker disposal and metadata cleanup cannot be interrupted by subscriber exceptions.
- [ ] Add regression tests for every corrected mapping or lifetime issue.
- [ ] Run the complete verification matrix with no regression.

Acceptance result: _Pending._

### UPG-005: Public OSC 8 metadata and interaction

Current state: OSC 8 URI and optional ID data are stored by the internal `OscLinkService`, while
public cell snapshots expose only an integer `HyperlinkId`. Consumers cannot resolve the URI from
the public API, and `TerminalView` cannot activate these links.

- [ ] Add an immutable public hyperlink metadata model without exposing internal mutable services.
- [ ] Provide snapshot-scoped metadata or a safe resolver that cannot race buffer mutation.
- [ ] Preserve URI, explicit OSC 8 ID and all supported parameters required by the chosen public API.
- [ ] Add a built-in OSC 8 link provider that maps contiguous and wrapped cell ranges.
- [ ] Support hover, leave and activation in `TerminalView` with the same stale-query cancellation
      rules as registered link providers.
- [ ] Define URI activation security and application override behavior.
- [ ] Test trimming, buffer switching, resize/reflow, duplicate IDs and anonymous links.
- [ ] Add headless and Avalonia integration tests.
- [ ] Run the complete verification matrix with no regression.

Acceptance result: _Pending._

### UPG-006: Remaining public headless option plumbing

Initial target options: `reflowCursorLine`, `windowsPty`, `disableStdin` and
`vtExtensions.kittySgrBoldFaintControl`.

- [ ] Add typed public construction and runtime-update options where upstream permits mutation.
- [ ] Pass `reflowCursorLine` and Windows PTY information into `BufferResizeOptions` from the
      production `TerminalEngine.Resize` path.
- [ ] Apply `disableStdin` consistently to keyboard, paste, focus and mouse application input without
      blocking terminal output writes.
- [ ] Gate SGR 221/222 behavior with `kittySgrBoldFaintControl`.
- [ ] Match upstream defaults, or document and test every intentional default difference.
- [ ] Raise one committed options-changed event with the correct revision for each update.
- [ ] Add public API, engine integration and runtime-update tests.
- [ ] Update README option documentation.
- [ ] Run the complete verification matrix with no regression.

Acceptance result: _Pending._

## P1: desktop and addon compatibility

### UPG-007: Renderer color and minimum-contrast behavior

Current state: `MinimumContrastRatio` is present in rendering configuration but has no rendering
consumer. The upstream inventory excludes 28 `Color.test.ts` cases.

- [ ] Implement relative luminance, contrast ratio and alpha blending using backend-neutral colors.
- [ ] Apply minimum-contrast correction to effective foreground colors during scene compilation.
- [ ] Preserve inverse, dim, bold-bright and selection/decorated color ordering.
- [ ] Cache corrected colors and invalidate the cache after theme or option changes.
- [ ] Port semantic equivalents of the 28 color cases; browser CSS parsing helpers need not be copied
      literally when they do not apply to the C# API.
- [ ] Add renderer tests for transparent backgrounds and both light and dark themes.
- [ ] Run the complete verification matrix with no regression.

Acceptance result: _Pending._

### UPG-008: Decoration lifecycle and overview-ruler parity

Current state: immutable single-line decorations are available through providers. Upstream also has
marker-bound decoration instances with width, height, disposal and indexed lookup behavior. The
Avalonia control has no overview-ruler surface.

- [ ] Define marker-bound decoration registration and disposal semantics.
- [ ] Support x offset, width, height, top/bottom layers and multi-line coverage.
- [ ] Preserve correct indexing after trim, line insertion and marker disposal.
- [ ] Define and implement mutable decoration update notifications.
- [ ] Add an Avalonia overview-ruler surface or explicitly document a backend contract for it.
- [ ] Port semantic equivalents of the 10 excluded `DecorationService` cases.
- [ ] Verify search and link decoration behavior remains compatible.
- [ ] Run the complete verification matrix with no regression.

Acceptance result: _Pending._

### UPG-009: Avalonia accessibility and screen-reader support

- [ ] Implement an Avalonia automation peer appropriate for terminal text and caret navigation.
- [ ] Expose focused text, selection, caret position and terminal title through accessibility APIs.
- [ ] Announce new output without blocking the terminal processor or render worker.
- [ ] Add an output-rate limit and a localized "too much output" announcement.
- [ ] Preserve keyboard, IME, selection and clipboard behavior when accessibility is active.
- [ ] Add automated accessibility-state tests and a manual NVDA/VoiceOver verification checklist.
- [ ] Document supported screen readers and known platform limitations.
- [ ] Run the complete verification matrix with no regression.

Acceptance result: _Pending._

### UPG-010: Progress addon

- [ ] Add an optional `XtermSharp.Addons.Progress` package.
- [ ] Parse ConEmu OSC 9;4 progress sequences through the public production parser.
- [ ] Expose state 0-4, normalized percentage values and a change event.
- [ ] Support programmatic read/reset/restore of the current state.
- [ ] Port the upstream addon tests and invalid-input cases.
- [ ] Add a minimal demo or documentation example.
- [ ] Run the complete verification matrix with no regression.

Acceptance result: _Pending._

### UPG-011: Clipboard/OSC 52 addon

- [ ] Add an optional clipboard addon with a platform-neutral provider interface.
- [ ] Implement OSC 52 query and set behavior with UTF-8-safe Base64 encoding and decoding.
- [ ] Make clipboard reads and writes explicitly configurable; default to the safer policy.
- [ ] Enforce payload limits and reject invalid Base64 without corrupting parser state.
- [ ] Provide an Avalonia clipboard provider without introducing UI dependencies into core.
- [ ] Port the upstream addon tests and add security-policy tests.
- [ ] Document the security implications for local and remote sessions.
- [ ] Run the complete verification matrix with no regression.

Acceptance result: _Pending._

### UPG-012: Serialize addon

- [ ] Add an optional `XtermSharp.Addons.Serialize` package.
- [ ] Serialize normal-buffer content, selected scrollback and explicit marker/range boundaries to ANSI.
- [ ] Restore cursor, styles, terminal modes and alternate-buffer state where requested.
- [ ] Support exclusion of modes and the alternate buffer.
- [ ] Implement HTML serialization for the active buffer, explicit ranges and selection.
- [ ] Preserve wide, combined, protected, underline-color and hyperlink cell data.
- [ ] Port the upstream addon tests and add round-trip differential scenarios.
- [ ] Document size compatibility expectations for restoration.
- [ ] Run the complete verification matrix with no regression.

Acceptance result: _Pending._

## P2: advanced rendering and interaction

### UPG-013: Inline image addon

- [ ] Define backend-neutral image storage, placement and display-list commands.
- [ ] Implement SIXEL parsing with palette, size and memory limits.
- [ ] Implement iTerm2 inline image protocol support.
- [ ] Select and document the supported subset of Kitty terminal graphics.
- [ ] Preserve image placement through scrolling, clearing, buffer switching and resize.
- [ ] Implement Skia rendering and retained-row invalidation for images.
- [ ] Add malformed-payload, cancellation, eviction and memory-pressure tests.
- [ ] Port applicable upstream image addon tests and fixtures.
- [ ] Run the complete verification matrix with no regression.

Acceptance result: _Pending._

### UPG-014: Ligatures and character joiners

- [ ] Define public registration and deregistration for character joiners.
- [ ] Shape eligible ASCII programming-ligature runs instead of always using unshaped `DrawText`.
- [ ] Preserve terminal cell geometry, clipping, selection and cursor placement across joined glyphs.
- [ ] Define fallback behavior when the selected font has no ligatures.
- [ ] Add common programming-ligature and custom joiner tests.
- [ ] Compare behavior with the applicable upstream addon cases.
- [ ] Run the complete verification matrix with no regression.

Acceptance result: _Pending._

### UPG-015: Remaining interactive terminal API and option parity

Candidate surface includes `showCursorImmediately`, font weight, word separators, smooth scrolling,
scroll sensitivity, custom key/wheel handlers, and complete selection text/position APIs.

- [ ] Create an explicit upstream public API comparison and classify every difference as implemented,
      architecture-equivalent, deferred or intentionally unsupported.
- [ ] Implement the options and hooks that apply to Avalonia/Skia.
- [ ] Keep browser DOM objects and CSS-specific types out of public C# contracts.
- [ ] Add selection text, selection position and line-selection APIs where functionality already exists
      only inside `TerminalView`.
- [ ] Add semantic tests for the three excluded cursor-initialization and two font-weight cases.
- [ ] Record every intentional public API difference in this document and `implementation-status.md`.
- [ ] Run the complete verification matrix with no regression.

Acceptance result: _Pending._

## Upstream addon disposition

| Upstream addon | Current disposition | Planned action |
| --- | --- | --- |
| `addon-search` | Ported | Keep synchronized with baseline upgrades. |
| `addon-web-links` | Ported | Keep synchronized with baseline upgrades. |
| `addon-unicode11` | Exact generated core provider | Keep synchronized with the pinned source under UPG-001. |
| `addon-unicode-graphemes` | Exact generated core providers | Keep synchronized with the pinned width source and Unicode 15.0 data under UPG-002. |
| `addon-fit` | Architecture-equivalent | Keep `TerminalView` bounds-to-cell auto-resize behavior. |
| `addon-clipboard` | Local UI clipboard only | Implement OSC 52 behavior under UPG-011. |
| `addon-ligatures` | Partial HarfBuzz shaping | Complete under UPG-014. |
| `addon-progress` | Missing | Implement under UPG-010. |
| `addon-serialize` | Missing | Implement under UPG-012. |
| `addon-image` | Missing | Implement under UPG-013. |
| `addon-attach` | Intentionally outside core | Keep transport wiring application-owned. |
| `addon-web-fonts` | Browser-specific | No direct port planned. |
| `addon-webgl` | Browser-specific | No direct port planned; Skia remains the native backend. |

## Explicitly not acceptance blockers

- Literal ports of upstream `MultiKeyMap` and `SortedList` are not required. Their 11 excluded tests
  exercise JavaScript implementation details; equivalent C# collections should be tested through
  their consumers.
- DOM, canvas and WebGL types must not enter the headless or backend-neutral public packages.
- Built-in PTY, WebSocket, process and SSH transports remain application responsibilities.
- WPF and WinUI controls are platform expansion work, not xterm.js common/headless parity blockers.
- Packed-cell storage and benchmark optimizations are important pre-1.0 engineering work but are not
  themselves missing upstream behavior.

## Acceptance procedure

For each checklist item:

1. Change its overview status to `In progress` when implementation begins.
2. Add or update upstream mappings when the work corresponds to a concrete pinned test.
3. Add differential scenarios for behavior that cannot be represented by a direct unit-test port.
4. Check every implementation and test condition in the relevant section.
5. Record any intentional semantic difference with a concrete explanation.
6. Run the complete verification matrix below.
7. Record the verification date and reviewer in the item's acceptance result.
8. Change its overview status to `Accepted` only after all required checks pass.

```bash
node tools/generate-upstream-tests.mjs --check
node tools/generate-unicode-v11.mjs --check
node tools/generate-unicode-v15.mjs --check
dotnet build XtermSharp.sln --no-restore -m:1
dotnet test --project tests/XtermSharp.Tests/XtermSharp.Tests.csproj --no-build
dotnet test --project tests/XtermSharp.ReferenceTests/XtermSharp.ReferenceTests.csproj --no-build
dotnet test --project tests/XtermSharp.Rendering.Tests/XtermSharp.Rendering.Tests.csproj --no-build
dotnet test --project tests/XtermSharp.Rendering.Skia.Tests/XtermSharp.Rendering.Skia.Tests.csproj --no-build
dotnet test --project tests/XtermSharp.Avalonia.Tests/XtermSharp.Avalonia.Tests.csproj --no-build
dotnet test --project tests/XtermSharp.Addons.WebLinks.Tests/XtermSharp.Addons.WebLinks.Tests.csproj --no-build
dotnet test --project tests/XtermSharp.Addons.Search.Tests/XtermSharp.Addons.Search.Tests.csproj --no-build
dotnet run --project tools/XtermSharp.TestMap/XtermSharp.TestMap.csproj --no-build -- --check
node tools/compare-reference.mjs tools/sample-request.json
node tools/compare-fixtures.mjs
```

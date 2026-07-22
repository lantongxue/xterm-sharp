# XtermSharp agent guide

## Project snapshot

- XtermSharp is an experimental headless terminal emulator written in pure C# for .NET 10.
- `XtermSharp.Addons.WebLinks`, `XtermSharp.Addons.Search`, `XtermSharp.Addons.Progress` and
  `XtermSharp.Addons.Clipboard` are the optional upstream-compatible web-link detection,
  buffer-search, progress-tracking and OSC 52 clipboard addons.
- Package version: `0.1.0-alpha.1`.
- Compatibility baseline: xterm.js `6.0.0`, commit
  `b1aee19ac6d6f4e4d11e4a10a3731b852956bdb7`.
- `xterm.js/` is the pinned development reference. Do not modify it or change its commit unless the
  task is explicitly an upstream-baseline upgrade.
- `XtermSharp` remains a common/headless package. Optional `XtermSharp.Addons.WebLinks`,
  `XtermSharp.Addons.Search`, `XtermSharp.Addons.Progress`, `XtermSharp.Addons.Clipboard`,
  `XtermSharp.Rendering`, `XtermSharp.Rendering.Skia`, `XtermSharp.Avalonia`,
  `XtermSharp.WinForms`, `XtermSharp.Wpf` and `XtermSharp.WinUI` packages provide
  link detection, buffer search, progress tracking, policy-controlled OSC 52 clipboard access,
  display-list, Skia, Avalonia, Windows Forms, WPF and WinUI integration without adding UI
  dependencies to the core package. Browser, DOM, WebGL, accessibility rendering and built-in
  PTY/SSH transports remain outside the current scope; the SSH sample integrates SSH.NET without
  changing the library boundary.

## Current conformance status

Last fully verified on 2026-07-22. Update this section whenever the pinned baseline or counts change.

| Item | Current result |
| --- | ---: |
| Expanded upstream common/headless cases discovered | 1,361 |
| Front-end renderer cases excluded | 54 |
| Required headless-applicable cases | 1,307 |
| Direct C# ports | 1,306 |
| Architecture-equivalent cases | 1 |
| Pending applicable cases | 0 |
| Upstream escape-sequence fixtures | 76/76 matching |
| Complex reflow differential scenarios | 14/14 matching |
| Marker and metadata differential scenarios | 7/7 matching |
| Main xUnit suite | 1,462/1,462 passing |
| Reference infrastructure suite | 1/1 passing |
| Rendering suites | 43/43 passing |
| Web links addon suite | 12/12 passing |
| Search addon suite | 14/14 passing |
| Progress addon suite | 12/12 passing |
| Clipboard addon suite | 19/19 passing |

`XTJS-0799` is the sole `ArchitectureEquivalent` case. xterm.js parses large writes in
131,072-code-point array chunks; XtermSharp streams each `Rune` without an intermediate parse
array and tests the equivalent bounded, ordered behavior.

The 1,462 main tests consist of 1,307 upstream bindings, 76 escape fixtures, two manifest audit
tests and 77 local production-parser, Unicode, resize/reflow, option-plumbing, marker/link-lifetime,
public OSC 8 and safety regressions. `tests/upstream-port-map.json` contains
1,307 unique bindings and must remain free of duplicate or missing IDs.

## Repository map

- `src/XtermSharp/`: public API and headless terminal implementation, grouped into `Decorations`,
  `Events`, `Input`, `Links`, `Options`, `Parsing`, `Selection`, `Snapshots` and `Unicode`
  contracts. Snapshot-scoped OSC 8 metadata and the built-in link provider live in `Links`.
- `src/XtermSharp.Addons.WebLinks/`: optional `addon-web-links` port with strict URL matching,
  wrapped-line range mapping and configurable activation/hover/leave callbacks.
- `src/XtermSharp.Addons.Search/`: optional `addon-search` port with forward/reverse search,
  wrapped/wide/UTF-16 mapping, result tracking and backend-neutral decorations.
- `src/XtermSharp.Addons.Progress/`: optional `addon-progress` port with strict OSC 9;4 parsing,
  normalized state/value tracking and change notifications.
- `src/XtermSharp.Addons.Clipboard/`: optional `addon-clipboard` port with configurable OSC 52
  permissions, strict UTF-8 Base64 handling and a platform-neutral provider contract.
- `src/XtermSharp/Internal/Engine/TerminalEngine.cs`: VT execution, modes, cursor and buffer
  mutations.
- `src/XtermSharp/Internal/Parser/EscapeSequenceParser.cs`: the single VT500 state machine used by
  both production writes and parser conformance tests.
- `src/XtermSharp/Internal/Parser/ParserRegistry.cs`: thread-safe public `ITerminalParser` facade
  over the production parser core.
- `src/XtermSharp/Internal/Buffers/`: mutable buffer, line, cell, range and reflow implementation.
- `src/XtermSharp/Internal/Engine/TerminalDimensions.cs`: shared effective minimum of two columns
  and one row.
- `src/XtermSharp/Internal/Services/`: charset, hyperlink, mouse, option and buffer services.
- `src/XtermSharp.Rendering/`: backend-neutral configuration, display lists, geometry, themes,
  selection and controller code grouped by responsibility.
- `src/XtermSharp.Rendering.Skia/Backends/`: SkiaSharp/HarfBuzz backend and retained row pictures.
- `src/XtermSharp.Avalonia/`: interactive Avalonia adapter grouped into `Clipboard`, `Controls`,
  `Input` and `Diagnostics`.
- `src/XtermSharp.WinForms/`: interactive Windows Forms adapter grouped into `Clipboard`,
  `Controls` and `Input`.
- `src/XtermSharp.Wpf/`: interactive WPF adapter grouped into `Clipboard`, `Controls` and `Input`.
- `src/XtermSharp.WinUI/`: interactive WinUI 3 adapter grouped into `Clipboard`, `Controls`,
  `Input` and `Themes`.
- `samples/XtermSharp.Avalonia.Demo/`: no-PTY ANSI playback and local input-echo smoke test with
  interactive web-links and search-addon demonstrations.
- `samples/XtermSharp.Avalonia.Demo.SSH/`: real SSH PTY integration sample with configurable
  password/private-key authentication and host-key verification.
- `samples/XtermSharp.WinForms.Demo.SSH/`: Windows Forms SSH PTY integration sample with the same
  authentication, host-key verification and remote-resize behavior.
- `samples/XtermSharp.Wpf.Demo.SSH/`: WPF SSH PTY integration sample with password/private-key
  authentication, host-key verification and remote-resize behavior.
- `samples/XtermSharp.WinUI.Demo.SSH/`: packaged WinUI 3 SSH PTY sample with responsive connection
  controls, password/private-key authentication, host-key verification and remote resize.
- `tests/XtermSharp.Tests/`: xUnit v3 behavior, upstream-port and fixture tests.
- `tests/XtermSharp.Tests/InputHandler/ProductionParserIntegrationTests.cs`: production parser
  wiring, error recovery, payload-limit and identifier regressions.
- `tests/XtermSharp.Tests/Headless/SingleColumnSafetyTests.cs`: public raw/effective one-column
  compatibility and wide-cell safety regressions.
- `tests/XtermSharp.TestSupport/`: upstream attributes, binding discovery and manifest validation.
- `tests/XtermSharp.ReferenceTests/`: reference-test infrastructure checks.
- `tests/XtermSharp.Rendering.Tests/`, `XtermSharp.Rendering.Skia.Tests/`,
  `XtermSharp.Avalonia.Tests/`, `XtermSharp.WinForms.Tests/`, `XtermSharp.Wpf.Tests/` and
  `XtermSharp.WinUI.Tests/`: renderer and platform-adapter verification.
- `tests/XtermSharp.Addons.WebLinks.Tests/`: upstream addon behavior, provider lifecycle and hover
  decoration verification.
- `tests/XtermSharp.Addons.Search.Tests/`: upstream search behavior, regression fixture, result
  tracking, decoration and renderer integration verification.
- `tests/XtermSharp.Addons.Progress.Tests/`: upstream progress behavior, programmatic state and
  handler lifecycle verification.
- `tests/XtermSharp.Addons.Clipboard.Tests/`: upstream clipboard behavior, permission, payload,
  encoding, cancellation and handler lifecycle verification.
- `tests/upstream-port-map.json`: maintained C# binding/status map.
- `tests/upstream-tests.json`: generated expanded upstream inventory; do not hand-edit it.
- `tools/XtermSharp.Conformance/`: JSON snapshot/event runner for differential tests.
- `tools/XtermSharp.TestMap/`: verifies manifest-to-C# binding uniqueness and completeness.
- `tools/generate-upstream-tests.mjs`: regenerates/checks the pinned upstream inventory.
- `tools/generate-unicode-v11.mjs`: regenerates/checks the exact Unicode 11 width data from the
  pinned `addon-unicode11` provider.
- `tools/generate-unicode-v15.mjs`: regenerates/checks the exact Unicode 15 width/grapheme data
  from the pinned addon and official Unicode 15.0 acceptance files.
- `tools/unicode/15.0.0/`: pinned Unicode grapheme-property, emoji-property and break-test data.
- `tools/compare-reference.mjs`: compares one JSON scenario with xterm.js headless.
- `tools/compare-reflow-scenarios.mjs` and `tools/reflow-scenarios.json`: compare complex-cell
  shrink/grow, scrollback, cursor-line and delayed-wrap behavior with xterm.js headless.
- `tools/compare-marker-scenarios.mjs` and `tools/marker-scenarios.json`: compare marker physical-row
  mapping, trimming, line insertion/deletion and OSC 8 metadata lifetime with xterm.js headless.
- `tools/compare-fixtures.mjs`: compares all 76 escape fixtures with xterm.js headless.
- `xterm.js/`: pinned upstream source and oracle build, with its own nested `AGENTS.md`.

## Architecture invariants

- Every `Terminal` owns one ordered asynchronous command queue and one processor task. Parser,
  modes, cursor and buffers are mutated only by that task.
- `WriteAsync` completes after parsing. Cancellation only applies before queue admission; an admitted
  write must finish in stream order.
- Byte and string decoders are streaming. Preserve incomplete UTF-8 sequences and split UTF-16
  surrogate pairs across writes.
- `TerminalEngine` and the public parser facade must share one `EscapeSequenceParser`; do not add a
  second production parser or duplicate its transition logic. Input remains Rune-streamed to
  preserve the `XTJS-0799` architecture-equivalent behavior.
- Parser handlers are newest-first. Returning `false` falls through to older or built-in handlers;
  asynchronous handlers keep the parser at the current sequence until completion. Handler
  exceptions fail the current write, abort active string handlers and reset the parser before the
  next queued command. Any already-executed prefix is committed with its own revision and events,
  but does not raise `WriteParsed`.
- OSC handlers that need to report a response use their short-lived `ITerminalParserContext`.
  Responses are emitted in the triggering write's commit; the context is invalid after the handler
  completes, and queuing a separate terminal command from the active handler would deadlock.
- OSC, DCS and APC string handlers share the upstream 10,000,000-UTF-16-code-unit payload limit.
  Limit breaches and invalid or overflowing OSC identifiers must not invoke user or built-in
  handlers, and parsing must resume normally after the terminator.
- Built-in CSI and ESC dispatch is identifier-exact. A syntactically valid but unsupported prefix or
  intermediate sequence must remain a no-op instead of falling into the plain command handler.
- Events are dispatched after the command commits and use the resulting revision. Subscriber
  exceptions are logged and must not stop other subscribers.
- Snapshots are immutable. Do not expose live mutable buffer objects through the public API.
- Core selection and decoration state is platform-neutral. Addons update `Terminal.Selection` and
  registered decoration providers; renderers consume immutable copies and must preserve bottom
  background, selection and top background ordering.
- Terminal event handlers in rendering adapters must only record invalidation state; snapshot
  acquisition, scene compilation and drawing must never block the terminal processor task.
- Link providers use one-based inclusive active-buffer ranges. Provider resolution and callbacks
  run outside the terminal processor task; platform adapters must cancel stale pointer queries on
  movement, resize, detach or terminal hot-swap.
- Rendering backends consume backend-neutral display lists. Do not expose `SKCanvas` or another
  graphics-library type through `XtermSharp.Rendering` public data structures.
- Skia font-family lists must skip unavailable candidates before selecting the primary grid font.
  Use that resolved family for metrics and normal glyph lookup, then apply per-glyph fallback; a
  missing first family must not silently force proportional metrics when a later monospace family
  is installed.
- `TerminalView` never owns or disposes the externally assigned `Terminal`. Detach and hot-swap
  must cancel pending frame work and unsubscribe without changing the session lifetime.
- `TerminalView` must route Backspace, Delete and other non-text keys through `SendKeyAsync` even
  when the platform supplies a non-empty `KeySymbol`; only committed printable text uses the text
  input/IME path. The no-PTY demo performs local line editing because echoed DEL bytes are terminal
  input, not screen-erasure output.
- `TerminalView` keyboard events must preserve browser `KeyboardEvent` semantics: normalize
  Avalonia `A`/`NumPad1` physical names to `KeyA`/`Numpad1`, distinguish virtual `keyCode` from the
  physical `code`, report repeat/release events for enhanced keyboard modes, and defer macOS Option
  or Windows AltGr text to the committed text/IME path. Clipboard shortcuts use Meta on macOS and
  Control elsewhere, and copy only consumes the shortcut when the selection is non-empty.
- The Windows Forms adapter renders in logical coordinates onto a DPI-scaled software Skia surface.
  Its `KeyPress` path carries committed printable/IME text, while non-text keys and enhanced
  keyboard press/repeat/release events use `SendKeyAsync`; AltGr text must not be double-sent.
- The WPF adapter renders retained Skia rows into a per-monitor-DPI `WriteableBitmap` and exposes
  viewport values as read-only dependency properties. Its preview text path carries committed
  printable/IME text, while non-text keys and enhanced keyboard press/repeat/release events use
  `SendKeyAsync`; unload, DPI change and terminal hot-swap must cancel or reschedule pending work.
- The WinUI adapter rasterizes retained Skia rows into a DPI-scaled BGRA `WriteableBitmap`, exposes
  viewport values through get-only dependency-property wrappers and uses `CoreTextEditContext` for
  committed text and IME preedit. Non-text keys and enhanced keyboard press/repeat/release events
  use `SendKeyAsync`; unload, rasterization-scale change and terminal hot-swap must cancel or
  reschedule pending frame and link work.
- Public terminals have an effective minimum width of two columns. `TerminalOptions.Columns == 1`
  remains visible as the raw requested option, while `Terminal.Columns`, buffers, snapshots and
  resize events report two. Keep zero and negative dimensions invalid. Low-level
  `TerminalBuffer.Resize(1, ...)` remains available for upstream ASCII tests, but width-0/2 cells
  must take the finite truncation path and normalize to a width-1 erase-style blank.
- Delayed wrap needs special care: xterm.js can represent the cursor as `x == cols`; XtermSharp keeps
  the physical `CursorX` at `cols - 1` and uses `WrapPending`. Use `LogicalCursorX` when an operation
  permits the logical right-margin position. In particular, ED/EL must not blanket-cancel pending
  wrap, and CBT must treat the logical `x == cols` state as a no-op.
- Reflow markers follow physical-row insertion/deletion semantics, not logical text offsets. Shrink
  keeps markers on existing rows and inserts new wrapped rows after them; grow disposes markers on
  removed rows. After resize, OSC 8 tracking must restore markers for every surviving linked cell
  so metadata cannot disappear while its numeric link ID remains in the buffer.
- OSC 8 metadata exposed through `TerminalSnapshot.Hyperlinks` must contain only links referenced by
  that snapshot's lines. The built-in provider uses the same cancellable resolution path as other
  providers. Neither core nor `TerminalView` may open terminal-provided URIs automatically;
  applications handle `HyperlinkActivated` and apply their own allowlist policy.
- ANSI newline mode `CSI 20 h/l` changes `TerminalOptions.ConvertEol`; it is not an independent mode
  layered on top of the option. LF, VT and FF all apply the current `ConvertEol` value.
- `TerminalOptions.WindowsPty` enables Windows row-growth compatibility when either backend or build
  number is configured. A non-zero build number permits column reflow only for ConPTY build 21376
  or newer. `DisableStdin` suppresses every outgoing PTY data path, including terminal responses,
  without blocking output parsing. Kitty SGR 221/222 remain enabled by default.

## Upstream test mapping workflow

1. Locate the pinned upstream case in `tests/upstream-tests.json` and preserve its `XTJS-####` ID.
2. Implement the behavior and bind the C# test using `UpstreamFact`, or use a theory row whose first
   value is the upstream ID and whose display name starts with that ID.
3. Update `tests/upstream-port-map.json`. Use `ArchitectureEquivalent` only for a real architectural
   substitution and include a concrete `difference` explanation.
4. Regenerate `tests/upstream-tests.json` with `node tools/generate-upstream-tests.mjs`.
5. Run the map audit and the full verification commands below.

Do not add new exclusions merely because a test is difficult. The only current exclusions are the
54 front-end rendering cases. Any intentional semantic difference must be documented in the port
map and project status documentation.

## Escape fixture oracle notes

All 76 fixtures are checked directly against the pinned xterm.js 6.0.0 headless build. Seven legacy
`.text` files encode older behavior, so `EscapeSequenceFixtureTests` contains pinned viewport
overrides for:

- `t0012-VT`
- `t0013-FF`
- `t0055-EL`
- `t0084-CBT`
- `t0101-NLM`
- `t0103-reverse_wrap`
- `t0504-vim`

Do not change engine behavior to satisfy those stale `.text` files. Compare against the pinned
headless oracle first, then update an override only when the oracle result is confirmed.

## Build and verification

The repository uses nullable reference types, latest C# language features, deterministic builds and
warnings as errors. The main tests use xUnit v3 on Microsoft.Testing.Platform; do not use the legacy
`tests/XtermSharp.Tests/Program.cs` runner because it is excluded from compilation.

Run the complete verification matrix from the repository root:

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
dotnet test --project tests/XtermSharp.WinForms.Tests/XtermSharp.WinForms.Tests.csproj --no-build
dotnet test --project tests/XtermSharp.Wpf.Tests/XtermSharp.Wpf.Tests.csproj --no-build
dotnet test --project tests/XtermSharp.WinUI.Tests/XtermSharp.WinUI.Tests.csproj --no-build
dotnet test --project tests/XtermSharp.Addons.WebLinks.Tests/XtermSharp.Addons.WebLinks.Tests.csproj --no-build
dotnet test --project tests/XtermSharp.Addons.Search.Tests/XtermSharp.Addons.Search.Tests.csproj --no-build
dotnet test --project tests/XtermSharp.Addons.Progress.Tests/XtermSharp.Addons.Progress.Tests.csproj --no-build
dotnet test --project tests/XtermSharp.Addons.Clipboard.Tests/XtermSharp.Addons.Clipboard.Tests.csproj --no-build
dotnet run --project tools/XtermSharp.TestMap/XtermSharp.TestMap.csproj --no-build -- --check
node tools/compare-reference.mjs tools/sample-request.json
node tools/compare-reflow-scenarios.mjs
node tools/compare-marker-scenarios.mjs
node tools/compare-fixtures.mjs
```

Expected final signals are zero build warnings/errors, 1,462 main tests passing, 43 rendering
tests passing, 12 web-links addon tests passing, 14 search addon tests passing, 12 progress addon
tests passing, 19 clipboard addon tests passing, one reference test passing, 1,307 verified
bindings, `MATCH`, `MATCH 14/14 complex reflow scenarios`, `MATCH 7/7 marker and metadata
scenarios`, and `MATCH 76/76 escape-sequence fixtures`.

The Node-based checks require the pinned upstream build. If it is absent, prepare it with:

```bash
cd xterm.js
npm ci
npm run build
npm run esbuild
npm run esbuild-package-headless-only
```

## Remaining work

- Keep manifests, differential tools and documentation synchronized during future upstream upgrades.
- Expand differential scenarios beyond the current sample and fixture corpus.
- Add parser chunk-boundary fuzzing and benchmark-driven packed-cell storage work.

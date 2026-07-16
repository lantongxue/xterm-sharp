# XtermSharp agent guide

## Project snapshot

- XtermSharp is an experimental headless terminal emulator written in pure C# for .NET 10.
- Package version: `0.1.0-alpha.1`.
- Compatibility baseline: xterm.js `6.0.0`, commit
  `b1aee19ac6d6f4e4d11e4a10a3731b852956bdb7`.
- `xterm.js/` is the pinned development reference. Do not modify it or change its commit unless the
  task is explicitly an upstream-baseline upgrade.
- `XtermSharp` remains a common/headless package. Optional `XtermSharp.Rendering`,
  `XtermSharp.Rendering.Skia` and `XtermSharp.Avalonia` packages provide display-list, Skia and
  Avalonia integration without adding UI dependencies to the core package. Browser, DOM, WebGL,
  accessibility rendering, PTY, SSH, WPF and WinUI integration remain outside the current scope.

## Current conformance status

Last fully verified on 2026-07-17. Update this section whenever the pinned baseline or counts change.

| Item | Current result |
| --- | ---: |
| Expanded upstream common/headless cases discovered | 1,361 |
| Front-end renderer cases excluded | 54 |
| Required headless-applicable cases | 1,307 |
| Direct C# ports | 1,306 |
| Architecture-equivalent cases | 1 |
| Pending applicable cases | 0 |
| Upstream escape-sequence fixtures | 76/76 matching |
| Main xUnit suite | 1,425/1,425 passing |
| Reference infrastructure suite | 1/1 passing |
| Rendering suites | 9/9 passing |

`XTJS-0799` is the sole `ArchitectureEquivalent` case. xterm.js parses large writes in
131,072-code-point array chunks; XtermSharp streams each `Rune` without an intermediate parse
array and tests the equivalent bounded, ordered behavior.

The 1,425 main tests consist of 1,307 upstream bindings, 76 escape fixtures, two manifest audit
tests and 40 local production-parser/safety regressions. `tests/upstream-port-map.json` contains
1,307 unique bindings and must remain free of duplicate or missing IDs.

## Repository map

- `src/XtermSharp/`: public API and headless terminal implementation.
- `src/XtermSharp/Internal/TerminalEngine.cs`: VT execution, modes, cursor and buffer mutations.
- `src/XtermSharp/Internal/Parser/EscapeSequenceParser.cs`: the single VT500 state machine used by
  both production writes and parser conformance tests.
- `src/XtermSharp/Internal/ParserRegistry.cs`: thread-safe public `ITerminalParser` facade over the
  production parser core.
- `src/XtermSharp/Internal/TerminalBuffer.cs` and `BufferLine.cs`: mutable internal buffer model.
- `src/XtermSharp/Internal/TerminalDimensions.cs`: shared effective minimum of two columns and one
  row.
- `src/XtermSharp.Rendering/`: backend-neutral frames, display lists, themes and selection.
- `src/XtermSharp.Rendering.Skia/`: SkiaSharp/HarfBuzz backend and retained row pictures.
- `src/XtermSharp.Avalonia/`: interactive Avalonia `TerminalView` platform adapter.
- `samples/XtermSharp.Avalonia.Demo/`: no-PTY ANSI playback and local input-echo smoke test.
- `tests/XtermSharp.Tests/`: xUnit v3 behavior, upstream-port and fixture tests.
- `tests/XtermSharp.Tests/InputHandler/ProductionParserIntegrationTests.cs`: production parser
  wiring, error recovery, payload-limit and identifier regressions.
- `tests/XtermSharp.Tests/Headless/SingleColumnSafetyTests.cs`: public raw/effective one-column
  compatibility and wide-cell safety regressions.
- `tests/XtermSharp.TestSupport/`: upstream attributes, binding discovery and manifest validation.
- `tests/XtermSharp.ReferenceTests/`: reference-test infrastructure checks.
- `tests/XtermSharp.Rendering.Tests/`, `XtermSharp.Rendering.Skia.Tests/` and
  `XtermSharp.Avalonia.Tests/`: renderer and platform-adapter verification.
- `tests/upstream-port-map.json`: maintained C# binding/status map.
- `tests/upstream-tests.json`: generated expanded upstream inventory; do not hand-edit it.
- `tools/XtermSharp.Conformance/`: JSON snapshot/event runner for differential tests.
- `tools/XtermSharp.TestMap/`: verifies manifest-to-C# binding uniqueness and completeness.
- `tools/generate-upstream-tests.mjs`: regenerates/checks the pinned upstream inventory.
- `tools/compare-reference.mjs`: compares one JSON scenario with xterm.js headless.
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
- OSC, DCS and APC string handlers share the upstream 10,000,000-UTF-16-code-unit payload limit.
  Limit breaches and invalid or overflowing OSC identifiers must not invoke user or built-in
  handlers, and parsing must resume normally after the terminator.
- Built-in CSI and ESC dispatch is identifier-exact. A syntactically valid but unsupported prefix or
  intermediate sequence must remain a no-op instead of falling into the plain command handler.
- Events are dispatched after the command commits and use the resulting revision. Subscriber
  exceptions are logged and must not stop other subscribers.
- Snapshots are immutable. Do not expose live mutable buffer objects through the public API.
- Terminal event handlers in rendering adapters must only record invalidation state; snapshot
  acquisition, scene compilation and drawing must never block the terminal processor task.
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
- Public terminals have an effective minimum width of two columns. `TerminalOptions.Columns == 1`
  remains visible as the raw requested option, while `Terminal.Columns`, buffers, snapshots and
  resize events report two. Keep zero and negative dimensions invalid. Low-level
  `TerminalBuffer.Resize(1, ...)` remains available for upstream ASCII tests, but width-0/2 cells
  must take the finite truncation path and normalize to a width-1 erase-style blank.
- Delayed wrap needs special care: xterm.js can represent the cursor as `x == cols`; XtermSharp keeps
  the physical `CursorX` at `cols - 1` and uses `WrapPending`. Use `LogicalCursorX` when an operation
  permits the logical right-margin position. In particular, ED/EL must not blanket-cancel pending
  wrap, and CBT must treat the logical `x == cols` state as a no-op.
- ANSI newline mode `CSI 20 h/l` changes `TerminalOptions.ConvertEol`; it is not an independent mode
  layered on top of the option. LF, VT and FF all apply the current `ConvertEol` value.

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
dotnet build XtermSharp.sln --no-restore -m:1
dotnet test --project tests/XtermSharp.Tests/XtermSharp.Tests.csproj --no-build
dotnet test --project tests/XtermSharp.ReferenceTests/XtermSharp.ReferenceTests.csproj --no-build
dotnet test --project tests/XtermSharp.Rendering.Tests/XtermSharp.Rendering.Tests.csproj --no-build
dotnet test --project tests/XtermSharp.Rendering.Skia.Tests/XtermSharp.Rendering.Skia.Tests.csproj --no-build
dotnet test --project tests/XtermSharp.Avalonia.Tests/XtermSharp.Avalonia.Tests.csproj --no-build
dotnet run --project tools/XtermSharp.TestMap/XtermSharp.TestMap.csproj --no-build -- --check
node tools/compare-reference.mjs tools/sample-request.json
node tools/compare-fixtures.mjs
```

Expected final signals are zero build warnings/errors, 1,425 main tests passing, nine rendering
tests passing, one reference test passing, 1,307 verified bindings, `MATCH`, and `MATCH 76/76
escape-sequence fixtures`.

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
- Continue hardening resize/reflow behavior for wide, combined and styled-cell edge cases.
- Complete Unicode 11 tables and full extended grapheme clustering.
- Improve marker behavior through scroll/reflow and richer hyperlink metadata APIs.
- Add parser chunk-boundary fuzzing and benchmark-driven packed-cell storage work.

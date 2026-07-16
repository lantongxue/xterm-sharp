# XtermSharp

XtermSharp is an experimental pure C# headless terminal emulator for .NET 10,
aligned with the common/headless behavior of xterm.js 6.0.0. It parses terminal
output, maintains normal and alternate screen buffers, and exposes immutable
snapshots for server-side processing, testing, recording, or renderer adapters.

> Current version: `0.1.0-alpha.1`. All 1,307 headless-applicable cases in the
> pinned upstream inventory are covered by C# tests. XtermSharp remains a
> pre-release package and does not include a PTY or renderer. See the detailed
> [implementation status](docs/implementation-status.md).

## Highlights

- Ordered asynchronous string and byte writes with bounded pending-input
  backpressure and streaming UTF-8/UTF-16 decoding across write boundaries.
- A single production VT500 parser shared by terminal writes and the public
  custom-handler API for CSI, ESC, OSC, DCS and APC sequences.
- Normal and alternate buffers, scrollback, delayed wrapping, resize reflow,
  cursor movement, tabs, scroll regions and common erase/edit operations.
- SGR styles with 16/256-color and true-color support, underline styles and
  underline color.
- DEC modes, mouse/key input encoding, focus and bracketed-paste modes,
  synchronized output, OSC titles, OSC 8 hyperlinks and DSR responses.
- Immutable viewport or full-buffer snapshots, terminal events, markers,
  addons and pluggable Unicode providers.

## Usage

```csharp
await using var terminal = new Terminal(new TerminalOptions
{
    Columns = 80,
    Rows = 24,
    Scrollback = 1000
});

terminal.Data += (_, e) => pty.Write(e.Data);

await terminal.WriteAsync("\x1b[32mhello\x1b[0m\r\n");
TerminalSnapshot snapshot = await terminal.GetSnapshotAsync();

foreach (TerminalLineSnapshot line in snapshot.ActiveBuffer.Lines)
{
    Console.WriteLine(line.TranslateToString(trimRight: true));
}
```

All state-changing operations use one ordered asynchronous command queue.
`WriteAsync` completes after its input has been parsed. Cancellation applies
only while waiting for queue capacity; an admitted write always finishes in
order so the terminal byte stream cannot be corrupted.

## Conformance

The compatibility baseline is xterm.js 6.0.0 at commit
`b1aee19ac6d6f4e4d11e4a10a3731b852956bdb7`. The expanded upstream inventory
contains 1,361 common/headless cases: 54 front-end renderer cases are outside
the package scope, and all 1,307 applicable cases have verified C# bindings.
Of those bindings, 1,306 are direct ports and `XTJS-0799` is a documented
architecture-equivalent streaming test.

The current verification results are:

- 1,422/1,422 main xUnit tests passing, including all 1,307 upstream bindings,
  all 76 escape-sequence fixtures, two manifest audits and 37 local parser and
  safety regressions.
- 1/1 reference infrastructure test passing.
- 1,307 unique manifest bindings with no pending applicable cases.
- All 76 escape-sequence fixtures matching the pinned xterm.js headless oracle.

## Build and verification

The test projects use xUnit v3 on Microsoft.Testing.Platform. From the
repository root, run the complete verification matrix:

```bash
node tools/generate-upstream-tests.mjs --check
dotnet build XtermSharp.sln --no-restore -m:1
dotnet test --project tests/XtermSharp.Tests/XtermSharp.Tests.csproj --no-build
dotnet test --project tests/XtermSharp.ReferenceTests/XtermSharp.ReferenceTests.csproj --no-build
dotnet run --project tools/XtermSharp.TestMap/XtermSharp.TestMap.csproj --no-build -- --check
node tools/compare-reference.mjs tools/sample-request.json
node tools/compare-fixtures.mjs
```

The Node-based differential checks require the pinned xterm.js build. If it is
not already available, prepare it once with:

```bash
cd xterm.js
npm ci
npm run build
npm run esbuild
npm run esbuild-package-headless-only
```

Benchmarks can be run separately with:

```bash
dotnet run --project benchmarks/XtermSharp.Benchmarks/XtermSharp.Benchmarks.csproj -c Release
```

## Scope and limitations

The core package does not start processes and does not implement PTY, SSH,
browser, DOM, canvas, WebGL, accessibility rendering, Avalonia, WPF or WinUI
integration. Those belong in separate adapter or renderer packages.

Before a stable 1.0 release, the project still needs broader differential and
parser fuzz coverage, additional resize/reflow hardening for complex cells,
complete Unicode 11 and extended grapheme-cluster data, stronger marker
tracking through scroll/reflow, richer hyperlink metadata and further
storage/performance work.

XtermSharp is licensed under MIT. See [NOTICE.md](NOTICE.md) for the upstream
baseline and attribution.

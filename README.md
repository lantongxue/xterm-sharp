# XtermSharp

XtermSharp is an experimental pure C# terminal emulator for .NET 10, aligned
with the common/headless behavior of xterm.js 6.0.0. The core package parses
terminal output and exposes immutable snapshots; optional rendering packages
provide a backend-neutral display list, a SkiaSharp/HarfBuzz backend and an
interactive Avalonia control, while optional addons provide web-link detection, buffer search,
ConEmu progress tracking and policy-controlled OSC 52 clipboard access.

> Current version: `0.1.0-alpha.1`. All 1,307 headless-applicable cases in the
> pinned upstream inventory are covered by C# tests. XtermSharp remains a
> pre-release package and does not include a PTY. See the detailed
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
  snapshot-scoped OSC 8 metadata, addons, ordered link providers and pluggable Unicode providers.
- An optional `XtermSharp.Addons.WebLinks` package matching the pinned
  `addon-web-links` URL detection and wrapped-cell mapping behavior.
- An optional `XtermSharp.Addons.Search` package matching the pinned `addon-search` forward/reverse,
  regex, incremental, wrapped-cell and highlighted-result behavior.
- An optional `XtermSharp.Addons.Progress` package matching the pinned `addon-progress` OSC 9;4
  parsing, state preservation, percentage normalization and change-notification behavior.
- An optional `XtermSharp.Addons.Clipboard` package matching the pinned `addon-clipboard` OSC 52
  read/write protocol with explicit permissions, payload limits and a platform-neutral provider.
- Backend-neutral terminal display lists with damage tracking and immutable
  frame publication.
- SkiaSharp/HarfBuzz rendering and an Avalonia `TerminalView` with selection,
  clipboard, keyboard, mouse, focus, scrolling and IME preedit support.

## Usage

```csharp
using XtermSharp;
using XtermSharp.Options;
using XtermSharp.Snapshots;

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

### Headless compatibility options

`TerminalOptions` and `TerminalOptionsUpdate` expose the remaining mutable xterm.js headless
compatibility controls:

- `ReflowCursorLine` includes the logical line containing the cursor during column reflow. It is
  disabled by default.
- `WindowsPty` identifies a `ConPty` or `WinPty` backend and optional Windows build number. Any
  configured field enables Windows row-growth behavior. A non-zero build number gates column
  reflow to ConPTY build 21376 or newer, matching xterm.js.
- `DisableStdin` suppresses keyboard, paste, focus, mouse and terminal-response data sent toward a
  PTY while continuing to accept and render terminal output through `WriteAsync`.
- `VtExtensions.KittySgrBoldFaintControl` controls Kitty SGR 221/222. It is enabled by default so
  bold and faint can be cleared independently.

Runtime changes are serialized on the terminal command queue and raise one `OptionsChanged` event
with the committed revision.

### Avalonia control

Reference `XtermSharp.Avalonia` and bind an externally owned terminal instance:

```csharp
using XtermSharp;
using XtermSharp.Avalonia.Controls;
using XtermSharp.Options;

var terminal = new Terminal(new TerminalOptions { Columns = 80, Rows = 24 });
var view = new TerminalView { Terminal = terminal };

terminal.Data += (_, e) => pty.Write(e.Data);
await terminal.WriteAsync("\x1b[32mhello from Skia\x1b[0m\r\n");
```

The control subscribes to the terminal but never disposes it. Applications
continue to own PTY/session wiring and the terminal lifetime.

### OSC 8 hyperlinks

OSC 8 URI, explicit ID and parameter metadata is available from the immutable
`TerminalSnapshot.Hyperlinks` map. The core also supplies an automatic OSC 8 link provider, so
`TerminalView` supports hover, leave and click interaction without an addon.

For security, OSC 8 activation never opens an external application by default. Subscribe to
`Terminal.HyperlinkActivated`, validate the terminal-provided URI against an application allowlist
and perform navigation explicitly. See [the OSC 8 guide](docs/osc-hyperlinks.md).

### Web links addon

Reference `XtermSharp.Addons.WebLinks` and load the addon before displaying the
terminal. `TerminalView` automatically handles link hover decoration and click
activation; the default handler opens links through the operating-system shell.

```csharp
using XtermSharp.Addons.WebLinks;

terminal.LoadAddon(new WebLinksAddon());
```

Provide a custom activation handler or `WebLinkProviderOptions` to integrate
navigation, hover tooltips, leave notifications or a custom URL regex. Headless
consumers can query registered providers through `GetLinksAsync` and
`GetLinkAtAsync`. See [the addon guide](docs/web-links-addon.md).

### Search addon

Reference `XtermSharp.Addons.Search`, load the addon and call `FindNext` or `FindPrevious` against
the latest committed active buffer. Optional match decorations are consumed automatically by
`XtermSharp.Rendering` and `TerminalView`.

```csharp
using XtermSharp.Addons.Search;
using XtermSharp.Snapshots;

var search = new SearchAddon();
terminal.LoadAddon(search);
search.FindNext("error", new SearchOptions
{
    Regex = false,
    Decorations = new SearchDecorationOptions
    {
        MatchBackground = TerminalColor.Rgb(80, 50, 0),
        ActiveMatchBackground = TerminalColor.Rgb(0, 80, 140)
    }
});
```

The addon supports case-sensitive, whole-word, regex and incremental searches, tracks capped result
counts, and debounces decorated-result recomputation after writes and resizes. See
[the addon guide](docs/search-addon.md).

### Progress addon

Reference `XtermSharp.Addons.Progress` and load the addon to track ConEmu OSC 9;4 progress reports.
The current state can also be reset or restored programmatically.

```csharp
using XtermSharp.Addons.Progress;

var progress = new ProgressAddon();
terminal.LoadAddon(progress);
progress.ProgressChanged += (_, args) =>
    Console.WriteLine($"{args.State}: {args.Value}%");

progress.Progress = new ProgressState(ProgressType.Remove, 0);
```

Values are clamped to 0 through 100. Error and pause sequences with a missing or zero value preserve
the last percentage, while indeterminate sequences preserve it without presenting it as a known
percentage. See [the addon guide](docs/progress-addon.md).

### Clipboard addon

Reference `XtermSharp.Addons.Clipboard`, supply a platform clipboard provider and opt into only the
OSC 52 operations that the session needs. Reads and writes are both denied by default.

```csharp
using XtermSharp.Addons.Clipboard;

var clipboard = new ClipboardAddon(provider, new ClipboardAddonOptions
{
    AllowRead = false,
    AllowWrite = true
});
terminal.LoadAddon(clipboard);
```

`XtermSharp.Avalonia` provides `AvaloniaClipboardProvider` for an Avalonia `IClipboard`. Clipboard
reads can expose host secrets to remote applications, so write-only access is recommended unless
query support is explicitly required. See [the addon and security guide](docs/clipboard-addon.md).

Set `ShowRenderingDebugOverlay` to display rolling FPS and average/maximum/minimum frame intervals.
See the [rendering debug overlay change log](docs/rendering-debug-overlay-2026-07-17.md) for sampling
semantics, SSH demo integration and verification details.

### SSH demo

`XtermSharp.Avalonia.Demo.SSH` is a separate sample application that connects
the Avalonia control to a real SSH pseudo-terminal through SSH.NET. It supports
password and private-key authentication, SHA-256 host-key verification, remote
window-size updates and a configurable terminal type.

```bash
dotnet run --project samples/XtermSharp.Avalonia.Demo.SSH/XtermSharp.Avalonia.Demo.SSH.csproj
```

Connection values can be entered in the window or prefilled with environment
variables. See the [SSH demo README](samples/XtermSharp.Avalonia.Demo.SSH/README.md)
for the complete list and host-key verification workflow. The SSH dependency is
sample-only; the XtermSharp library packages remain transport-agnostic.

The non-PTY `XtermSharp.Avalonia.Demo` loads both `WebLinksAddon` and `SearchAddon`. Its toolbar
demonstrates regex, case-sensitive and whole-word buffer searches with highlighted results, while
the terminal content includes clickable web links.

## Conformance

The compatibility baseline is xterm.js 6.0.0 at commit
`b1aee19ac6d6f4e4d11e4a10a3731b852956bdb7`. The expanded upstream inventory
contains 1,361 common/headless cases: 54 front-end renderer cases remain outside
the headless conformance inventory, and all 1,307 applicable cases have verified C# bindings.
Of those bindings, 1,306 are direct ports and `XTJS-0799` is a documented
architecture-equivalent streaming test.

The current verification results are:

- 1,462/1,462 main xUnit tests passing, including all 1,307 upstream bindings,
  all 76 escape-sequence fixtures, two manifest audits and 77 local parser, Unicode,
  resize/reflow, option-plumbing, marker/link-lifetime, public OSC 8 and safety regressions.
- Twenty-four rendering tests passing across the backend-neutral, Skia and Avalonia suites.
- Twelve `addon-web-links` compatibility and integration tests passing.
- Fourteen `addon-search` compatibility, regression and rendering-integration tests passing.
- Twelve `addon-progress` compatibility, programmatic-state and lifecycle tests passing.
- Nineteen `addon-clipboard` compatibility, security-policy and lifecycle tests passing.
- 1/1 reference infrastructure test passing.
- 1,307 unique manifest bindings with no pending applicable cases.
- All 14 complex-cell resize/reflow scenarios matching the pinned xterm.js headless oracle.
- All seven marker, trimming and metadata-lifetime scenarios matching the pinned xterm.js oracle.
- All 76 escape-sequence fixtures matching the pinned xterm.js headless oracle.

## Build and verification

The test projects use xUnit v3 on Microsoft.Testing.Platform. From the
repository root, run the complete verification matrix:

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
dotnet test --project tests/XtermSharp.Addons.Progress.Tests/XtermSharp.Addons.Progress.Tests.csproj --no-build
dotnet test --project tests/XtermSharp.Addons.Clipboard.Tests/XtermSharp.Addons.Clipboard.Tests.csproj --no-build
dotnet run --project tools/XtermSharp.TestMap/XtermSharp.TestMap.csproj --no-build -- --check
node tools/compare-reference.mjs tools/sample-request.json
node tools/compare-reflow-scenarios.mjs
node tools/compare-marker-scenarios.mjs
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

See [the 2026-07-17 rendering performance log](docs/rendering-performance-2026-07-17.md) for the
Avalonia/Skia optimization design, benchmark workload and measured results.

## Code organization

Source files follow a one-top-level-type-per-file convention and are grouped by responsibility.
See the [code organization guide](docs/code-organization.md) for the core, rendering, platform,
sample and test-support directory layout.

## Scope and limitations

The core package does not start processes and does not implement PTY or SSH
transport, browser, DOM, canvas, WebGL or accessibility rendering. The SSH demo
shows application-owned transport wiring through SSH.NET. Avalonia and Skia
live in separate optional packages; WPF, WinUI, GDI and Direct2D backends remain
out of scope for the current release.

Before a stable 1.0 release, the project still needs broader differential and
parser fuzz coverage and further storage/performance work.

XtermSharp is licensed under MIT. See [NOTICE.md](NOTICE.md) for the upstream
baseline and attribution.

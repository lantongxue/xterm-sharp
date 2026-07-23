# XtermSharp

XtermSharp is an experimental pure C# terminal emulator for .NET 10, aligned
with the common/headless behavior of xterm.js 6.0.0. The core package parses
terminal output and exposes immutable snapshots; optional rendering packages
provide a backend-neutral display list, a SkiaSharp/HarfBuzz backend and interactive Avalonia,
.NET MAUI, Windows Forms, WPF and WinUI controls, while optional addons provide web-link detection,
buffer search,
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
- SkiaSharp/HarfBuzz rendering and GPU-aware Avalonia/Windows Forms/WPF/WinUI `TerminalView`
  controls with software fallbacks plus selection, clipboard, keyboard, mouse, focus, scrolling
  and text-input support.
- SkiaSharp/HarfBuzz rendering in a MAUI `TerminalView` with touch selection, link activation,
  soft-keyboard input, clipboard, focus and scrolling support.

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
continue to own PTY/session wiring and the terminal lifetime. It automatically renders retained
Skia pictures on Avalonia's GPU-backed surface when one is available and falls back to software
Skia otherwise. `ActiveRenderMode` and `IsGpuAccelerated` expose the mode used by the most recently
presented frame. `RequestedRenderMode` can switch live between `Auto`, `Software` and `Gpu`; a GPU
request still depends on the Avalonia application host supplying a GPU-backed Skia lease.

### .NET MAUI control

Reference `XtermSharp.Maui` and assign an application-owned terminal:

```csharp
using Microsoft.Maui.Hosting;
using XtermSharp;
using XtermSharp.Maui.Controls;
using XtermSharp.Maui.Hosting;
using XtermSharp.Options;

MauiApp app = MauiApp.CreateBuilder()
    .UseMauiApp<App>()
    .UseXtermSharpMaui()
    .Build();

var terminal = new Terminal(new TerminalOptions { Columns = 80, Rows = 24 });
var view = new TerminalView { Terminal = terminal };

terminal.Data += (_, e) => pty.Write(e.Data);
await terminal.WriteAsync("\x1b[32mhello from MAUI Skia\x1b[0m\r\n");
```

The MAUI control reuses `XtermSharp.Rendering.Skia` for font measurement, HarfBuzz shaping and
retained row pictures, then presents them through GPU-backed `SKGLView` with `SKCanvasView`
fallback. `UseXtermSharpMaui()` registers the required SkiaSharp MAUI handler. The view handles device-pixel scaling, resize, soft-keyboard
input, touch selection, mouse-protocol forwarding, link activation and clipboard operations while
leaving terminal and transport ownership with the application. Changing `RequestedRenderMode`
switches between the `SKGLView` and `SKCanvasView` without recreating the terminal session.

### Windows Forms control

Reference `XtermSharp.WinForms` from a `net10.0-windows` application and assign an externally owned
terminal:

```csharp
using XtermSharp;
using XtermSharp.Options;
using XtermSharp.WinForms.Controls;

var terminal = new Terminal(new TerminalOptions { Columns = 80, Rows = 24 });
var view = new TerminalView { Dock = DockStyle.Fill, Terminal = terminal };

terminal.Data += (_, e) => pty.Write(e.Data);
await terminal.WriteAsync("\x1b[32mhello from WinForms\x1b[0m\r\n");
```

The control uses the same retained Skia rows and worker-side frame preparation as the Avalonia
adapter. Set `RequestedRenderMode` to `Gpu` to present through the modern OpenTK GPU surface, or use
the compatibility alias `EnableGpuRendering = true`; when it is disabled or unavailable, the
control uses its DPI-aware software surface. It supports committed text/IME
input, browser-compatible key coordinates, enhanced keyboard releases/repeats, terminal mouse
protocols, local selection, clipboard shortcuts and cancellable link interaction. The control never
owns or disposes its assigned `Terminal`.

### WPF control

Reference `XtermSharp.Wpf` from a `net10.0-windows` application and assign an externally owned
terminal. `Terminal`, `TerminalTheme`, `RenderOptions`, `Columns`, `Rows`, `ScrollValue` and
`ScrollMaximum` are dependency properties suitable for XAML binding.

```xml
<Window xmlns:xterm="clr-namespace:XtermSharp.Wpf.Controls;assembly=XtermSharp.Wpf">
  <xterm:TerminalView x:Name="TerminalView" Padding="8" />
</Window>
```

```csharp
var terminal = new Terminal(new TerminalOptions { Columns = 80, Rows = 24 });
TerminalView.Terminal = terminal;
terminal.Data += (_, e) => pty.Write(e.Data);
await terminal.WriteAsync("\x1b[32mhello from WPF\x1b[0m\r\n");
```

The WPF adapter replays retained Skia rows through an OpenTK/WPF GPU surface and falls back to a
DPI-aware `WriteableBitmap` when the context is unavailable. It supports the same selection,
clipboard, committed text/IME, browser-compatible key, enhanced-keyboard, mouse and link interaction
paths as the other desktop controls, supports live `RequestedRenderMode` changes, and never owns or
disposes the assigned terminal.

### WinUI control

Reference `XtermSharp.WinUI` from a WinUI 3 application and assign an externally owned terminal.
The bindable dependency-property surface matches the other Windows adapters.

```xml
<Page xmlns:xterm="using:XtermSharp.WinUI.Controls">
  <xterm:TerminalView x:Name="TerminalView" Padding="8" />
</Page>
```

```csharp
var terminal = new Terminal(new TerminalOptions { Columns = 80, Rows = 24 });
TerminalView.Terminal = terminal;
terminal.Data += (_, e) => pty.Write(e.Data);
await terminal.WriteAsync("\x1b[32mhello from WinUI\x1b[0m\r\n");
```

The WinUI adapter prepares retained Skia rows off the UI thread and presents them through the
ANGLE-backed `SKSwapChainPanel`, with a DPI-scaled `WriteableBitmap` fallback. It uses
`CoreTextEditContext` for committed text and IME preedit.
It supports selection, clipboard, browser-compatible key events, terminal mouse protocols and
cancellable link interaction without owning or disposing the assigned terminal. Changing
`RequestedRenderMode` swaps the swap-chain and bitmap presentation paths at runtime.

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

`XtermSharp.Avalonia` provides `AvaloniaClipboardProvider`; `XtermSharp.Maui` provides
`MauiClipboardProvider`; `XtermSharp.WinForms` provides
`WinFormsClipboardProvider`; `XtermSharp.Wpf` provides `WpfClipboardProvider`; and
`XtermSharp.WinUI` provides `WinUIClipboardProvider`. All dispatch system clipboard access through
their UI thread.
Clipboard reads can expose host secrets to remote applications, so write-only access is recommended
unless query support is explicitly required. See [the addon and security guide](docs/clipboard-addon.md).

Every Avalonia, MAUI, Windows Forms, WPF and WinUI `TerminalView` exposes
`ShowRenderingDebugOverlay` plus active GPU/software render status. Set the overlay to display the
active renderer, rolling FPS and average/maximum/minimum frame intervals through the shared Skia
backend. Each control also exposes `RequestedRenderMode`: `Auto` selects the available platform
surface, `Software` forces CPU rasterization and `Gpu` requests GPU presentation with software
fallback. `ActiveRenderMode` remains the actual mode used by the most recently presented frame.
WinForms defaults to `Software` for compatibility with hosts that cannot create an OpenGL context;
the other adapters default to `Auto`.
All included demos enable the overlay by default and expose a `Rendering`/`Rendering mode`
selector with the same three values. Changing it updates the active terminal surface immediately;
the overlay reports the actual mode after GPU fallback. The WinForms SSH demo starts at `Gpu` to
preserve its existing GPU-enabled behavior, while the other demos start at `Auto`.
See the [rendering debug overlay change log](docs/rendering-debug-overlay-2026-07-17.md) for sampling
semantics, SSH demo integration and verification details.

### SSH demos

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

The Windows Forms sample provides the same SSH PTY and host-key verification workflow:

```bash
dotnet run --project samples/XtermSharp.WinForms.Demo.SSH/XtermSharp.WinForms.Demo.SSH.csproj
```

See the [Windows Forms SSH demo README](samples/XtermSharp.WinForms.Demo.SSH/README.md) for its
connection fields and environment variables.

The WPF sample provides the same SSH PTY, private-key and host-key verification workflow:

```bash
dotnet run --project samples/XtermSharp.Wpf.Demo.SSH/XtermSharp.Wpf.Demo.SSH.csproj
```

See the [WPF SSH demo README](samples/XtermSharp.Wpf.Demo.SSH/README.md) for its connection fields,
security behavior and environment variables.

The packaged WinUI sample uses the same SSH workflow with a responsive Fluent connection surface:

```bash
dotnet run --project samples/XtermSharp.WinUI.Demo.SSH/XtermSharp.WinUI.Demo.SSH.csproj
```

See the [WinUI SSH demo README](samples/XtermSharp.WinUI.Demo.SSH/README.md) for packaging,
connection fields, security behavior and environment variables.

The non-PTY `XtermSharp.Avalonia.Demo` loads both `WebLinksAddon` and `SearchAddon`. Its toolbar
demonstrates regex, case-sensitive and whole-word buffer searches with highlighted results, while
the terminal content includes clickable web links.

`XtermSharp.Maui.Demo.SSH` provides the equivalent Android, iOS and Mac Catalyst application with
SkiaSharp/HarfBuzz rendering. Its Core project compiles the same SSH transport source files used
by the Avalonia demo, so authentication, host-key verification, PTY resize and ordered data pumps
stay identical. See the [MAUI SSH demo README](samples/XtermSharp.Maui.Demo.SSH/README.md).

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
- Forty-three rendering tests passing across the backend-neutral, Skia, Avalonia, Windows Forms,
  WPF and WinUI suites.
- Eight .NET MAUI Skia-integration, input, clipboard and ownership tests passing.
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
dotnet test --project tests/XtermSharp.Maui.Tests/XtermSharp.Maui.Tests.csproj --no-build
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
shows application-owned transport wiring through SSH.NET. Avalonia, MAUI, Windows Forms, WPF and
WinUI are optional adapters over the shared Skia package; GDI and Direct2D backends remain
out of scope for the current release.

Before a stable 1.0 release, the project still needs broader differential and
parser fuzz coverage and further storage/performance work.

XtermSharp is licensed under MIT. See [NOTICE.md](NOTICE.md) for the upstream
baseline and attribution.

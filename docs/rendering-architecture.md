# Rendering architecture

The renderer is optional and keeps the headless core independent from UI and graphics libraries.

```text
Terminal snapshots and revisioned events
                |
                v
XtermSharp.Rendering.Controllers
  TerminalRenderController
XtermSharp.Rendering.Themes / Selection / Display
  theme and selection resolution
  immutable per-row display lists
                |
                v
XtermSharp.Rendering.Skia.Backends
  font metrics and fallback
  HarfBuzz shaping
  retained SKPicture rows
                |
                v
XtermSharp.Avalonia.Controls.TerminalView
  dispatcher, GPU-aware presentation and frame scheduling
  DPI-aware resize
  keyboard, mouse, clipboard and IME
```

## Threading and frames

`Terminal` events are dispatched by its ordered processor task. The rendering controller's event
handlers only merge dirty rows and raise `Invalidated`; they do not request snapshots or draw.
The platform adapter schedules one `PrepareFrameAsync` operation at render priority, moves frame
preparation to the worker pool, obtains an immutable viewport snapshot and atomically publishes a
`TerminalRenderFrame`. Later revisions are coalesced, unchanged `TerminalLineSnapshot` objects
reuse cached display rows, and frames with empty pixel damage do not invalidate the Avalonia
visual. Scroll, column, row and extent properties are direct Avalonia properties whose bindings
are notified only when their values actually change.

## GPU acceleration

`TerminalView` presents its retained Skia pictures directly through Avalonia's current Skia API
lease. When Avalonia supplies a `GRContext`, picture replay, text, fills and decorations execute on
the GPU-backed surface. If the host selected a software renderer, or the graphics device is
temporarily unavailable, the same display list is rendered by Skia in software without changing
terminal behavior or requiring a second graphics context.

GPU selection belongs to the Avalonia application host. Desktop applications using
`UsePlatformDetect()` use Avalonia's platform renderer selection and normally prefer Metal,
Direct3D, Vulkan or OpenGL where supported. `TerminalView.ActiveRenderMode` reports `Unknown`,
`Software` or `Gpu` for the most recently presented frame, and `TerminalView.IsGpuAccelerated`
provides a bindable boolean convenience property. Both reset when the control detaches or changes
terminal sessions.

`TerminalView.ShowRenderingDebugOverlay` enables a top-right retained Skia overlay with the active
GPU/software renderer, rolling presentation FPS and average, maximum and minimum frame intervals.
Sampling resets after a long idle gap so terminal inactivity is not reported as a single slow
frame. The overlay is disabled by default and can be enabled from XAML:

```xml
<Window xmlns:xterm="clr-namespace:XtermSharp.Avalonia.Controls;assembly=XtermSharp.Avalonia">
  <xterm:TerminalView ShowRenderingDebugOverlay="True" />
</Window>
```

Synchronized output mode holds an already published frame for at most one second. Leaving the mode
flushes immediately; the timeout prevents a malformed stream from freezing the visible terminal.

## Backend contract

`ITerminalRenderBackend<TSurface>` is experimental. It measures the configured font and executes a
backend-neutral `TerminalDisplayList` on its native surface. Compatible fixed-width ASCII cells and
adjacent background cells are batched into runs before reaching a backend. Display commands contain
rectangles, text clusters, colors and line styles but no Skia or Avalonia types. Future GDI,
Direct2D or other backends can therefore reuse terminal color, attribute, selection and cursor
semantics.

The Skia backend uses the direct Skia text path for safe printable ASCII runs and HarfBuzz for text
that requires shaping. It selects fallback typefaces per cluster, clips glyphs to their allocated
cells and records each changed display row as an `SKPicture` during worker-side frame preparation.
The Avalonia render thread normally only replays retained pictures. Font metrics, fonts, fill paints
and current row pictures are cached with bounded stale-picture eviction. Theme, font or DPI changes
invalidate the relevant display rows; different backends are expected to preserve terminal
semantics, not pixel-identical rasterization.

## Platform ownership

`TerminalView.Terminal` is externally assigned. The control subscribes, renders and forwards input
but never disposes the terminal. Applications remain responsible for PTY/session wiring. When the
control detaches or receives another terminal, it cancels pending frame work, releases Skia
resources and unsubscribes from the old instance.

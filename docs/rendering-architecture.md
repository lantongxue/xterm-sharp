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
Platform adapters
  XtermSharp.Avalonia.Controls.TerminalView
    dispatcher and GPU-aware presentation
  XtermSharp.WinForms.Controls.TerminalView
    dispatcher and software-surface presentation
  XtermSharp.Wpf.Controls.TerminalView
    dependency properties and WriteableBitmap presentation
  XtermSharp.WinUI.Controls.TerminalView
    dependency properties, CoreText and WriteableBitmap presentation
  shared responsibilities
    frame scheduling, DPI-aware resize, keyboard, mouse, clipboard and IME
```

## Threading and frames

`Terminal` events are dispatched by its ordered processor task. The rendering controller's event
handlers only merge dirty rows and raise `Invalidated`; they do not request snapshots or draw.
The platform adapter schedules one `PrepareFrameAsync` operation at render priority, moves frame
preparation to the worker pool, obtains an immutable viewport snapshot and atomically publishes a
`TerminalRenderFrame`. Later revisions are coalesced, unchanged `TerminalLineSnapshot` objects
reuse cached display rows, and frames with empty pixel damage do not invalidate the platform
visual. The Avalonia adapter raises direct-property changes only when values change; the Windows
Forms adapter publishes one `ViewportChanged` event for scroll, extent or grid-size changes; the
WPF adapter updates read-only dependency properties; and the WinUI adapter publishes get-only
dependency-property wrappers for those viewport values.

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

The Windows Forms adapter deliberately presents through a software Skia surface backed by a
32-bit premultiplied WinForms bitmap. It prepares and caches the same retained `SKPicture` rows on a
worker, scales logical coordinates by the control's per-monitor DPI during paint and reports raw
device pixels to terminal mouse protocols. This keeps the UI package independent from a separate
OpenGL control or graphics-context lifetime while preserving the backend-neutral architecture.

The WPF adapter uses the same software strategy with a per-monitor-DPI `WriteableBitmap`. Its
locked premultiplied BGRA back buffer is exposed directly to an `SKSurface`, then invalidated as one
WPF image without image encoding or an additional graphics package. WPF device-independent pointer
coordinates and padding remain aligned with the controller's logical viewport.

The WinUI adapter also uses a DPI-scaled premultiplied BGRA software surface. It renders retained
Skia rows into a reusable bitmap, copies that contiguous buffer into one WinUI `WriteableBitmap`
and invalidates the image source without encoding. `XamlRoot.RasterizationScale` keeps logical
pointer coordinates, terminal padding and physical pixels aligned across monitor changes.

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
The platform paint path normally only replays retained pictures. Font metrics, fonts, fill paints
and current row pictures are cached with bounded stale-picture eviction. Theme, font or DPI changes
invalidate the relevant display rows; different presenters are expected to preserve terminal
semantics, not pixel-identical rasterization.

## Platform ownership

`TerminalView.Terminal` is externally assigned in all platform packages. Controls subscribe,
render and forward input but never dispose the terminal. Applications remain responsible for
PTY/session wiring. When a control detaches or receives another terminal, it cancels pending frame
work and link queries, releases Skia resources and unsubscribes from the old instance.

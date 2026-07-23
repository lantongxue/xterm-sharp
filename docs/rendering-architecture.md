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
  shared rendering telemetry overlay
                |
                v
Platform adapters
  XtermSharp.Avalonia.Controls.TerminalView
    dispatcher and GPU-aware presentation
  XtermSharp.Maui.Controls.TerminalView
    SKGLView with SKCanvasView fallback, touch and soft keyboard
  XtermSharp.WinForms.Controls.TerminalView
    dispatcher and optional OpenTK GPU surface with software fallback
  XtermSharp.Wpf.Controls.TerminalView
    dependency properties and OpenTK/WPF GPU surface with bitmap fallback
  XtermSharp.WinUI.Controls.TerminalView
    dependency properties, CoreText and SKSwapChainPanel with bitmap fallback
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

The MAUI adapter presents through `SKGLView` and replays the retained pictures on the platform GPU
surface. It keeps the existing `SKCanvasView` as a software fallback and switches to it when the
GPU handler or frame callback reports an error.

The Windows Forms adapter can present through a modern OpenTK `GLControl` when
`EnableGpuRendering` is enabled. This opt-in avoids blocking hosts that cannot create a GLFW
context during control construction. The normal path wraps the current framebuffer in an Skia
`GRBackendRenderTarget`; a 32-bit premultiplied WinForms bitmap remains the fallback.

The WPF adapter uses `OpenTK.GLWpfControl` as a child visual and wraps its current framebuffer in
the same Skia GPU surface. Its per-monitor-DPI `WriteableBitmap` remains available when WPF cannot
create or retain the OpenGL context. WPF device-independent pointer coordinates and padding remain
aligned with the controller's logical viewport.

The WinUI adapter uses SkiaSharp's `SKSwapChainPanel` (ANGLE-backed) and keeps the existing
DPI-scaled premultiplied BGRA `WriteableBitmap` path as a fallback. `XamlRoot.RasterizationScale`
keeps logical pointer coordinates, terminal padding and physical pixels aligned across monitor
changes. All four adapters report the mode of the most recently presented frame through
`ActiveRenderMode` and `IsGpuAccelerated`.

Every platform `TerminalView.ShowRenderingDebugOverlay` enables a top-right Skia overlay with the
active GPU/software renderer, rolling presentation FPS and average, maximum and minimum frame
intervals. Sampling and drawing live in `XtermSharp.Rendering.Skia`, while each adapter only maps
the switch into its native property system and requests a repaint. Sampling resets after a long
idle gap so terminal inactivity is not reported as a single slow frame. The overlay is disabled by
default and can be enabled from XAML, for example in Avalonia:

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
semantics, not pixel-identical rasterization. Its optional debug overlay is drawn after retained
terminal rows and therefore remains outside terminal snapshots, selection and damage state. The
WinUI adapter performs a full bitmap presentation while telemetry is enabled so every updated
overlay pixel reaches its `WriteableBitmap` despite row-based terminal damage tracking.

The MAUI adapter uses the same `SkiaTerminalRenderBackend` and retained row pictures as Avalonia.
It presents them through `SKGLView` first and `SKCanvasView` on fallback, scales logical terminal
coordinates to the platform surface and maps Skia touch coordinates back to MAUI logical units.
Applications call `UseXtermSharpMaui()` while building their `MauiApp` to register the SkiaSharp
view handler.

## Platform ownership

`TerminalView.Terminal` is externally assigned in all platform packages. Controls subscribe,
render and forward input but never dispose the terminal. Applications remain responsible for
PTY/session wiring. When a control detaches or receives another terminal, it cancels pending frame
work and link queries, releases Skia resources and unsubscribes from the old instance.

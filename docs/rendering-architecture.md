# Rendering architecture

The renderer is optional and keeps the headless core independent from UI and graphics libraries.

```text
Terminal snapshots and revisioned events
                |
                v
XtermSharp.Rendering
  TerminalRenderController
  theme and selection resolution
  immutable per-row display lists
                |
                v
XtermSharp.Rendering.Skia
  font metrics and fallback
  HarfBuzz shaping
  retained SKPicture rows
                |
                v
XtermSharp.Avalonia.TerminalView
  dispatcher and frame scheduling
  DPI-aware resize
  keyboard, mouse, clipboard and IME
```

## Threading and frames

`Terminal` events are dispatched by its ordered processor task. The rendering controller's event
handlers only merge dirty rows and raise `Invalidated`; they do not request snapshots or draw.
The platform adapter schedules one `PrepareFrameAsync` operation at render priority, obtains an
immutable viewport snapshot and atomically publishes a `TerminalRenderFrame`. Later revisions are
coalesced, and unchanged `TerminalLineSnapshot` objects reuse cached display rows.

Synchronized output mode holds an already published frame for at most one second. Leaving the mode
flushes immediately; the timeout prevents a malformed stream from freezing the visible terminal.

## Backend contract

`ITerminalRenderBackend<TSurface>` is experimental. It measures the configured font and executes a
backend-neutral `TerminalDisplayList` on its native surface. Display commands contain rectangles,
text clusters, colors and line styles but no Skia or Avalonia types. Future GDI, Direct2D or other
backends can therefore reuse terminal color, attribute, selection and cursor semantics.

The Skia backend shapes text with HarfBuzz, selects fallback typefaces per cluster, clips glyphs to
their allocated cells and records each display row as an `SKPicture`. Theme, font or DPI changes
invalidate the relevant caches; different backends are expected to preserve terminal semantics,
not pixel-identical rasterization.

## Platform ownership

`TerminalView.Terminal` is externally assigned. The control subscribes, renders and forwards input
but never disposes the terminal. Applications remain responsible for PTY/session wiring. When the
control detaches or receives another terminal, it cancels pending frame work, releases Skia
resources and unsubscribes from the old instance.

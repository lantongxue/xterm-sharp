# Rendering debug overlay change log - 2026-07-17, shared backend update 2026-07-23

## Summary

Every platform `TerminalView` has an optional rendering telemetry overlay for diagnosing real
application presentation behavior. The overlay is disabled by default and appears in the
top-right corner of the terminal control when `ShowRenderingDebugOverlay` is enabled. Metrics and
drawing are implemented once by `XtermSharp.Rendering.Skia` and reused by Avalonia, MAUI, Windows
Forms, WPF and WinUI.

It reports four rolling values:

- `FPS`: presentation FPS calculated from the average sampled frame interval;
- `AVG`: average frame interval in milliseconds;
- `MAX`: maximum frame interval in milliseconds;
- `MIN`: minimum frame interval in milliseconds.

The feature is also exposed in the SSH demo through a live checkbox and an environment-variable
default.

## Public API

Each control exposes the same public property through its native property system:

```csharp
public bool ShowRenderingDebugOverlay { get; set; }
```

It can be enabled from XAML, for example in Avalonia:

```xml
<Window xmlns:xterm="clr-namespace:XtermSharp.Avalonia.Controls;assembly=XtermSharp.Avalonia">
  <xterm:TerminalView ShowRenderingDebugOverlay="True" />
</Window>
```

or changed at runtime:

```csharp
terminalView.ShowRenderingDebugOverlay = true;
```

Avalonia uses a styled property, MAUI a bindable property, WPF and WinUI dependency properties, and
Windows Forms a regular designer property. Changing it resets existing samples and invalidates the
visual immediately. Disabling it removes the overlay without changing the terminal, render
controller or externally owned session.

## Sampling behavior

Sampling happens when the shared Skia backend actually renders onto a platform surface. This means
the values describe component presentation intervals rather than terminal parser throughput or
only the CPU duration of `SKCanvas` calls.

The collector uses:

- a rolling window of up to 120 frame intervals;
- monotonic `Stopwatch` timestamps;
- a lock around sampling and UI-thread resets;
- a two-second idle threshold.

If the gap between draws exceeds two seconds, previous samples are discarded. The first frame after
startup, enabling the overlay or an idle reset displays placeholders until a second frame provides
an interval. This prevents an inactive terminal from reporting one very large false "slow frame".

## Rendering behavior

The terminal frame is replayed first. The debug panel is then drawn directly onto the same Skia
canvas so it always remains above terminal content and never becomes part of the terminal buffer,
snapshot or selection model. Every adapter passes its actual presentation mode into the shared
backend. MAUI uses `SKGLView`, WinUI uses ANGLE-backed `SKSwapChainPanel`, WPF uses
`OpenTK.GLWpfControl`, and WinForms can use `OpenTK.GLControl` when `EnableGpuRendering` is enabled.
Each retains the existing software path as a fallback and reports `Unknown` until a frame has
actually been presented.

The panel uses:

- an eight-pixel top/right margin;
- a rounded translucent dark background;
- compact monospace text;
- clipping to the `TerminalView` bounds.

The Avalonia custom draw operation equality check includes the option value. The software adapters
invalidate their surfaces when it changes; WinUI also presents the full bitmap while telemetry is
enabled so its row-damage optimization cannot leave stale panel pixels.

## SSH demo integration

The SSH demo connection panel includes a **Show rendering debug overlay** checkbox. It remains
enabled while an SSH session is connected, allowing live comparison of normal shell output and
full-screen applications such as `btop` without reconnecting.

The overlay is enabled by default in every demo. The Avalonia SSH demo checkbox can be initialized
as disabled with:

```bash
XTERMSHARP_RENDERING_DEBUG=0 dotnet run \
  --project samples/XtermSharp.Avalonia.Demo.SSH/XtermSharp.Avalonia.Demo.SSH.csproj
```

Values `1` and `true` keep it enabled, matching the other boolean demo environment settings.

## Verification

Regression coverage verifies both the metrics and the rendered placement:

- frame intervals of 10, 20 and 30 ms produce 50 FPS, 20 ms average, 30 ms maximum and 10 ms
  minimum;
- the option defaults to disabled and is exposed through all five platform property systems;
- a Skia bitmap assertion confirms that the panel paints the top-right region while leaving the
  bottom-left region untouched.

The final tree was verified with:

- zero build warnings and errors;
- 1,462/1,462 main tests passing;
- 1/1 reference infrastructure test passing;
- 43/43 rendering and platform-adapter tests passing;
- 1,307 unique upstream bindings verified;
- the reference scenario reporting `MATCH`;
- all 76 escape-sequence fixtures matching the pinned xterm.js headless oracle;
- the SSH demo building successfully.

## Operational notes

The overlay intentionally adds a small amount of text formatting and Skia drawing work, so it should
remain disabled when telemetry is not needed. Its default-off behavior keeps the optimized normal
rendering path unchanged.

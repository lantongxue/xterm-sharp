---
layout: doc
title: Platform controls
description: Integrate the shared terminal and Skia renderer with Avalonia, .NET MAUI, Windows Forms, WPF, or WinUI 3.
category: Rendering
search: true
permalink: /platforms/
---

# Platform controls

All five controls share the same terminal state and retained Skia renderer. They differ only where
their UI framework requires native property, input, clipboard, scaling, and presentation behavior.

## Shared ownership model

Create one terminal in the application or session layer, assign it to a view, and dispose it when the
session ends:

```csharp
var terminal = new Terminal(new TerminalOptions
{
    Columns = 80,
    Rows = 24
});

var view = new TerminalView { Terminal = terminal };
```

`TerminalView` does not own `Terminal`. Detaching, unloading, or hot-swapping a view cancels frame and
link work and unsubscribes from events without ending the terminal session.

## Adapter comparison

| Adapter | GPU path | Software fallback | Text input |
| --- | --- | --- | --- |
| Avalonia | Host-owned Skia API lease | Same display list on a CPU-backed surface | Avalonia text input / IME client |
| .NET MAUI | `SKGLView` | `SKCanvasView` | Native committed text and soft keyboard |
| Windows Forms | OpenTK surface | DPI-scaled software Skia surface | `KeyPress` committed text / IME |
| WPF | OpenTK/WPF surface | Per-monitor-DPI `WriteableBitmap` | Preview text input / IME |
| WinUI 3 | ANGLE `SKSwapChainPanel` | DPI-scaled BGRA `WriteableBitmap` | `CoreTextEditContext` |

`RequestedRenderMode` is a preference. The active mode reports what the most recently presented frame
actually used and resets to `Unknown` when the control detaches. A GPU failure keeps software
presentation available, and a later `Auto` or `Gpu` request can attempt GPU presentation again.

## Avalonia

```bash
dotnet add package XtermSharp.Avalonia --version 0.1.0-alpha.1
```

```csharp
using XtermSharp.Avalonia.Controls;

var view = new TerminalView { Terminal = terminal };
```

The control replays retained pictures through Avalonia's current `ISkiaSharpApiLeaseFeature`. The
application host chooses the graphics API; the control does not create a second graphics context.

## .NET MAUI

Register the handlers before building the app:

```csharp
using XtermSharp.Maui.Hosting;

MauiApp app = MauiApp.CreateBuilder()
    .UseMauiApp<App>()
    .UseXtermSharpMaui()
    .Build();
```

The control keeps hit testing in MAUI logical coordinates and scales retained drawing to surface
pixels. Touch selection, scrollback, link activation, clipboard, and the soft keyboard are supported.

## Windows Forms

```csharp
using XtermSharp.WinForms.Controls;

var view = new TerminalView
{
    Dock = DockStyle.Fill,
    Terminal = terminal
};
```

The software path renders in logical coordinates onto a DPI-scaled Skia surface. GPU presentation is
opt-in through `RequestedRenderMode`; `EnableGpuRendering` remains a compatibility alias.

## WPF

```xml
<Window xmlns:xterm="clr-namespace:XtermSharp.Wpf.Controls;assembly=XtermSharp.Wpf">
  <xterm:TerminalView x:Name="TerminalView" Padding="8" />
</Window>
```

Viewport values are read-only dependency properties. The adapter reacts to unload and per-monitor DPI
changes by canceling or rescheduling pending work.

## WinUI 3

```xml
<Page xmlns:xterm="using:XtermSharp.WinUI.Controls">
  <xterm:TerminalView x:Name="TerminalView" Padding="8" />
</Page>
```

The WinUI control exposes get-only dependency-property wrappers for viewport state and uses
`CoreTextEditContext` for committed text and IME preedit.

## Input and clipboard behavior

Non-text keys such as Backspace and Delete always use `SendKeyAsync`; committed printable text uses
the platform text/IME path. This prevents Option or AltGr text from being sent twice. Enhanced
keyboard modes receive press, repeat, and release events with browser-compatible key coordinates.

Clipboard shortcuts use Meta on macOS and Control elsewhere. Copy consumes the shortcut only when the
selection is non-empty. OSC 52 access remains separately controlled by the
[clipboard addon policy](clipboard-addon.md).

## Rendering details

The platform event handlers only record invalidation state. Snapshot acquisition, scene compilation,
and drawing never block the terminal processor task. Continue with the
[rendering architecture](rendering-architecture.md) for display-list and threading details.

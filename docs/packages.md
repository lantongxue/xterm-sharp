---
layout: doc
title: Package map
description: Choose the headless core, rendering layers, native controls, and addons that match your application boundary.
category: Start
search: true
permalink: /packages/
---

# Package map

XtermSharp keeps the common package headless. Graphics, UI frameworks, system clipboard access, and
optional terminal behavior arrive through separate packages.

## Core and rendering

| Package | Purpose | UI dependency |
| --- | --- | --- |
| `XtermSharp` | VT parser, buffers, immutable snapshots, input encoding, links, selection, decorations, and addon contracts | None |
| `XtermSharp.Rendering` | Themes, geometry, damage tracking, selection resolution, and backend-neutral display lists | None |
| `XtermSharp.Rendering.Skia` | SkiaSharp/HarfBuzz shaping, font fallback, retained rows, and telemetry overlay | SkiaSharp |

Start with `XtermSharp` for services, tests, transcript processing, or a custom renderer. Applications
normally reference a platform package directly; its transitive references provide the shared
rendering layers.

## Platform controls

| Package | Application target | Presentation |
| --- | --- | --- |
| `XtermSharp.Avalonia` | Avalonia 12 | Current Avalonia Skia lease, with software fallback |
| `XtermSharp.Maui` | Android, iOS, Mac Catalyst, Windows | `SKGLView`, with `SKCanvasView` fallback |
| `XtermSharp.WinForms` | `net10.0-windows` | Optional OpenTK surface, with DPI-aware software fallback |
| `XtermSharp.Wpf` | `net10.0-windows` | OpenTK/WPF surface, with `WriteableBitmap` fallback |
| `XtermSharp.WinUI` | WinUI 3 | ANGLE-backed `SKSwapChainPanel`, with `WriteableBitmap` fallback |

Every `TerminalView` consumes an externally assigned `Terminal`. None of the controls owns or
disposes the terminal or its transport. See [platform controls](platforms.md) for setup and runtime
render-mode behavior.

## Addons

| Package | Behavior | Extra platform dependency |
| --- | --- | --- |
| `XtermSharp.Addons.WebLinks` | Validated URL detection, wrapped ranges, activation, hover, and leave callbacks | None |
| `XtermSharp.Addons.Search` | Forward/reverse text or regex search, selection, result tracking, and decorations | None |
| `XtermSharp.Addons.Progress` | Strict OSC 9;4 parsing and normalized progress state | None |
| `XtermSharp.Addons.Clipboard` | Policy-controlled OSC 52 read/write handling | Provider supplied by application or platform adapter |

Addons depend on the core, not on rendering. A headless service can query links or search state, while
a native control consumes the same link and decoration contracts automatically.

## Install a package

All packages currently use the same prerelease version:

```bash
dotnet add package XtermSharp.Avalonia --version 0.1.0-alpha.1
dotnet add package XtermSharp.Addons.Search --version 0.1.0-alpha.1
```

Avoid referencing every package as a bundle. Select one platform control and only the addons your
session policy permits.

## What is intentionally separate

XtermSharp does not ship a process launcher, PTY, SSH transport, browser renderer, WebGL backend, or
automatic OSC 8 URI opener. Those capabilities require application-specific lifetime, security, and
platform decisions and remain outside the common terminal engine.

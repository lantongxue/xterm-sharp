---
layout: doc
title: Getting started
description: Install the headless package, process terminal output, and connect XtermSharp to an application-owned transport.
category: Start
search: true
permalink: /getting-started/
---

# Getting started

XtermSharp separates terminal emulation from process and network transport. The core accepts output
from a PTY, SSH channel, serial connection, recording, or test fixture; it emits input and terminal
responses through events for the application to send back.

## Requirements

- .NET 10 SDK
- An application that owns the terminal lifetime
- A transport only when an interactive process or remote session is required

XtermSharp is currently prerelease software. Pin `0.1.0-alpha.1` when reproducible package restore is
important.

## Install the core

```bash
dotnet add package XtermSharp.Core --version 0.1.0-alpha.1
```

The core package has no UI framework or graphics dependency. See the [package map](packages.md) when
you also need rendering or a native control.

## Create a terminal

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

await terminal.WriteAsync("\x1b[32mhello\x1b[0m\r\n");

TerminalSnapshot snapshot = await terminal.GetSnapshotAsync();
foreach (TerminalLineSnapshot line in snapshot.ActiveBuffer.Lines)
{
    Console.WriteLine(line.TranslateToString(trimRight: true));
}
```

`WriteAsync` completes after the supplied input has been parsed and committed. The returned snapshot
is immutable: later writes cannot change its lines, cells, cursor, modes, or hyperlink metadata.

## Connect a transport

Terminal output flows into `WriteAsync`. Data produced by keyboard input, paste, mouse reports, focus
reports, and terminal responses flows back through `Terminal.Data`:

```csharp
terminal.Data += (_, args) => transport.Write(args.Data);

transport.OutputReceived += async (_, bytes) =>
{
    await terminal.WriteAsync(bytes);
};
```

The application owns `transport`, error handling, reconnect policy, host-key verification, and
credentials. XtermSharp does not start processes and does not include a PTY or SSH implementation.
The SSH samples use SSH.NET at the application boundary without adding it to the library.

## Respect command ordering

Every terminal owns one command queue and one processor task. Writes, resizes, resets, scrolling, and
option updates execute in admission order.

Cancellation applies only while a write is waiting for queue capacity. Once admitted, the write
finishes so a terminal byte stream cannot be partially applied or reordered. Do not queue another
terminal command from inside an active asynchronous parser handler; use its short-lived parser
context when the handler must emit a response.

## Choose a snapshot scope

The default snapshot contains the viewport. Request the active buffer when a search, export, or
diagnostic needs scrollback:

```csharp
TerminalSnapshot viewport = await terminal.GetSnapshotAsync();
TerminalSnapshot buffer = await terminal.GetSnapshotAsync(SnapshotScope.ActiveBuffer);
```

Snapshot hyperlink IDs are local to that snapshot. Resolve them through `snapshot.GetHyperlink(id)`
instead of retaining numeric IDs between revisions.

## Add a native view

Install one platform adapter and assign the same externally owned terminal:

```csharp
using XtermSharp.Avalonia.Controls;

var view = new TerminalView
{
    Terminal = terminal
};
```

The view subscribes to the terminal but never disposes it. It handles rendering and interaction while
the application continues to own transport and session lifetime. Compare all five adapters in the
[platform controls guide](platforms.md).

## Next steps

- Use the [package map](packages.md) to select only the dependencies you need.
- Review the [architecture](architecture.md) before adding parser handlers or high-volume writers.
- Add [web links, search, progress, or clipboard behavior](addons.md).
- Check the [implementation status](implementation-status.md) before relying on prerelease behavior.

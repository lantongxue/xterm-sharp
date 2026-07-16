# XtermSharp

XtermSharp is an experimental, headless terminal emulator for .NET 10. It is a
pure C# translation and redesign based on xterm.js 6.0.0. The library parses
terminal output, maintains normal and alternate screen buffers, and exposes
immutable snapshots suitable for server-side processing or a future renderer.

> Current status: `0.1.0-alpha`. The common VT behavior is usable, but this is
> not yet a complete xterm.js headless replacement. See
> [implementation status](docs/implementation-status.md).

## Example

```csharp
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

## Build and verify

```bash
dotnet build XtermSharp.sln -m:1
dotnet test --project tests/XtermSharp.Tests/XtermSharp.Tests.csproj
dotnet test --project tests/XtermSharp.ReferenceTests/XtermSharp.ReferenceTests.csproj
dotnet run --project tools/XtermSharp.TestMap/XtermSharp.TestMap.csproj -- --check
dotnet run --project benchmarks/XtermSharp.Benchmarks/XtermSharp.Benchmarks.csproj -c Release
```

The test projects use xUnit v3. The checked-in manifests pin xterm.js 6.0.0 at
commit `b1aee19ac6d6f4e4d11e4a10a3731b852956bdb7`: 1,361 upstream cases are
discovered, 54 front-end renderer cases are excluded from the headless scope,
and all 1,307 applicable cases are bound to C# tests (1,306 direct ports and one
documented architecture-equivalent test). The suite also runs all 76 upstream
escape-sequence fixtures.

For differential testing, build the pinned xterm.js headless bundle and run:

```bash
node tools/compare-reference.mjs tools/sample-request.json
node tools/compare-fixtures.mjs
```

## Scope

The core package does not start processes and does not implement PTY, SSH,
browser, DOM, WebGL, Avalonia, WPF, or WinUI integration. Those belong in
separate adapter or renderer packages.

XtermSharp is licensed under MIT. See [NOTICE.md](NOTICE.md) for the upstream
baseline and attribution.

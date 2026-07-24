# XtermSharp .NET MAUI SSH demo

This sample connects `XtermSharp.Maui.Controls.TerminalView` to a real remote shell using SSH.NET.
Rendering uses the shared SkiaSharp/HarfBuzz backend through `SKCanvasView`.

The transport, authentication, host-key verification, output pump and PTY resize implementation are
compiled from the same source files as the Avalonia SSH demo. The MAUI-specific Core project keeps
the page buildable on machines without platform workloads, while the app project targets Android,
iOS, Mac Catalyst and Windows.

Install the MAUI workload, then build the desired target framework:

- net10.0-android
- net10.0-ios
- net10.0-maccatalyst
- net10.0-windows10.0.19041.0

The Windows target uses MAUI's unpackaged local-development mode. Run it with:

```bash
dotnet build samples/XtermSharp.Maui.Demo.SSH/XtermSharp.Maui.Demo.SSH.csproj -f net10.0-windows10.0.19041.0 -m:1
dotnet run --project samples/XtermSharp.Maui.Demo.SSH/XtermSharp.Maui.Demo.SSH.csproj -f net10.0-windows10.0.19041.0 --no-build
```

The app only calls `UseXtermSharpMaui()` during startup. Keyboard shortcuts, committed text,
selection clearing and mouse-wheel forwarding are registered by the shared `XtermSharp.Maui`
component; no application-side Windows input adapter is required.

The connection form supports password and private-key authentication, encrypted private keys,
configurable terminal type, SHA-256 host-key verification and a test-only verification bypass.
On a first connection, leave the fingerprint empty. The connection stops before authentication and
fills in the server fingerprint; verify it through a trusted channel, then connect again.

Use the **Rendering** selector in the settings panel to switch live between `Auto`, `Software` and
`Gpu`; the terminal keeps the current session while the requested surface changes.

The same environment variables supported by the Avalonia SSH demo can prefill the form:

| Variable | Meaning |
| --- | --- |
| SSH_HOST | Server hostname or IP address |
| SSH_PORT | Server port, default 22 |
| SSH_USER | SSH username |
| SSH_PASSWORD | Password authentication value |
| SSH_PRIVATE_KEY | Private-key file path; selects private-key authentication |
| SSH_PRIVATE_KEY_PASSPHRASE | Optional private-key passphrase |
| SSH_TERM | Remote TERM value, default xterm-256color |
| SSH_HOST_KEY_SHA256 | Trusted SHA256:... server fingerprint |
| SSH_ACCEPT_ANY_HOST_KEY | Set to 1 or true to skip verification |

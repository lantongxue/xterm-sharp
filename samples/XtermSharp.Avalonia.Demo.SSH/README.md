# XtermSharp Avalonia SSH demo

This sample connects `TerminalView` to a real remote shell using SSH.NET. It is
kept separate from `XtermSharp.Avalonia.Demo`, which remains the no-PTY local
input-echo smoke test.

Run it from the repository root:

```bash
dotnet run --project samples/XtermSharp.Avalonia.Demo.SSH/XtermSharp.Avalonia.Demo.SSH.csproj
```

The connection form supports:

- SSH host, port and username.
- Password or private-key authentication, including encrypted key files.
- A configurable terminal type, defaulting to `xterm-256color`.
- SHA-256 server host-key verification.
- A test-only option to skip host-key verification.

For a first connection, leave the host-key field empty and keep the skip option
disabled. The connection will stop before authentication and populate the
server's `SHA256:...` fingerprint. Verify that fingerprint through a trusted
channel, then click **Connect** again. Skipping verification is convenient for
disposable local test servers but exposes the SSH connection to
man-in-the-middle attacks.

The form can be prefilled with these environment variables:

| Variable | Meaning |
| --- | --- |
| `SSH_HOST` | Server hostname or IP address |
| `SSH_PORT` | Server port, default `22` |
| `SSH_USER` | SSH username |
| `SSH_PASSWORD` | Password authentication value |
| `SSH_PRIVATE_KEY` | Private-key file path; selects private-key authentication |
| `SSH_PRIVATE_KEY_PASSPHRASE` | Optional private-key passphrase |
| `SSH_TERM` | Remote `TERM` value, default `xterm-256color` |
| `SSH_HOST_KEY_SHA256` | Trusted `SHA256:...` server fingerprint |
| `SSH_ACCEPT_ANY_HOST_KEY` | Set to `1` or `true` to skip verification |
| `XTERMSHARP_RENDERING_DEBUG` | Rendering overlay is shown by default; set to another value to hide it |

The **Show rendering debug overlay** checkbox remains available while connected, so telemetry can be
toggled while running an interactive shell or a full-screen application.

The demo does not persist connection values or credentials. Terminal input,
remote output and resize notifications are bridged only while the SSH session
is connected.

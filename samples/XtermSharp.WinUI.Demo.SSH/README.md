# XtermSharp WinUI SSH demo

This packaged WinUI 3 sample connects `XtermSharp.WinUI.Controls.TerminalView` to a real remote
pseudo-terminal through SSH.NET. Run it from the repository root:

```bash
dotnet run --project samples/XtermSharp.WinUI.Demo.SSH/XtermSharp.WinUI.Demo.SSH.csproj
```

The project uses the official single-project MSIX template. Its WinApp build-tools dependency
registers a development identity and launches the packaged app for `dotnet run`.

The responsive connection form supports password and private-key authentication, encrypted
private keys, a configurable remote terminal type, remote window-size updates and SHA-256 host-key
verification.

On a first connection, leave the trusted-host-key field empty and keep **Skip verification**
disabled. The attempt stops before authentication and fills in the fingerprint reported by the
server. Verify it through a trusted channel, then connect again. Skipping verification is intended
only for disposable local test servers and exposes the connection to man-in-the-middle attacks.

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

The sample does not persist connection values or credentials. SSH.NET remains a sample-only
dependency; `XtermSharp.WinUI` and the other library packages stay transport-agnostic.

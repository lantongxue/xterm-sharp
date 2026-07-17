namespace XtermSharp.Avalonia.Demo.SSH;

internal enum SshAuthenticationKind
{
    Password,
    PrivateKey
}

internal sealed record SshConnectionSettings(
    string Host,
    int Port,
    string Username,
    SshAuthenticationKind AuthenticationKind,
    string Password,
    string PrivateKeyPath,
    string PrivateKeyPassphrase,
    string TerminalType,
    string HostKeySha256,
    bool AcceptAnyHostKey);

internal sealed class HostKeyVerificationException(
    string fingerprint,
    string message,
    Exception innerException) : Exception(message, innerException)
{
    public string Fingerprint { get; } = fingerprint;
}

namespace XtermSharp.Avalonia.Demo.SSH;

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

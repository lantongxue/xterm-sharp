namespace XtermSharp.Avalonia.Demo.SSH.Exceptions;

internal sealed class HostKeyVerificationException(
    string fingerprint,
    string message,
    Exception innerException) : Exception(message, innerException)
{
    public string Fingerprint { get; } = fingerprint;
}

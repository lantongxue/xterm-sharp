using Renci.SshNet;
using Renci.SshNet.Common;
using System.Security.Cryptography;
using System.Threading.Channels;

namespace XtermSharp.Avalonia.Demo.SSH.Events;

internal sealed class SshSessionEndedEventArgs(Exception? exception) : EventArgs
{
    public Exception? Exception { get; } = exception;
}

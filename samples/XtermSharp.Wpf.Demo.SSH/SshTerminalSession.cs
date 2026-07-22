using System.Text;
using System.Threading.Channels;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace XtermSharp.Wpf.Demo.SSH;

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

internal sealed class SshSessionEndedEventArgs(Exception? exception) : EventArgs
{
    public Exception? Exception { get; } = exception;
}

internal sealed class SshTerminalSession : IAsyncDisposable
{
    private readonly Terminal _terminal;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private SshConnectionSettings? _settings;
    private AuthenticationMethod? _authentication;
    private SshClient? _client;
    private ShellStream? _shell;
    private Channel<ShellOperation>? _outbound;
    private CancellationTokenSource? _sessionCancellation;
    private Task? _remoteOutputPump;
    private Task? _outboundPump;
    private string _observedHostKeyFingerprint = string.Empty;
    private bool _hostKeyRejected;
    private int _connected;
    private int _sessionEndReported;
    private int _disposed;

    public SshTerminalSession(Terminal terminal)
    {
        _terminal = terminal ?? throw new ArgumentNullException(nameof(terminal));
    }

    public bool IsConnected => Volatile.Read(ref _connected) != 0;
    public string ObservedHostKeyFingerprint => _observedHostKeyFingerprint;

    public event EventHandler<SshSessionEndedEventArgs>? Ended;

    public async Task ConnectAsync(SshConnectionSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            if (_client is not null)
            {
                throw new InvalidOperationException("The SSH session is already connected or connecting.");
            }

            _settings = settings;
            _observedHostKeyFingerprint = string.Empty;
            _hostKeyRejected = false;
            Interlocked.Exchange(ref _sessionEndReported, 0);
            AuthenticationMethod authentication = CreateAuthentication(settings);
            _authentication = authentication;
            var connectionInfo = new ConnectionInfo(
                settings.Host,
                settings.Port,
                settings.Username,
                authentication)
            {
                Timeout = TimeSpan.FromSeconds(15)
            };

            try
            {
                var client = new SshClient(connectionInfo)
                {
                    KeepAliveInterval = TimeSpan.FromSeconds(30)
                };
                _client = client;
                client.HostKeyReceived += OnHostKeyReceived;
                client.ErrorOccurred += OnClientError;
                await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
                ShellStream shell = await Task.Run(
                    () => client.CreateShellStream(
                        settings.TerminalType,
                        (uint)_terminal.Columns,
                        (uint)_terminal.Rows,
                        0,
                        0,
                        1024 * 1024),
                    cancellationToken).ConfigureAwait(false);
                _shell = shell;
                shell.ErrorOccurred += OnShellError;
                shell.Closed += OnShellClosed;

                var outbound = Channel.CreateUnbounded<ShellOperation>(new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false,
                    AllowSynchronousContinuations = false
                });
                var sessionCancellation = new CancellationTokenSource();
                _outbound = outbound;
                _sessionCancellation = sessionCancellation;
                _terminal.Data += OnTerminalData;
                _terminal.Binary += OnTerminalBinary;
                _terminal.Resized += OnTerminalResized;
                Volatile.Write(ref _connected, 1);
                _remoteOutputPump = Task.Run(
                    () => PumpRemoteOutputAsync(shell, sessionCancellation.Token),
                    CancellationToken.None);
                _outboundPump = PumpOutboundAsync(shell, outbound.Reader, sessionCancellation.Token);
            }
            catch (Exception exception) when (_hostKeyRejected && !cancellationToken.IsCancellationRequested)
            {
                await CleanupFailedConnectionAsync().ConfigureAwait(false);
                string fingerprint = _observedHostKeyFingerprint;
                throw new HostKeyVerificationException(
                    fingerprint,
                    $"The SSH host key was not trusted. Server fingerprint: {fingerprint}",
                    exception);
            }
            catch
            {
                await CleanupFailedConnectionAsync().ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask DisconnectAsync()
    {
        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await DisconnectCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }
        await DisconnectAsync().ConfigureAwait(false);
        _lifecycleGate.Dispose();
    }

    private static AuthenticationMethod CreateAuthentication(SshConnectionSettings settings) =>
        settings.AuthenticationKind switch
        {
            SshAuthenticationKind.Password =>
                new PasswordAuthenticationMethod(settings.Username, settings.Password),
            SshAuthenticationKind.PrivateKey =>
                new PrivateKeyAuthenticationMethod(
                    settings.Username,
                    string.IsNullOrEmpty(settings.PrivateKeyPassphrase)
                        ? new PrivateKeyFile(settings.PrivateKeyPath)
                        : new PrivateKeyFile(settings.PrivateKeyPath, settings.PrivateKeyPassphrase)),
            _ => throw new ArgumentOutOfRangeException(nameof(settings))
        };

    private async Task PumpRemoteOutputAsync(ShellStream shell, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[64 * 1024];
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int count = shell.Read(buffer, 0, buffer.Length);
                if (count == 0)
                {
                    break;
                }
                await _terminal.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
            }
            if (!cancellationToken.IsCancellationRequested)
            {
                ReportSessionEnded(null);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ReportSessionEnded(exception);
        }
    }

    private async Task PumpOutboundAsync(
        ShellStream shell,
        ChannelReader<ShellOperation> reader,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (ShellOperation operation in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                switch (operation)
                {
                    case WriteOperation write:
                        await shell.WriteAsync(write.Data.AsMemory(), cancellationToken).ConfigureAwait(false);
                        await shell.FlushAsync(cancellationToken).ConfigureAwait(false);
                        break;
                    case ResizeOperation resize:
                        shell.ChangeWindowSize((uint)resize.Columns, (uint)resize.Rows, 0, 0);
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ReportSessionEnded(exception);
        }
    }

    private void OnTerminalData(object? sender, TerminalDataEventArgs args)
    {
        if (args.Data.Length != 0)
        {
            _outbound?.Writer.TryWrite(new WriteOperation(Encoding.UTF8.GetBytes(args.Data)));
        }
    }

    private void OnTerminalBinary(object? sender, TerminalDataEventArgs args)
    {
        if (args.Data.Length == 0)
        {
            return;
        }
        byte[] bytes = GC.AllocateUninitializedArray<byte>(args.Data.Length);
        for (int index = 0; index < args.Data.Length; index++)
        {
            bytes[index] = (byte)args.Data[index];
        }
        _outbound?.Writer.TryWrite(new WriteOperation(bytes));
    }

    private void OnTerminalResized(object? sender, TerminalResizeEventArgs args)
    {
        _outbound?.Writer.TryWrite(new ResizeOperation(args.Columns, args.Rows));
    }

    private void OnHostKeyReceived(object? sender, HostKeyEventArgs args)
    {
        string fingerprint = $"SHA256:{args.FingerPrintSHA256}";
        _observedHostKeyFingerprint = fingerprint;
        SshConnectionSettings? settings = _settings;
        string expected = NormalizeSha256Fingerprint(settings?.HostKeySha256);
        bool trusted = settings is not null &&
            (settings.AcceptAnyHostKey ||
             expected.Length != 0 && string.Equals(expected, args.FingerPrintSHA256, StringComparison.Ordinal));
        _hostKeyRejected = !trusted;
        args.CanTrust = trusted;
    }

    private void OnClientError(object? sender, ExceptionEventArgs args)
    {
        if (IsConnected)
        {
            ReportSessionEnded(args.Exception);
        }
    }

    private void OnShellError(object? sender, ExceptionEventArgs args) => ReportSessionEnded(args.Exception);

    private void OnShellClosed(object? sender, EventArgs args)
    {
        if (_sessionCancellation?.IsCancellationRequested == false)
        {
            ReportSessionEnded(null);
        }
    }

    private void ReportSessionEnded(Exception? exception)
    {
        if (!IsConnected || Interlocked.Exchange(ref _sessionEndReported, 1) != 0)
        {
            return;
        }
        Ended?.Invoke(this, new SshSessionEndedEventArgs(exception));
    }

    private async Task CleanupFailedConnectionAsync()
    {
        ShellStream? shell = _shell;
        SshClient? client = _client;
        AuthenticationMethod? authentication = _authentication;
        _shell = null;
        _client = null;
        _authentication = null;
        _settings = null;
        if (shell is not null)
        {
            shell.ErrorOccurred -= OnShellError;
            shell.Closed -= OnShellClosed;
            TryDispose(shell);
        }
        if (client is not null)
        {
            client.HostKeyReceived -= OnHostKeyReceived;
            client.ErrorOccurred -= OnClientError;
            await DisposeClientAsync(client).ConfigureAwait(false);
        }
        if (authentication is not null)
        {
            TryDispose(authentication);
        }
    }

    private async Task DisconnectCoreAsync()
    {
        Volatile.Write(ref _connected, 0);
        _terminal.Data -= OnTerminalData;
        _terminal.Binary -= OnTerminalBinary;
        _terminal.Resized -= OnTerminalResized;
        CancellationTokenSource? sessionCancellation = _sessionCancellation;
        Channel<ShellOperation>? outbound = _outbound;
        ShellStream? shell = _shell;
        SshClient? client = _client;
        AuthenticationMethod? authentication = _authentication;
        Task? remoteOutputPump = _remoteOutputPump;
        Task? outboundPump = _outboundPump;
        _settings = null;
        _sessionCancellation = null;
        _outbound = null;
        _shell = null;
        _client = null;
        _authentication = null;
        _remoteOutputPump = null;
        _outboundPump = null;
        sessionCancellation?.Cancel();
        outbound?.Writer.TryComplete();
        if (shell is not null)
        {
            shell.ErrorOccurred -= OnShellError;
            shell.Closed -= OnShellClosed;
            TryDispose(shell);
        }
        if (client is not null)
        {
            client.HostKeyReceived -= OnHostKeyReceived;
            client.ErrorOccurred -= OnClientError;
            await DisposeClientAsync(client).ConfigureAwait(false);
        }
        if (authentication is not null)
        {
            TryDispose(authentication);
        }
        await ObservePumpAsync(remoteOutputPump).ConfigureAwait(false);
        await ObservePumpAsync(outboundPump).ConfigureAwait(false);
        sessionCancellation?.Dispose();
    }

    private static async Task ObservePumpAsync(Task? pump)
    {
        if (pump is null)
        {
            return;
        }
        try
        {
            await pump.ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private static void TryDispose(IDisposable value)
    {
        try
        {
            value.Dispose();
        }
        catch
        {
        }
    }

    private static async Task DisposeClientAsync(SshClient client)
    {
        try
        {
            await Task.Run(client.Dispose).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private static string NormalizeSha256Fingerprint(string? value)
    {
        string fingerprint = value?.Trim() ?? string.Empty;
        if (fingerprint.StartsWith("SHA256:", StringComparison.OrdinalIgnoreCase))
        {
            fingerprint = fingerprint[7..];
        }
        return fingerprint.TrimEnd('=');
    }

    private abstract record ShellOperation;
    private sealed record WriteOperation(byte[] Data) : ShellOperation;
    private sealed record ResizeOperation(int Columns, int Rows) : ShellOperation;
}

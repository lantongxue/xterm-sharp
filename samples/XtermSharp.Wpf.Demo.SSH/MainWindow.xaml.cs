using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace XtermSharp.Wpf.Demo.SSH;

public partial class MainWindow : Window
{
    private readonly CancellationTokenSource _windowCancellation = new();
    private readonly Terminal _terminal;
    private SshTerminalSession? _session;
    private bool _closing;
    private bool _shutdownComplete;

    public MainWindow()
    {
        InitializeComponent();
        _terminal = new Terminal(new TerminalOptions
        {
            Columns = 100,
            Rows = 30,
            FontFamily = "Cascadia Mono, Consolas, monospace",
            FontSize = 15,
            Scrollback = 5_000,
            UnicodeVersion = UnicodeV15Provider.GraphemeVersionName,
            AllowProposedApi = true
        });
        TerminalView.Terminal = _terminal;
        _terminal.TitleChanged += OnTerminalTitleChanged;
        RenderingModeComboBox.ItemsSource = Enum.GetValues<SkiaRenderModePreference>();
        RenderingModeComboBox.SelectedItem = SkiaRenderModePreference.Auto;

        HostTextBox.Text = GetEnvironmentValue("SSH_HOST", "localhost");
        PortTextBox.Text = GetEnvironmentValue("SSH_PORT", "22");
        UsernameTextBox.Text = GetEnvironmentValue("SSH_USER", Environment.UserName);
        PasswordBox.Password = Environment.GetEnvironmentVariable("SSH_PASSWORD") ?? string.Empty;
        PrivateKeyTextBox.Text = Environment.GetEnvironmentVariable("SSH_PRIVATE_KEY") ?? string.Empty;
        PrivateKeyPassphraseBox.Password =
            Environment.GetEnvironmentVariable("SSH_PRIVATE_KEY_PASSPHRASE") ?? string.Empty;
        string terminalType = GetEnvironmentValue("SSH_TERM", "xterm-256color");
        PasswordTerminalTypeTextBox.Text = terminalType;
        KeyTerminalTypeTextBox.Text = terminalType;
        HostKeyTextBox.Text = Environment.GetEnvironmentVariable("SSH_HOST_KEY_SHA256") ?? string.Empty;
        AcceptAnyHostKeyCheckBox.IsChecked = IsTrue(Environment.GetEnvironmentVariable("SSH_ACCEPT_ANY_HOST_KEY"));
        AuthenticationComboBox.SelectedIndex = string.IsNullOrWhiteSpace(PrivateKeyTextBox.Text) ? 0 : 1;
        UpdateAuthenticationPanels();
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (_session?.IsConnected == true)
        {
            await DisconnectCurrentSessionAsync();
        }
        else
        {
            await ConnectAsync();
        }
    }

    private async Task ConnectAsync()
    {
        SshConnectionSettings settings;
        try
        {
            settings = ReadSettings();
        }
        catch (InvalidOperationException exception)
        {
            SetStatus(exception.Message, StatusKind.Error);
            return;
        }

        SetConfigurationEnabled(false);
        ConnectButton.IsEnabled = false;
        ConnectButton.Content = "Connecting...";
        SetStatus($"Connecting to {settings.Username}@{settings.Host}:{settings.Port}...", StatusKind.Working);
        var session = new SshTerminalSession(_terminal);
        _session = session;
        session.Ended += OnSessionEnded;
        try
        {
            await _terminal.ResetAsync(_windowCancellation.Token);
            await _terminal.ClearAsync(_windowCancellation.Token);
            await session.ConnectAsync(settings, _windowCancellation.Token);
            ConnectButton.Content = "Disconnect";
            ConnectButton.IsEnabled = true;
            string verification = settings.AcceptAnyHostKey
                ? "host key verification skipped"
                : $"host key {session.ObservedHostKeyFingerprint}";
            SetStatus(
                $"Connected to {settings.Username}@{settings.Host}:{settings.Port} ({verification}).",
                StatusKind.Connected);
            _ = TerminalView.Focus();
        }
        catch (OperationCanceledException) when (_windowCancellation.IsCancellationRequested)
        {
            await DisposeFailedSessionAsync(session);
        }
        catch (HostKeyVerificationException exception)
        {
            await DisposeFailedSessionAsync(session);
            if (string.IsNullOrWhiteSpace(HostKeyTextBox.Text))
            {
                HostKeyTextBox.Text = exception.Fingerprint;
                _ = HostKeyTextBox.Focus();
                SetStatus(
                    $"Host key not trusted. Verify {exception.Fingerprint} through a trusted channel, then connect again.",
                    StatusKind.Error);
            }
            else
            {
                MarkInvalid(HostKeyTextBox, "The server host key does not match this fingerprint.");
                _ = HostKeyTextBox.Focus();
                SetStatus(
                    $"Host key mismatch. Server: {exception.Fingerprint}; expected: {HostKeyTextBox.Text}.",
                    StatusKind.Error);
            }
        }
        catch (Exception exception)
        {
            await DisposeFailedSessionAsync(session);
            SetStatus($"Connection failed: {exception.Message}", StatusKind.Error);
        }
    }

    private async Task DisposeFailedSessionAsync(SshTerminalSession session)
    {
        session.Ended -= OnSessionEnded;
        await session.DisposeAsync();
        if (ReferenceEquals(_session, session))
        {
            _session = null;
        }
        SetConfigurationEnabled(true);
        ConnectButton.Content = "Connect";
        ConnectButton.IsEnabled = true;
    }

    private async Task DisconnectCurrentSessionAsync()
    {
        SshTerminalSession? session = _session;
        if (session is null)
        {
            return;
        }
        _session = null;
        session.Ended -= OnSessionEnded;
        ConnectButton.IsEnabled = false;
        ConnectButton.Content = "Disconnecting...";
        SetStatus("Disconnecting...", StatusKind.Working);
        await session.DisposeAsync();
        SetConfigurationEnabled(true);
        ConnectButton.Content = "Connect";
        ConnectButton.IsEnabled = true;
        SetStatus("Disconnected. Enter connection details and connect.", StatusKind.Idle);
    }

    private void OnSessionEnded(object? sender, SshSessionEndedEventArgs args)
    {
        if (sender is not SshTerminalSession session || _closing || Dispatcher.HasShutdownStarted)
        {
            return;
        }
        _ = Dispatcher.InvokeAsync(() => _ = HandleSessionEndedAsync(session, args.Exception));
    }

    private async Task HandleSessionEndedAsync(SshTerminalSession session, Exception? exception)
    {
        if (_closing || !ReferenceEquals(_session, session))
        {
            return;
        }
        await DisconnectCurrentSessionAsync();
        SetStatus(
            exception is null ? "The remote shell closed the connection." : $"SSH session ended: {exception.Message}",
            exception is null ? StatusKind.Idle : StatusKind.Error);
    }

    private void AuthenticationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        UpdateAuthenticationPanels();
    }

    private void RenderingModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (RenderingModeComboBox.SelectedItem is SkiaRenderModePreference mode)
        {
            TerminalView.RequestedRenderMode = mode;
        }
    }

    private void UpdateAuthenticationPanels()
    {
        if (PasswordPanel is null || PrivateKeyPanel is null)
        {
            return;
        }
        bool privateKey = AuthenticationComboBox.SelectedIndex == 1;
        if (privateKey)
        {
            KeyTerminalTypeTextBox.Text = PasswordTerminalTypeTextBox.Text;
        }
        else
        {
            PasswordTerminalTypeTextBox.Text = KeyTerminalTypeTextBox.Text;
        }
        PasswordPanel.Visibility = privateKey ? Visibility.Collapsed : Visibility.Visible;
        PrivateKeyPanel.Visibility = privateKey ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BrowsePrivateKeyButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        var dialog = new OpenFileDialog
        {
            Title = "Select an SSH private key",
            CheckFileExists = true,
            Multiselect = false,
            Filter = "Private keys|*.pem;*.key;id_*|All files|*.*"
        };
        if (dialog.ShowDialog(this) == true)
        {
            PrivateKeyTextBox.Text = dialog.FileName;
        }
    }

    private SshConnectionSettings ReadSettings()
    {
        ClearValidationErrors();
        string host = Required(HostTextBox, "Host is required.");
        if (!int.TryParse(PortTextBox.Text, out int port) || port is < 1 or > 65_535)
        {
            return InvalidSettings<SshConnectionSettings>(PortTextBox, "Port must be between 1 and 65535.");
        }
        string username = Required(UsernameTextBox, "Username is required.");
        bool privateKey = AuthenticationComboBox.SelectedIndex == 1;
        TextBox terminalTypeControl = privateKey ? KeyTerminalTypeTextBox : PasswordTerminalTypeTextBox;
        string terminalType = Required(terminalTypeControl, "Terminal type is required.");
        string privateKeyPath = ExpandHome(PrivateKeyTextBox.Text.Trim());
        if (privateKey && !File.Exists(privateKeyPath))
        {
            return InvalidSettings<SshConnectionSettings>(
                PrivateKeyTextBox,
                "Select an existing SSH private key file.");
        }
        return new SshConnectionSettings(
            host,
            port,
            username,
            privateKey ? SshAuthenticationKind.PrivateKey : SshAuthenticationKind.Password,
            PasswordBox.Password,
            privateKeyPath,
            PrivateKeyPassphraseBox.Password,
            terminalType,
            HostKeyTextBox.Text.Trim(),
            AcceptAnyHostKeyCheckBox.IsChecked == true);
    }

    private string Required(TextBox control, string message)
    {
        string value = control.Text.Trim();
        return value.Length == 0 ? InvalidSettings<string>(control, message) : value;
    }

    private T InvalidSettings<T>(Control control, string message)
    {
        MarkInvalid(control, message);
        _ = control.Focus();
        throw new InvalidOperationException(message);
    }

    private void MarkInvalid(Control control, string message)
    {
        control.BorderBrush = (Brush)FindResource("ErrorBrush");
        control.ToolTip = message;
        AutomationProperties.SetHelpText(control, message);
    }

    private void ClearValidationErrors()
    {
        Brush border = (Brush)FindResource("BorderBrush");
        Control[] controls =
        [
            HostTextBox,
            PortTextBox,
            UsernameTextBox,
            PrivateKeyTextBox,
            PasswordTerminalTypeTextBox,
            KeyTerminalTypeTextBox,
            HostKeyTextBox
        ];
        foreach (Control control in controls)
        {
            control.BorderBrush = border;
            control.ToolTip = null;
            AutomationProperties.SetHelpText(control, string.Empty);
        }
    }

    private void SetConfigurationEnabled(bool enabled)
    {
        HostTextBox.IsEnabled = enabled;
        PortTextBox.IsEnabled = enabled;
        UsernameTextBox.IsEnabled = enabled;
        AuthenticationComboBox.IsEnabled = enabled;
        PasswordPanel.IsEnabled = enabled;
        PrivateKeyPanel.IsEnabled = enabled;
        HostKeyTextBox.IsEnabled = enabled;
        AcceptAnyHostKeyCheckBox.IsEnabled = enabled;
    }

    private void SetStatus(string message, StatusKind kind)
    {
        StatusTextBlock.Text = message;
        string brushKey = kind switch
        {
            StatusKind.Connected => "SuccessBrush",
            StatusKind.Error => "ErrorBrush",
            _ => "SecondaryTextBrush"
        };
        var brush = (Brush)FindResource(brushKey);
        StatusTextBlock.Foreground = brush;
        StatusIndicator.Fill = brush;
    }

    private void OnTerminalTitleChanged(object? sender, TerminalTitleChangedEventArgs args)
    {
        _ = sender;
        if (_closing || Dispatcher.HasShutdownStarted)
        {
            return;
        }
        _ = Dispatcher.InvokeAsync(() =>
        {
            if (!_closing)
            {
                Title = string.IsNullOrWhiteSpace(args.Title)
                    ? "XtermSharp WPF SSH Demo"
                    : $"{args.Title} - XtermSharp SSH";
            }
        });
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _ = sender;
        if (_shutdownComplete)
        {
            return;
        }
        e.Cancel = true;
        if (!_closing)
        {
            _ = CloseAsync();
        }
    }

    private async Task CloseAsync()
    {
        _closing = true;
        IsEnabled = false;
        _windowCancellation.Cancel();
        _terminal.TitleChanged -= OnTerminalTitleChanged;
        SshTerminalSession? session = _session;
        _session = null;
        if (session is not null)
        {
            session.Ended -= OnSessionEnded;
            await session.DisposeAsync();
        }
        TerminalView.Terminal = null;
        TerminalView.Dispose();
        await _terminal.DisposeAsync();
        _windowCancellation.Dispose();
        _shutdownComplete = true;
        Close();
    }

    private static string ExpandHome(string path)
    {
        if (path == "~")
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        if (path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..]);
        }
        return path;
    }

    private static string GetEnvironmentValue(string name, string fallback) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : fallback;

    private static bool IsTrue(string? value) =>
        value is not null && (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase));

    private enum StatusKind
    {
        Idle,
        Working,
        Connected,
        Error
    }
}

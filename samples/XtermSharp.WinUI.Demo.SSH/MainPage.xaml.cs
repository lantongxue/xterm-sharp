using System.IO;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace XtermSharp.WinUI.Demo.SSH;

public sealed partial class MainPage : Page
{
    private readonly CancellationTokenSource _pageCancellation = new();
    private readonly Terminal _terminal;
    private SshTerminalSession? _session;
    private bool _loaded;
    private bool _closing;
    private bool _shutdownComplete;

    public MainPage()
    {
        InitializeComponent();
        _terminal = new Terminal(new TerminalOptions
        {
            Columns = 100,
            Rows = 30,
            FontFamily = "Cascadia Mono, Consolas, monospace",
            FontSize = 15,
            Scrollback = 5_000,
            AllowProposedApi = true
        });
        TerminalView.Terminal = _terminal;
        _terminal.TitleChanged += OnTerminalTitleChanged;

        HostTextBox.Text = GetEnvironmentValue("SSH_HOST", "localhost");
        PortNumberBox.Value = GetEnvironmentPort();
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

    private void Page_Loaded(object sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        if (_loaded)
        {
            return;
        }
        _loaded = true;
        App.Current.MainWindow.AppWindow.Closing += OnWindowClosing;
    }

    private void Page_SizeChanged(object sender, SizeChangedEventArgs args)
    {
        _ = sender;
        bool wide = args.NewSize.Width >= 900;
        SetGridPlacement(HostTextBox, 0, 0, wide ? 1 : 4);
        SetGridPlacement(PortNumberBox, wide ? 0 : 1, wide ? 1 : 0, wide ? 1 : 2);
        SetGridPlacement(UsernameTextBox, wide ? 0 : 1, 2, wide ? 1 : 2);
        SetGridPlacement(AuthenticationComboBox, wide ? 0 : 2, wide ? 3 : 0, wide ? 1 : 4);
        SetGridPlacement(PasswordPanel, wide ? 1 : 3, 0, wide ? 2 : 4);
        SetGridPlacement(PrivateKeyPanel, wide ? 1 : 3, 0, wide ? 2 : 4);
        SetGridPlacement(HostKeyPanel, wide ? 1 : 4, wide ? 2 : 0, wide ? 2 : 4);
    }

    private void ConnectionExpander_SizeChanged(object sender, SizeChangedEventArgs args)
    {
        _ = sender;
        ConnectionScroller.Width = args.NewSize.Width;
        ConnectionGrid.Width = Math.Max(0, args.NewSize.Width - 24);
    }

    private static void SetGridPlacement(FrameworkElement element, int row, int column, int columnSpan)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        Grid.SetColumnSpan(element, columnSpan);
    }

    private void Page_Unloaded(object sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        if (!_loaded)
        {
            return;
        }
        _loaded = false;
        App.Current.MainWindow.AppWindow.Closing -= OnWindowClosing;
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
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
            SetStatus(exception.Message, InfoBarSeverity.Error);
            return;
        }

        SetConfigurationEnabled(false);
        ConnectButton.IsEnabled = false;
        ConnectButton.Label = "Connecting";
        SetStatus($"Connecting to {settings.Username}@{settings.Host}:{settings.Port}...", InfoBarSeverity.Informational);
        var session = new SshTerminalSession(_terminal);
        _session = session;
        session.Ended += OnSessionEnded;
        try
        {
            await _terminal.ResetAsync(_pageCancellation.Token);
            await _terminal.ClearAsync(_pageCancellation.Token);
            await session.ConnectAsync(settings, _pageCancellation.Token);
            ConnectIcon.Symbol = Symbol.Stop;
            ConnectButton.Label = "Disconnect";
            ConnectButton.IsEnabled = true;
            ConnectionExpander.IsExpanded = false;
            string verification = settings.AcceptAnyHostKey
                ? "host key verification skipped"
                : $"host key {session.ObservedHostKeyFingerprint}";
            SetStatus(
                $"Connected to {settings.Username}@{settings.Host}:{settings.Port} ({verification}).",
                InfoBarSeverity.Success);
            _ = TerminalView.Focus(FocusState.Programmatic);
        }
        catch (OperationCanceledException) when (_pageCancellation.IsCancellationRequested)
        {
            await DisposeFailedSessionAsync(session);
        }
        catch (HostKeyVerificationException exception)
        {
            await DisposeFailedSessionAsync(session);
            ConnectionExpander.IsExpanded = true;
            if (string.IsNullOrWhiteSpace(HostKeyTextBox.Text))
            {
                HostKeyTextBox.Text = exception.Fingerprint;
                _ = HostKeyTextBox.Focus(FocusState.Programmatic);
                SetStatus(
                    $"Host key not trusted. Verify {exception.Fingerprint} through a trusted channel, then connect again.",
                    InfoBarSeverity.Error);
            }
            else
            {
                AutomationProperties.SetHelpText(
                    HostKeyTextBox,
                    "The server host key does not match this fingerprint.");
                _ = HostKeyTextBox.Focus(FocusState.Programmatic);
                SetStatus(
                    $"Host key mismatch. Server: {exception.Fingerprint}; expected: {HostKeyTextBox.Text}.",
                    InfoBarSeverity.Error);
            }
        }
        catch (Exception exception)
        {
            await DisposeFailedSessionAsync(session);
            ConnectionExpander.IsExpanded = true;
            SetStatus($"Connection failed: {exception.Message}", InfoBarSeverity.Error);
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
        ConnectIcon.Symbol = Symbol.Play;
        ConnectButton.Label = "Connect";
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
        ConnectButton.Label = "Disconnecting";
        SetStatus("Disconnecting...", InfoBarSeverity.Informational);
        await session.DisposeAsync();
        SetConfigurationEnabled(true);
        ConnectIcon.Symbol = Symbol.Play;
        ConnectButton.Label = "Connect";
        ConnectButton.IsEnabled = true;
        ConnectionExpander.IsExpanded = true;
        SetStatus("Disconnected. Enter connection details and connect.", InfoBarSeverity.Informational);
    }

    private void OnSessionEnded(object? sender, SshSessionEndedEventArgs args)
    {
        if (sender is not SshTerminalSession session || _closing)
        {
            return;
        }
        _ = DispatcherQueue.TryEnqueue(() => _ = HandleSessionEndedAsync(session, args.Exception));
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
            exception is null ? InfoBarSeverity.Informational : InfoBarSeverity.Error);
    }

    private void AuthenticationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        _ = sender;
        _ = args;
        UpdateAuthenticationPanels();
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

    private async void BrowsePrivateKeyButton_Click(object sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            ViewMode = PickerViewMode.List
        };
        picker.FileTypeFilter.Add("*");
        nint windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);
        Windows.Storage.StorageFile? file = await picker.PickSingleFileAsync();
        if (file is not null)
        {
            PrivateKeyTextBox.Text = file.Path;
        }
    }

    private async void CopyButton_Click(object sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        try
        {
            await TerminalView.CopySelectionAsync(_pageCancellation.Token);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            SetStatus($"Copy failed: {exception.Message}", InfoBarSeverity.Error);
        }
    }

    private async void PasteButton_Click(object sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        try
        {
            await TerminalView.PasteAsync(cancellationToken: _pageCancellation.Token);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            SetStatus($"Paste failed: {exception.Message}", InfoBarSeverity.Error);
        }
    }

    private async void ClearButton_Click(object sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        try
        {
            await _terminal.ClearAsync(_pageCancellation.Token);
        }
        catch (OperationCanceledException) when (_pageCancellation.IsCancellationRequested)
        {
        }
    }

    private void TerminalView_SelectionChanged(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;
        CopyButton.IsEnabled = TerminalView.HasSelection;
    }

    private SshConnectionSettings ReadSettings()
    {
        ClearValidationErrors();
        string host = Required(HostTextBox, "Enter an SSH host.");
        int port = double.IsNaN(PortNumberBox.Value)
            ? throw InvalidSettings(PortNumberBox, "Enter a port from 1 through 65535.")
            : checked((int)PortNumberBox.Value);
        if (port is < 1 or > 65535)
        {
            throw InvalidSettings(PortNumberBox, "Enter a port from 1 through 65535.");
        }
        string username = Required(UsernameTextBox, "Enter an SSH username.");
        bool privateKey = AuthenticationComboBox.SelectedIndex == 1;
        string terminalType = Required(
            privateKey ? KeyTerminalTypeTextBox : PasswordTerminalTypeTextBox,
            "Enter a remote terminal type.");
        string privateKeyPath = string.Empty;
        if (privateKey)
        {
            privateKeyPath = ExpandHome(Required(PrivateKeyTextBox, "Select an SSH private key."));
            if (!File.Exists(privateKeyPath))
            {
                throw InvalidSettings(PrivateKeyTextBox, "Select an existing SSH private key file.");
            }
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
        return value.Length == 0 ? throw InvalidSettings(control, message) : value;
    }

    private InvalidOperationException InvalidSettings(Control control, string message)
    {
        AutomationProperties.SetHelpText(control, message);
        _ = control.Focus(FocusState.Programmatic);
        return new InvalidOperationException(message);
    }

    private void ClearValidationErrors()
    {
        Control[] controls =
        [
            HostTextBox,
            PortNumberBox,
            UsernameTextBox,
            PrivateKeyTextBox,
            PasswordTerminalTypeTextBox,
            KeyTerminalTypeTextBox,
            HostKeyTextBox
        ];
        foreach (Control control in controls)
        {
            AutomationProperties.SetHelpText(control, string.Empty);
        }
    }

    private void SetConfigurationEnabled(bool enabled)
    {
        HostTextBox.IsEnabled = enabled;
        PortNumberBox.IsEnabled = enabled;
        UsernameTextBox.IsEnabled = enabled;
        AuthenticationComboBox.IsEnabled = enabled;
        PasswordBox.IsEnabled = enabled;
        PasswordTerminalTypeTextBox.IsEnabled = enabled;
        PrivateKeyTextBox.IsEnabled = enabled;
        BrowsePrivateKeyButton.IsEnabled = enabled;
        PrivateKeyPassphraseBox.IsEnabled = enabled;
        KeyTerminalTypeTextBox.IsEnabled = enabled;
        HostKeyTextBox.IsEnabled = enabled;
        AcceptAnyHostKeyCheckBox.IsEnabled = enabled;
    }

    private void SetStatus(string message, InfoBarSeverity severity)
    {
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = severity;
        StatusInfoBar.IsOpen = true;
    }

    private void OnTerminalTitleChanged(object? sender, TerminalTitleChangedEventArgs args)
    {
        _ = sender;
        if (_closing)
        {
            return;
        }
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (!_closing)
            {
                App.Current.MainWindow.SetTerminalTitle(args.Title);
            }
        });
    }

    private void OnWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        _ = sender;
        if (_shutdownComplete)
        {
            return;
        }
        args.Cancel = true;
        if (!_closing)
        {
            _ = CloseAsync();
        }
    }

    private async Task CloseAsync()
    {
        _closing = true;
        IsEnabled = false;
        _pageCancellation.Cancel();
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
        _pageCancellation.Dispose();
        _shutdownComplete = true;
        App.Current.MainWindow.Close();
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

    private static int GetEnvironmentPort() =>
        int.TryParse(Environment.GetEnvironmentVariable("SSH_PORT"), out int port) && port is >= 1 and <= 65535
            ? port
            : 22;

    private static bool IsTrue(string? value) =>
        value is not null && (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase));
}

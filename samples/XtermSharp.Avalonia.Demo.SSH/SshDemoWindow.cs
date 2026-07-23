using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace XtermSharp.Avalonia.Demo.SSH;

internal sealed class SshDemoWindow : Window
{
    private const string PasswordAuthentication = "Password";
    private const string PrivateKeyAuthentication = "Private key";

    private readonly Terminal _terminal;
    private readonly TerminalView _terminalView;
    private readonly TextBox _hostText;
    private readonly TextBox _portText;
    private readonly TextBox _usernameText;
    private readonly ComboBox _authenticationCombo;
    private readonly TextBox _passwordText;
    private readonly TextBox _privateKeyText;
    private readonly Button _browsePrivateKeyButton;
    private readonly TextBox _privateKeyPassphraseText;
    private readonly TextBox _terminalTypeText;
    private readonly TextBox _hostKeyFingerprintText;
    private readonly CheckBox _acceptAnyHostKeyCheck;
    private readonly CheckBox _showRenderingDebugCheck;
    private readonly ComboBox _renderingModeCombo;
    private readonly Button _connectButton;
    private readonly TextBlock _statusText;
    private readonly List<Control> _configurationControls;
    private readonly CancellationTokenSource _windowCancellation = new();
    private SshTerminalSession? _session;
    private bool _closing;

    public SshDemoWindow()
    {
        Title = "XtermSharp Avalonia SSH Demo";
        Width = 1280;
        Height = 760;
        MinWidth = 900;
        MinHeight = 600;
        string? renderingDebugValue = Environment.GetEnvironmentVariable("XTERMSHARP_RENDERING_DEBUG");
        bool showRenderingDebug = renderingDebugValue is null || IsTrue(renderingDebugValue);

        _terminal = new Terminal(new TerminalOptions
        {
            Columns = 100,
            Rows = 30,
            Scrollback = 10_000,
            FontFamily = "Cascadia Mono, Menlo, DejaVu Sans Mono, monospace",
            FontSize = 15
        });
        _terminalView = new TerminalView
        {
            Terminal = _terminal,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Padding = new Thickness(8),
            ShowRenderingDebugOverlay = showRenderingDebug
        };

        _hostText = new TextBox { Text = GetEnvironmentValue("SSH_HOST", "localhost") };
        _portText = new TextBox { Text = GetEnvironmentValue("SSH_PORT", "22") };
        _usernameText = new TextBox { Text = GetEnvironmentValue("SSH_USER", Environment.UserName) };
        _authenticationCombo = new ComboBox
        {
            ItemsSource = new[] { PasswordAuthentication, PrivateKeyAuthentication },
            SelectedIndex = string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SSH_PRIVATE_KEY")) ? 0 : 1
        };
        _passwordText = new TextBox
        {
            Text = Environment.GetEnvironmentVariable("SSH_PASSWORD") ?? string.Empty,
            PasswordChar = '●'
        };
        _privateKeyText = new TextBox
        {
            Text = Environment.GetEnvironmentVariable("SSH_PRIVATE_KEY") ?? string.Empty,
            PlaceholderText = "~/.ssh/id_ed25519"
        };
        _browsePrivateKeyButton = new Button { Content = "Browse…" };
        _privateKeyPassphraseText = new TextBox
        {
            Text = Environment.GetEnvironmentVariable("SSH_PRIVATE_KEY_PASSPHRASE") ?? string.Empty,
            PasswordChar = '●'
        };
        _terminalTypeText = new TextBox
        {
            Text = GetEnvironmentValue("SSH_TERM", "xterm-256color")
        };
        _hostKeyFingerprintText = new TextBox
        {
            Text = Environment.GetEnvironmentVariable("SSH_HOST_KEY_SHA256") ?? string.Empty,
            PlaceholderText = "SHA256:…"
        };
        _acceptAnyHostKeyCheck = new CheckBox
        {
            Content = "Skip host key verification (test only)",
            IsChecked = IsTrue(Environment.GetEnvironmentVariable("SSH_ACCEPT_ANY_HOST_KEY")),
            VerticalAlignment = VerticalAlignment.Center
        };
        _showRenderingDebugCheck = new CheckBox
        {
            Content = "Show rendering debug overlay",
            IsChecked = showRenderingDebug,
            VerticalAlignment = VerticalAlignment.Center
        };
        _renderingModeCombo = new ComboBox
        {
            ItemsSource = Enum.GetValues<SkiaRenderModePreference>(),
            SelectedItem = SkiaRenderModePreference.Auto,
            MinWidth = 130
        };
        _connectButton = new Button
        {
            Content = "Connect",
            MinWidth = 110,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _statusText = new TextBlock
        {
            Text = "Disconnected. Enter a trusted SHA-256 host key or verify the fingerprint shown after the first attempt.",
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.DimGray
        };

        _configurationControls =
        [
            _hostText,
            _portText,
            _usernameText,
            _authenticationCombo,
            _passwordText,
            _privateKeyText,
            _browsePrivateKeyButton,
            _privateKeyPassphraseText,
            _terminalTypeText,
            _hostKeyFingerprintText,
            _acceptAnyHostKeyCheck
        ];

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*")
        };
        root.Children.Add(CreateConfigurationPanel());
        Grid.SetRow(_terminalView, 1);
        root.Children.Add(_terminalView);
        Content = root;

        _authenticationCombo.SelectionChanged += (_, _) => UpdateAuthenticationControls();
        _browsePrivateKeyButton.Click += async (_, _) => await BrowsePrivateKeyAsync();
        _connectButton.Click += async (_, _) => await ToggleConnectionAsync();
        _showRenderingDebugCheck.PropertyChanged += (_, args) =>
        {
            if (args.Property == ToggleButton.IsCheckedProperty)
            {
                _terminalView.ShowRenderingDebugOverlay = _showRenderingDebugCheck.IsChecked == true;
            }
        };
        _renderingModeCombo.SelectionChanged += (_, _) =>
        {
            if (_renderingModeCombo.SelectedItem is SkiaRenderModePreference mode)
            {
                _terminalView.RequestedRenderMode = mode;
            }
        };
        _terminal.TitleChanged += OnTerminalTitleChanged;
        Opened += (_, _) => _terminalView.Focus();
        Closed += async (_, _) => await CloseAsync();
        UpdateAuthenticationControls();
    }

    private Control CreateConfigurationPanel()
    {
        var primaryFields = new WrapPanel();
        primaryFields.Children.Add(CreateField("Host", _hostText, 230));
        primaryFields.Children.Add(CreateField("Port", _portText, 90));
        primaryFields.Children.Add(CreateField("Username", _usernameText, 180));
        primaryFields.Children.Add(CreateField("Authentication", _authenticationCombo, 160));
        primaryFields.Children.Add(CreateField("Terminal type", _terminalTypeText, 180));
        primaryFields.Children.Add(CreateField("Rendering", _renderingModeCombo, 130));

        var keyPicker = new Grid
        {
            Width = 430,
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };
        keyPicker.Children.Add(_privateKeyText);
        Grid.SetColumn(_browsePrivateKeyButton, 1);
        _browsePrivateKeyButton.Margin = new Thickness(8, 0, 0, 0);
        keyPicker.Children.Add(_browsePrivateKeyButton);

        var authenticationFields = new WrapPanel();
        authenticationFields.Children.Add(CreateField("Password", _passwordText, 220));
        authenticationFields.Children.Add(CreateField("Private key", keyPicker));
        authenticationFields.Children.Add(CreateField("Key passphrase", _privateKeyPassphraseText, 220));

        var verificationFields = new WrapPanel();
        verificationFields.Children.Add(CreateField("Host key SHA-256", _hostKeyFingerprintText, 430));
        var checkContainer = new StackPanel
        {
            Margin = new Thickness(0, 22, 12, 8),
            Spacing = 6,
            Children = { _acceptAnyHostKeyCheck, _showRenderingDebugCheck }
        };
        verificationFields.Children.Add(checkContainer);

        var actionRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*")
        };
        actionRow.Children.Add(_connectButton);
        Grid.SetColumn(_statusText, 1);
        _statusText.Margin = new Thickness(12, 0, 0, 0);
        actionRow.Children.Add(_statusText);

        var content = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock
                {
                    Text = "SSH connection",
                    FontSize = 18,
                    FontWeight = FontWeight.SemiBold
                },
                primaryFields,
                authenticationFields,
                verificationFields,
                actionRow
            }
        };

        return new Border
        {
            Padding = new Thickness(12),
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = content
        };
    }

    private static Control CreateField(string label, Control control, double? width = null)
    {
        if (width is not null)
        {
            control.Width = width.Value;
        }

        return new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(0, 4, 12, 8),
            Children =
            {
                new TextBlock { Text = label },
                control
            }
        };
    }

    private async Task ToggleConnectionAsync()
    {
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
        catch (Exception exception)
        {
            SetStatus(exception.Message, isError: true);
            return;
        }

        SetConfigurationEnabled(false);
        _connectButton.IsEnabled = false;
        _connectButton.Content = "Connecting…";
        SetStatus($"Connecting to {settings.Username}@{settings.Host}:{settings.Port}…");

        var session = new SshTerminalSession(_terminal);
        _session = session;
        session.Ended += OnSessionEnded;
        try
        {
            await _terminal.ResetAsync(_windowCancellation.Token);
            await _terminal.ClearAsync(_windowCancellation.Token);
            await session.ConnectAsync(settings, _windowCancellation.Token);
            _connectButton.Content = "Disconnect";
            _connectButton.IsEnabled = true;
            string verification = settings.AcceptAnyHostKey
                ? "host key verification skipped"
                : $"host key {session.ObservedHostKeyFingerprint}";
            SetStatus($"Connected to {settings.Username}@{settings.Host}:{settings.Port} ({verification}).", isConnected: true);
            _terminalView.Focus();
        }
        catch (OperationCanceledException) when (_windowCancellation.IsCancellationRequested)
        {
            await DisposeFailedSessionAsync(session);
        }
        catch (HostKeyVerificationException exception)
        {
            await DisposeFailedSessionAsync(session);
            if (string.IsNullOrWhiteSpace(_hostKeyFingerprintText.Text))
            {
                _hostKeyFingerprintText.Text = exception.Fingerprint;
                SetStatus(
                    $"Host key not trusted. The server reported {exception.Fingerprint}. Verify it out of band, then click Connect again.",
                    isError: true);
            }
            else
            {
                SetStatus(
                    $"Host key mismatch. The server reported {exception.Fingerprint}; expected {_hostKeyFingerprintText.Text}.",
                    isError: true);
            }
        }
        catch (Exception exception)
        {
            await DisposeFailedSessionAsync(session);
            SetStatus($"Connection failed: {exception.Message}", isError: true);
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
        _connectButton.Content = "Connect";
        _connectButton.IsEnabled = true;
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
        _connectButton.IsEnabled = false;
        _connectButton.Content = "Disconnecting…";
        SetStatus("Disconnecting…");
        await session.DisposeAsync();
        SetConfigurationEnabled(true);
        _connectButton.Content = "Connect";
        _connectButton.IsEnabled = true;
        SetStatus("Disconnected.");
    }

    private void OnSessionEnded(object? sender, SshSessionEndedEventArgs args)
    {
        if (sender is not SshTerminalSession session)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => _ = HandleSessionEndedAsync(session, args.Exception));
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
            isError: exception is not null);
    }

    private async Task BrowsePrivateKeyAsync()
    {
        IStorageProvider? storageProvider = StorageProvider;
        if (storageProvider is null)
        {
            return;
        }

        IReadOnlyList<IStorageFile> files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select an SSH private key",
            AllowMultiple = false
        });
        if (files.Count != 0)
        {
            _privateKeyText.Text = files[0].TryGetLocalPath() ?? files[0].Name;
        }
    }

    private SshConnectionSettings ReadSettings()
    {
        string host = Required(_hostText.Text, "Host is required.");
        if (!int.TryParse(_portText.Text, out int port) || port is < 1 or > 65_535)
        {
            throw new InvalidOperationException("Port must be between 1 and 65535.");
        }

        string username = Required(_usernameText.Text, "Username is required.");
        string terminalType = Required(_terminalTypeText.Text, "Terminal type is required.");
        SshAuthenticationKind authentication = _authenticationCombo.SelectedIndex == 1
            ? SshAuthenticationKind.PrivateKey
            : SshAuthenticationKind.Password;
        string privateKeyPath = ExpandHome(_privateKeyText.Text?.Trim() ?? string.Empty);
        if (authentication == SshAuthenticationKind.PrivateKey && !File.Exists(privateKeyPath))
        {
            throw new InvalidOperationException("Select an existing SSH private key file.");
        }

        return new SshConnectionSettings(
            host,
            port,
            username,
            authentication,
            _passwordText.Text ?? string.Empty,
            privateKeyPath,
            _privateKeyPassphraseText.Text ?? string.Empty,
            terminalType,
            _hostKeyFingerprintText.Text?.Trim() ?? string.Empty,
            _acceptAnyHostKeyCheck.IsChecked == true);
    }

    private void UpdateAuthenticationControls()
    {
        if (_session is not null)
        {
            return;
        }

        bool privateKey = _authenticationCombo.SelectedIndex == 1;
        _passwordText.IsEnabled = !privateKey;
        _privateKeyText.IsEnabled = privateKey;
        _browsePrivateKeyButton.IsEnabled = privateKey;
        _privateKeyPassphraseText.IsEnabled = privateKey;
    }

    private void SetConfigurationEnabled(bool enabled)
    {
        foreach (Control control in _configurationControls)
        {
            control.IsEnabled = enabled;
        }

        if (enabled)
        {
            UpdateAuthenticationControls();
        }
    }

    private void SetStatus(string message, bool isError = false, bool isConnected = false)
    {
        _statusText.Text = message;
        _statusText.Foreground = isError
            ? Brushes.OrangeRed
            : isConnected
                ? Brushes.ForestGreen
                : Brushes.DimGray;
    }

    private void OnTerminalTitleChanged(object? sender, TerminalTitleChangedEventArgs args)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!_closing)
            {
                Title = string.IsNullOrWhiteSpace(args.Title)
                    ? "XtermSharp Avalonia SSH Demo"
                    : $"{args.Title} — XtermSharp SSH";
            }
        });
    }

    private async Task CloseAsync()
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        _windowCancellation.Cancel();
        _terminal.TitleChanged -= OnTerminalTitleChanged;
        SshTerminalSession? session = _session;
        _session = null;
        if (session is not null)
        {
            session.Ended -= OnSessionEnded;
            await session.DisposeAsync();
        }
        await _terminal.DisposeAsync();
        _windowCancellation.Dispose();
    }

    private static string Required(string? value, string message)
    {
        string result = value?.Trim() ?? string.Empty;
        return result.Length == 0 ? throw new InvalidOperationException(message) : result;
    }

    private static string ExpandHome(string path)
    {
        if (path == "~")
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        if (path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                path[2..]);
        }
        return path;
    }

    private static string GetEnvironmentValue(string name, string fallback) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : fallback;

    private static bool IsTrue(string? value) =>
        value is not null &&
        (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase));
}

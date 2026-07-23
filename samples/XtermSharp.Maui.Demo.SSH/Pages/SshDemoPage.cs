using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Layouts;
using Microsoft.Maui.Storage;

namespace XtermSharp.Maui.Demo.SSH.Pages;

internal sealed class SshDemoPage : ContentPage, IAsyncDisposable
{
    private const string PasswordAuthentication = "Password";
    private const string PrivateKeyAuthentication = "Private key";

    private readonly Terminal _terminal;
    private readonly TerminalView _terminalView;
    private readonly Entry _hostText;
    private readonly Entry _portText;
    private readonly Entry _usernameText;
    private readonly Picker _authenticationPicker;
    private readonly Entry _passwordText;
    private readonly Entry _privateKeyText;
    private readonly Button _browsePrivateKeyButton;
    private readonly Entry _privateKeyPassphraseText;
    private readonly Entry _terminalTypeText;
    private readonly Entry _hostKeyFingerprintText;
    private readonly CheckBox _acceptAnyHostKeyCheck;
    private readonly Button _connectButton;
    private readonly Button _settingsButton;
    private readonly Label _statusText;
    private readonly VisualElement _configurationPanel;
    private readonly List<VisualElement> _configurationControls;
    private readonly CancellationTokenSource _pageCancellation = new();
    private SshTerminalSession? _session;
    private int _disposed;

    public SshDemoPage()
    {
        Title = "XtermSharp MAUI SSH";
        BackgroundColor = Color.FromArgb("#101214");

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
            Padding = new Thickness(8),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            ShowRenderingDebugOverlay = true
        };

        _hostText = EntryValue(GetEnvironmentValue("SSH_HOST", "localhost"));
        _portText = EntryValue(GetEnvironmentValue("SSH_PORT", "22"), Keyboard.Numeric);
        _usernameText = EntryValue(GetEnvironmentValue("SSH_USER", Environment.UserName));
        _authenticationPicker = new Picker
        {
            ItemsSource = new[] { PasswordAuthentication, PrivateKeyAuthentication },
            SelectedIndex = string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SSH_PRIVATE_KEY")) ? 0 : 1
        };
        _passwordText = EntryValue(Environment.GetEnvironmentVariable("SSH_PASSWORD") ?? string.Empty);
        _passwordText.IsPassword = true;
        _privateKeyText = EntryValue(Environment.GetEnvironmentVariable("SSH_PRIVATE_KEY") ?? string.Empty);
        _privateKeyText.Placeholder = "~/.ssh/id_ed25519";
        _browsePrivateKeyButton = new Button { Text = "Browse" };
        _privateKeyPassphraseText = EntryValue(
            Environment.GetEnvironmentVariable("SSH_PRIVATE_KEY_PASSPHRASE") ?? string.Empty);
        _privateKeyPassphraseText.IsPassword = true;
        _terminalTypeText = EntryValue(GetEnvironmentValue("SSH_TERM", "xterm-256color"));
        _hostKeyFingerprintText = EntryValue(
            Environment.GetEnvironmentVariable("SSH_HOST_KEY_SHA256") ?? string.Empty);
        _hostKeyFingerprintText.Placeholder = "SHA256:...";
        _acceptAnyHostKeyCheck = new CheckBox
        {
            IsChecked = IsTrue(Environment.GetEnvironmentVariable("SSH_ACCEPT_ANY_HOST_KEY"))
        };
        _connectButton = new Button
        {
            Text = "Connect",
            MinimumWidthRequest = 110
        };
        _settingsButton = new Button
        {
            Text = "Settings",
            IsVisible = false
        };
        _statusText = new Label
        {
            Text = "Disconnected",
            TextColor = Colors.Gray,
            VerticalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        };

        _configurationControls =
        [
            _hostText,
            _portText,
            _usernameText,
            _authenticationPicker,
            _passwordText,
            _privateKeyText,
            _browsePrivateKeyButton,
            _privateKeyPassphraseText,
            _terminalTypeText,
            _hostKeyFingerprintText,
            _acceptAnyHostKeyCheck
        ];
        _configurationPanel = CreateConfigurationPanel();
        VisualElement keyBar = CreateKeyBar();

        var header = new Grid
        {
            Padding = new Thickness(10, 6),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            BackgroundColor = Color.FromArgb("#1B1F23")
        };
        header.Add(_connectButton);
        header.Add(_statusText, 1);
        header.Add(_settingsButton, 2);
        _statusText.Margin = new Thickness(12, 0);

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            }
        };
        root.Add(header);
        root.Add(_configurationPanel, 0, 1);
        root.Add(keyBar, 0, 2);
        root.Add(_terminalView, 0, 3);
        Content = root;

        _authenticationPicker.SelectedIndexChanged += (_, _) => UpdateAuthenticationControls();
        _browsePrivateKeyButton.Clicked += async (_, _) => await BrowsePrivateKeyAsync();
        _connectButton.Clicked += async (_, _) => await ToggleConnectionAsync();
        _settingsButton.Clicked += (_, _) =>
        {
            _configurationPanel.IsVisible = !_configurationPanel.IsVisible;
            _settingsButton.Text = _configurationPanel.IsVisible ? "Hide" : "Settings";
        };
        _terminal.TitleChanged += OnTerminalTitleChanged;
        Appearing += (_, _) => _terminalView.Focus();
        UpdateAuthenticationControls();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }
        _pageCancellation.Cancel();
        _terminal.TitleChanged -= OnTerminalTitleChanged;
        SshTerminalSession? session = _session;
        _session = null;
        if (session is not null)
        {
            session.Ended -= OnSessionEnded;
            await session.DisposeAsync();
        }
        await _terminal.DisposeAsync();
        _pageCancellation.Dispose();
    }

    private VisualElement CreateConfigurationPanel()
    {
        var fields = new FlexLayout
        {
            Direction = FlexDirection.Row,
            Wrap = FlexWrap.Wrap,
            AlignItems = FlexAlignItems.End
        };
        fields.Add(CreateField("Host", _hostText, 220));
        fields.Add(CreateField("Port", _portText, 90));
        fields.Add(CreateField("Username", _usernameText, 180));
        fields.Add(CreateField("Authentication", _authenticationPicker, 160));
        fields.Add(CreateField("Password", _passwordText, 210));
        fields.Add(CreateField("Private key", _privateKeyText, 330));
        fields.Add(CreateField(string.Empty, _browsePrivateKeyButton, 100));
        fields.Add(CreateField("Key passphrase", _privateKeyPassphraseText, 210));
        fields.Add(CreateField("Terminal type", _terminalTypeText, 180));
        fields.Add(CreateField("Host key SHA-256", _hostKeyFingerprintText, 360));

        var verification = new HorizontalStackLayout
        {
            Spacing = 8,
            Margin = new Thickness(4, 4, 12, 8),
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                _acceptAnyHostKeyCheck,
                new Label
                {
                    Text = "Skip host key verification (test only)",
                    VerticalTextAlignment = TextAlignment.Center
                }
            }
        };
        fields.Add(verification);

        return new ScrollView
        {
            MaximumHeightRequest = 310,
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(12, 8),
                Children =
                {
                    new Label
                    {
                        Text = "SSH connection",
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold
                    },
                    fields
                }
            }
        };
    }

    private VisualElement CreateKeyBar()
    {
        var keys = new HorizontalStackLayout
        {
            Spacing = 4,
            Padding = new Thickness(8, 4),
            Children =
            {
                KeyButton("Esc", new TerminalKeyEvent("Escape", "Escape", 27)),
                KeyButton("Tab", new TerminalKeyEvent("Tab", "Tab", 9)),
                KeyButton("Up", new TerminalKeyEvent("ArrowUp", "ArrowUp", 38)),
                KeyButton("Down", new TerminalKeyEvent("ArrowDown", "ArrowDown", 40)),
                KeyButton("Left", new TerminalKeyEvent("ArrowLeft", "ArrowLeft", 37)),
                KeyButton("Right", new TerminalKeyEvent("ArrowRight", "ArrowRight", 39)),
                KeyButton("PgUp", new TerminalKeyEvent("PageUp", "PageUp", 33)),
                KeyButton("PgDn", new TerminalKeyEvent("PageDown", "PageDown", 34)),
                KeyButton(
                    "Ctrl+C",
                    new TerminalKeyEvent("c", "KeyC", 67, TerminalModifiers.Control)),
                KeyButton(
                    "Ctrl+D",
                    new TerminalKeyEvent("d", "KeyD", 68, TerminalModifiers.Control))
            }
        };
        return new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            BackgroundColor = Color.FromArgb("#171A1D"),
            Content = keys
        };
    }

    private Button KeyButton(string text, TerminalKeyEvent key)
    {
        var button = new Button
        {
            Text = text,
            FontSize = 12,
            Padding = new Thickness(10, 5),
            MinimumHeightRequest = 34
        };
        button.Clicked += async (_, _) => await _terminalView.SendKeyAsync(key);
        return button;
    }

    private static VerticalStackLayout CreateField(string label, View control, double width)
    {
        control.WidthRequest = width;
        var field = new VerticalStackLayout
        {
            Spacing = 3,
            Margin = new Thickness(4, 2, 8, 6),
            Children = { control }
        };
        if (label.Length != 0)
        {
            field.Children.Insert(0, new Label { Text = label, FontSize = 12 });
        }
        return field;
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
        _connectButton.Text = "Connecting...";
        SetStatus($"Connecting to {settings.Username}@{settings.Host}:{settings.Port}...");

        var session = new SshTerminalSession(_terminal);
        _session = session;
        session.Ended += OnSessionEnded;
        try
        {
            await _terminal.ResetAsync(_pageCancellation.Token);
            await _terminal.ClearAsync(_pageCancellation.Token);
            await session.ConnectAsync(settings, _pageCancellation.Token);
            _connectButton.Text = "Disconnect";
            _connectButton.IsEnabled = true;
            _configurationPanel.IsVisible = false;
            _settingsButton.IsVisible = true;
            _settingsButton.Text = "Settings";
            string verification = settings.AcceptAnyHostKey
                ? "host key verification skipped"
                : $"host key {session.ObservedHostKeyFingerprint}";
            SetStatus(
                $"Connected to {settings.Username}@{settings.Host}:{settings.Port} ({verification})",
                isConnected: true);
            _terminalView.Focus();
        }
        catch (OperationCanceledException) when (_pageCancellation.IsCancellationRequested)
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
                    $"Host key not trusted. Verify {exception.Fingerprint}, then connect again.",
                    isError: true);
            }
            else
            {
                SetStatus(
                    $"Host key mismatch: server {exception.Fingerprint}; expected {_hostKeyFingerprintText.Text}.",
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
        _configurationPanel.IsVisible = true;
        _settingsButton.IsVisible = false;
        _connectButton.Text = "Connect";
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
        _connectButton.Text = "Disconnecting...";
        SetStatus("Disconnecting...");
        await session.DisposeAsync();
        SetConfigurationEnabled(true);
        _configurationPanel.IsVisible = true;
        _settingsButton.IsVisible = false;
        _connectButton.Text = "Connect";
        _connectButton.IsEnabled = true;
        SetStatus("Disconnected");
    }

    private void OnSessionEnded(object? sender, SshSessionEndedEventArgs args)
    {
        if (sender is SshTerminalSession session)
        {
            Dispatcher.Dispatch(() => _ = HandleSessionEndedAsync(session, args.Exception));
        }
    }

    private async Task HandleSessionEndedAsync(SshTerminalSession session, Exception? exception)
    {
        if (Volatile.Read(ref _disposed) != 0 || !ReferenceEquals(_session, session))
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
        FileResult? file = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Select an SSH private key"
        });
        if (file is not null)
        {
            _privateKeyText.Text = file.FullPath;
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
        SshAuthenticationKind authentication = _authenticationPicker.SelectedIndex == 1
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
            _acceptAnyHostKeyCheck.IsChecked);
    }

    private void UpdateAuthenticationControls()
    {
        if (_session is not null)
        {
            return;
        }
        bool privateKey = _authenticationPicker.SelectedIndex == 1;
        _passwordText.IsEnabled = !privateKey;
        _privateKeyText.IsEnabled = privateKey;
        _browsePrivateKeyButton.IsEnabled = privateKey;
        _privateKeyPassphraseText.IsEnabled = privateKey;
    }

    private void SetConfigurationEnabled(bool enabled)
    {
        foreach (VisualElement control in _configurationControls)
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
        _statusText.TextColor = isError
            ? Colors.OrangeRed
            : isConnected
                ? Colors.LimeGreen
                : Colors.Gray;
    }

    private void OnTerminalTitleChanged(object? sender, TerminalTitleChangedEventArgs args)
    {
        Dispatcher.Dispatch(() =>
        {
            if (Volatile.Read(ref _disposed) == 0)
            {
                Title = string.IsNullOrWhiteSpace(args.Title)
                    ? "XtermSharp MAUI SSH"
                    : $"{args.Title} - XtermSharp SSH";
            }
        });
    }

    private static Entry EntryValue(string value, Keyboard? keyboard = null) => new()
    {
        Text = value,
        Keyboard = keyboard ?? Keyboard.Default
    };

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

namespace XtermSharp.WinForms.Demo.SSH;

internal sealed class SshDemoForm : Form
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
    private readonly Button _connectButton;
    private readonly ToolStripStatusLabel _statusText;
    private readonly ErrorProvider _errors;
    private readonly Control[] _configurationControls;
    private readonly CancellationTokenSource _windowCancellation = new();
    private SshTerminalSession? _session;
    private bool _closing;
    private bool _shutdownComplete;

    public SshDemoForm()
    {
        Text = "XtermSharp WinForms SSH Demo";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1280, 760);
        MinimumSize = new Size(960, 680);
        AutoScaleMode = AutoScaleMode.Dpi;
        KeyPreview = false;

        _terminal = new Terminal(new TerminalOptions
        {
            Columns = 100,
            Rows = 30,
            Scrollback = 10_000,
            FontFamily = "Cascadia Mono, Consolas, DejaVu Sans Mono, monospace",
            FontSize = 15,
            UnicodeVersion = UnicodeV15Provider.GraphemeVersionName
        });
        _terminalView = new TerminalView
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
            Terminal = _terminal,
            AccessibleName = "SSH terminal session",
            ShowRenderingDebugOverlay = true,
            EnableGpuRendering = true
        };

        _hostText = CreateTextBox(GetEnvironmentValue("SSH_HOST", "localhost"));
        _portText = CreateTextBox(GetEnvironmentValue("SSH_PORT", "22"));
        _usernameText = CreateTextBox(GetEnvironmentValue("SSH_USER", Environment.UserName));
        _authenticationCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            IntegralHeight = true
        };
        _authenticationCombo.Items.AddRange([PasswordAuthentication, PrivateKeyAuthentication]);
        _authenticationCombo.SelectedIndex = string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable("SSH_PRIVATE_KEY")) ? 0 : 1;
        _passwordText = CreateTextBox(Environment.GetEnvironmentVariable("SSH_PASSWORD") ?? string.Empty);
        _passwordText.UseSystemPasswordChar = true;
        _privateKeyText = CreateTextBox(Environment.GetEnvironmentVariable("SSH_PRIVATE_KEY") ?? string.Empty);
        _browsePrivateKeyButton = new Button
        {
            Text = "Browse...",
            AutoSize = true,
            MinimumSize = new Size(84, 30),
            AccessibleName = "Browse for SSH private key"
        };
        _privateKeyPassphraseText = CreateTextBox(
            Environment.GetEnvironmentVariable("SSH_PRIVATE_KEY_PASSPHRASE") ?? string.Empty);
        _privateKeyPassphraseText.UseSystemPasswordChar = true;
        _terminalTypeText = CreateTextBox(GetEnvironmentValue("SSH_TERM", "xterm-256color"));
        _hostKeyFingerprintText = CreateTextBox(
            Environment.GetEnvironmentVariable("SSH_HOST_KEY_SHA256") ?? string.Empty);
        _acceptAnyHostKeyCheck = new CheckBox
        {
            Text = "Skip host key verification (test only)",
            AutoSize = true,
            Checked = IsTrue(Environment.GetEnvironmentVariable("SSH_ACCEPT_ANY_HOST_KEY")),
            Padding = new Padding(0, 5, 0, 0)
        };
        _connectButton = new Button
        {
            Text = "Connect",
            AutoSize = true,
            MinimumSize = new Size(112, 34)
        };
        _statusText = new ToolStripStatusLabel
        {
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "Disconnected."
        };
        _errors = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };
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

        Controls.Add(CreateLayout());
        _errors.ContainerControl = this;
        _authenticationCombo.SelectedIndexChanged += (_, _) => UpdateAuthenticationControls();
        _browsePrivateKeyButton.Click += (_, _) => BrowsePrivateKey();
        _connectButton.Click += async (_, _) => await ToggleConnectionAsync();
        _terminal.TitleChanged += OnTerminalTitleChanged;
        Shown += (_, _) => _hostText.Focus();
        FormClosing += OnFormClosing;
        UpdateAuthenticationControls();
        SetStatus("Disconnected. Enter connection details and connect.");
    }

    private Control CreateLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        Control configuration = CreateConfigurationPanel();
        root.Controls.Add(configuration, 0, 0);
        root.Controls.Add(_terminalView, 0, 1);
        var status = new StatusStrip
        {
            SizingGrip = false,
            AccessibleName = "Connection status"
        };
        status.Items.Add(_statusText);
        root.Controls.Add(status, 0, 2);
        return root;
    }

    private Control CreateConfigurationPanel()
    {
        var rows = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Padding = new Padding(12, 8, 4, 8),
            BackColor = SystemColors.ControlLight
        };
        rows.Controls.Add(CreateField("Host", _hostText, 210));
        rows.Controls.Add(CreateField("Port", _portText, 72));
        rows.Controls.Add(CreateField("Username", _usernameText, 160));
        rows.Controls.Add(CreateField("Authentication", _authenticationCombo, 140));
        rows.Controls.Add(CreateField("Terminal type", _terminalTypeText, 150));
        rows.Controls.Add(CreateField("Password", _passwordText, 190));

        var keyPicker = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 1,
            Width = 350,
            Height = 31,
            Margin = Padding.Empty
        };
        keyPicker.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        keyPicker.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _privateKeyText.Dock = DockStyle.Fill;
        _browsePrivateKeyButton.Margin = new Padding(6, 0, 0, 0);
        keyPicker.Controls.Add(_privateKeyText, 0, 0);
        keyPicker.Controls.Add(_browsePrivateKeyButton, 1, 0);
        rows.Controls.Add(CreateField("Private key", keyPicker, 350));
        rows.Controls.Add(CreateField("Key passphrase", _privateKeyPassphraseText, 190));
        rows.Controls.Add(CreateField("Host key SHA-256", _hostKeyFingerprintText, 350));

        var verification = new Panel
        {
            AutoSize = true,
            Margin = new Padding(4, 22, 12, 0),
            Padding = new Padding(0, 0, 0, 0)
        };
        verification.Controls.Add(_acceptAnyHostKeyCheck);
        rows.Controls.Add(verification);

        var action = new Panel
        {
            AutoSize = true,
            Margin = new Padding(0, 20, 8, 0)
        };
        action.Controls.Add(_connectButton);
        rows.Controls.Add(action);
        return rows;
    }

    private static Control CreateField(string label, Control control, int width)
    {
        control.Width = width;
        control.Margin = Padding.Empty;
        var field = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 0, 12, 4),
            Padding = Padding.Empty
        };
        field.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        field.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        field.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4)
        }, 0, 0);
        field.Controls.Add(control, 0, 1);
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
        catch (InvalidOperationException exception)
        {
            SetStatus(exception.Message, isError: true);
            return;
        }

        SetConfigurationEnabled(false);
        _connectButton.Enabled = false;
        _connectButton.Text = "Connecting...";
        SetStatus($"Connecting to {settings.Username}@{settings.Host}:{settings.Port}...");
        var session = new SshTerminalSession(_terminal);
        _session = session;
        session.Ended += OnSessionEnded;
        try
        {
            await _terminal.ResetAsync(_windowCancellation.Token);
            await _terminal.ClearAsync(_windowCancellation.Token);
            await session.ConnectAsync(settings, _windowCancellation.Token);
            _connectButton.Text = "Disconnect";
            _connectButton.Enabled = true;
            string verification = settings.AcceptAnyHostKey
                ? "host key verification skipped"
                : $"host key {session.ObservedHostKeyFingerprint}";
            SetStatus(
                $"Connected to {settings.Username}@{settings.Host}:{settings.Port} ({verification}).",
                isConnected: true);
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
                _hostKeyFingerprintText.Focus();
                SetStatus(
                    $"Host key not trusted. Verify {exception.Fingerprint} through a trusted channel, then connect again.",
                    isError: true);
            }
            else
            {
                _errors.SetError(_hostKeyFingerprintText, "The server host key does not match this fingerprint.");
                _hostKeyFingerprintText.Focus();
                SetStatus(
                    $"Host key mismatch. Server: {exception.Fingerprint}; expected: {_hostKeyFingerprintText.Text}.",
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
        _connectButton.Text = "Connect";
        _connectButton.Enabled = true;
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
        _connectButton.Enabled = false;
        _connectButton.Text = "Disconnecting...";
        SetStatus("Disconnecting...");
        await session.DisposeAsync();
        SetConfigurationEnabled(true);
        _connectButton.Text = "Connect";
        _connectButton.Enabled = true;
        SetStatus("Disconnected.");
    }

    private void OnSessionEnded(object? sender, SshSessionEndedEventArgs args)
    {
        if (sender is not SshTerminalSession session || IsDisposed || !IsHandleCreated)
        {
            return;
        }
        BeginInvoke(() => _ = HandleSessionEndedAsync(session, args.Exception));
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

    private void BrowsePrivateKey()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select an SSH private key",
            CheckFileExists = true,
            Multiselect = false,
            Filter = "All files (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _privateKeyText.Text = dialog.FileName;
        }
    }

    private SshConnectionSettings ReadSettings()
    {
        _errors.Clear();
        string host = Required(_hostText, "Host is required.");
        if (!int.TryParse(_portText.Text, out int port) || port is < 1 or > 65_535)
        {
            return InvalidSettings<SshConnectionSettings>(_portText, "Port must be between 1 and 65535.");
        }
        string username = Required(_usernameText, "Username is required.");
        string terminalType = Required(_terminalTypeText, "Terminal type is required.");
        SshAuthenticationKind authentication = _authenticationCombo.SelectedIndex == 1
            ? SshAuthenticationKind.PrivateKey
            : SshAuthenticationKind.Password;
        string privateKeyPath = ExpandHome(_privateKeyText.Text.Trim());
        if (authentication == SshAuthenticationKind.PrivateKey && !File.Exists(privateKeyPath))
        {
            return InvalidSettings<SshConnectionSettings>(
                _privateKeyText,
                "Select an existing SSH private key file.");
        }
        return new SshConnectionSettings(
            host,
            port,
            username,
            authentication,
            _passwordText.Text,
            privateKeyPath,
            _privateKeyPassphraseText.Text,
            terminalType,
            _hostKeyFingerprintText.Text.Trim(),
            _acceptAnyHostKeyCheck.Checked);
    }

    private string Required(TextBox control, string message)
    {
        string value = control.Text.Trim();
        return value.Length == 0 ? InvalidSettings<string>(control, message) : value;
    }

    private T InvalidSettings<T>(Control control, string message)
    {
        _errors.SetError(control, message);
        control.Focus();
        throw new InvalidOperationException(message);
    }

    private void UpdateAuthenticationControls()
    {
        if (_session is not null)
        {
            return;
        }
        bool privateKey = _authenticationCombo.SelectedIndex == 1;
        _passwordText.Enabled = !privateKey;
        _privateKeyText.Enabled = privateKey;
        _browsePrivateKeyButton.Enabled = privateKey;
        _privateKeyPassphraseText.Enabled = privateKey;
    }

    private void SetConfigurationEnabled(bool enabled)
    {
        foreach (Control control in _configurationControls)
        {
            control.Enabled = enabled;
        }
        if (enabled)
        {
            UpdateAuthenticationControls();
        }
    }

    private void SetStatus(string message, bool isError = false, bool isConnected = false)
    {
        _statusText.Text = message;
        _statusText.ForeColor = isError
            ? Color.Firebrick
            : isConnected
                ? Color.DarkGreen
                : SystemColors.ControlText;
    }

    private void OnTerminalTitleChanged(object? sender, TerminalTitleChangedEventArgs args)
    {
        if (_closing || IsDisposed || !IsHandleCreated)
        {
            return;
        }
        BeginInvoke(() =>
        {
            if (!_closing)
            {
                Text = string.IsNullOrWhiteSpace(args.Title)
                    ? "XtermSharp WinForms SSH Demo"
                    : $"{args.Title} - XtermSharp SSH";
            }
        });
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
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
        Enabled = false;
        _windowCancellation.Cancel();
        _terminal.TitleChanged -= OnTerminalTitleChanged;
        SshTerminalSession? session = _session;
        _session = null;
        if (session is not null)
        {
            session.Ended -= OnSessionEnded;
            await session.DisposeAsync();
        }
        _terminalView.Terminal = null;
        await _terminal.DisposeAsync();
        _windowCancellation.Dispose();
        _shutdownComplete = true;
        Close();
    }

    private static TextBox CreateTextBox(string text) => new()
    {
        Text = text,
        BorderStyle = BorderStyle.FixedSingle,
        MinimumSize = new Size(0, 28)
    };

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
}

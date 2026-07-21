namespace XtermSharp.Addons.Progress;

/// <summary>Tracks ConEmu OSC 9;4 progress sequences.</summary>
public sealed class ProgressAddon : ITerminalAddon
{
    private const int OscIdentifier = 9;
    private readonly object _gate = new();
    private ProgressState _progress;
    private Terminal? _terminal;
    private IDisposable? _registration;
    private bool _active;
    private bool _disposed;

    /// <summary>Raised whenever a valid sequence or programmatic update changes the tracked state.</summary>
    public event EventHandler<ProgressChangedEventArgs>? ProgressChanged;

    /// <summary>Gets or sets the current normalized progress state.</summary>
    public ProgressState Progress
    {
        get
        {
            lock (_gate)
            {
                return _progress;
            }
        }
        set => SetProgress(value);
    }

    public void Activate(Terminal terminal)
    {
        ArgumentNullException.ThrowIfNull(terminal);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _active = false;
            _registration?.Dispose();
            _terminal = terminal;
            _registration = terminal.Parser.RegisterOscHandler(OscIdentifier, HandleOscAsync);
            _active = true;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _active = false;
            _registration?.Dispose();
            _registration = null;
            _terminal = null;
            ProgressChanged = null;
        }
    }

    private ValueTask<bool> HandleOscAsync(string data)
    {
        if (!data.StartsWith("4;", StringComparison.Ordinal))
        {
            return ValueTask.FromResult(false);
        }

        string[] parts = data.Split(';');
        if (parts.Length > 3)
        {
            return ValueTask.FromResult(true);
        }

        string valueText = parts.Length == 2 ? string.Empty : parts[2];
        if (!TryParseDecimal(parts[1], out int stateValue))
        {
            return ValueTask.FromResult(true);
        }

        _ = TryParseDecimal(valueText, out int progressValue);
        switch ((ProgressType)stateValue)
        {
            case ProgressType.Remove:
                SetProgress(new ProgressState(ProgressType.Remove, 0));
                break;
            case ProgressType.Set:
                if (progressValue < 0)
                {
                    break;
                }
                SetProgress(new ProgressState(ProgressType.Set, progressValue));
                break;
            case ProgressType.Error:
            case ProgressType.Pause:
                if (progressValue < 0)
                {
                    break;
                }
                SetProgressPreservingZero((ProgressType)stateValue, progressValue);
                break;
            case ProgressType.Indeterminate:
                SetProgressPreservingZero(ProgressType.Indeterminate, 0);
                break;
        }
        return ValueTask.FromResult(true);
    }

    private void SetProgressPreservingZero(ProgressType state, int value)
    {
        ProgressChangedEventArgs? args;
        EventHandler<ProgressChangedEventArgs>? handler;
        lock (_gate)
        {
            int nextValue = value == 0 ? _progress.Value : value;
            _progress = new ProgressState(state, Math.Clamp(nextValue, 0, 100));
            args = new ProgressChangedEventArgs(_progress);
            handler = _active ? ProgressChanged : null;
        }
        handler?.Invoke(this, args);
    }

    private void SetProgress(ProgressState progress)
    {
        ProgressChangedEventArgs? args;
        EventHandler<ProgressChangedEventArgs>? handler;
        Terminal? terminal;
        lock (_gate)
        {
            if (!IsValid(progress.State))
            {
                terminal = _terminal;
                args = null;
                handler = null;
            }
            else
            {
                terminal = null;
                _progress = progress with { Value = Math.Clamp(progress.Value, 0, 100) };
                args = new ProgressChangedEventArgs(_progress);
                handler = _active ? ProgressChanged : null;
            }
        }

        if (args is null)
        {
            terminal?.Options.Logger?.Log(
                TerminalLogLevel.Warning,
                "Progress state is outside the supported range 0 through 4 and was not applied.");
            return;
        }
        handler?.Invoke(this, args);
    }

    private static bool TryParseDecimal(string value, out int result)
    {
        result = 0;
        foreach (char character in value)
        {
            if (character is < '0' or > '9')
            {
                result = -1;
                return false;
            }

            int digit = character - '0';
            result = result > (int.MaxValue - digit) / 10
                ? int.MaxValue
                : result * 10 + digit;
        }
        return true;
    }

    private static bool IsValid(ProgressType state) => state is >= ProgressType.Remove and <= ProgressType.Pause;
}

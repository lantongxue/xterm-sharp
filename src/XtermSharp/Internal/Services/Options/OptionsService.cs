using XtermSharp.Internal.Utilities;

namespace XtermSharp.Internal;

/// <summary>
/// Owns the mutable subset of terminal options and provides the same synchronous notification
/// semantics as xterm.js' internal options service.
/// </summary>
internal sealed class OptionsService : IDisposable
{
    private readonly Emitter<TerminalOption> _optionChanged = new();
    private TerminalOptions _options;

    public OptionsService(TerminalOptions? options = null)
    {
        _options = NormalizeConstructorOptions(options ?? new TerminalOptions());
    }

    public TerminalOptions Options => _options;

    public IReadOnlyList<TerminalOption> OptionNames { get; } = Enum.GetValues<TerminalOption>();

    public XtermEvent<TerminalOption> OnOptionChange => _optionChanged.Event;

    public TerminalOptions Update(TerminalOptionsUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);
        TerminalOptions previous = _options;
        TerminalOptions next = previous.Apply(update);
        _options = next;

        FireIfChanged(TerminalOption.Scrollback, previous.Scrollback, next.Scrollback);
        FireIfChanged(TerminalOption.TabStopWidth, previous.TabStopWidth, next.TabStopWidth);
        FireIfChanged(TerminalOption.ConvertEol, previous.ConvertEol, next.ConvertEol);
        FireIfChanged(TerminalOption.ScrollOnUserInput, previous.ScrollOnUserInput, next.ScrollOnUserInput);
        FireIfChanged(TerminalOption.MacOptionIsMeta, previous.MacOptionIsMeta, next.MacOptionIsMeta);
        FireIfChanged(TerminalOption.UnicodeVersion, previous.UnicodeVersion, next.UnicodeVersion);
        return next;
    }

    public IDisposable OnSpecificOptionChange<T>(TerminalOption option, Action<T> listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        return OnOptionChange(changed =>
        {
            if (changed == option)
            {
                listener((T)GetValue(option));
            }
        });
    }

    public IDisposable OnMultipleOptionChange(
        IReadOnlyCollection<TerminalOption> options,
        Action listener)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(listener);
        HashSet<TerminalOption> selected = options.ToHashSet();
        return OnOptionChange(changed =>
        {
            if (selected.Contains(changed))
            {
                listener();
            }
        });
    }

    public void Dispose() => _optionChanged.Dispose();

    private static TerminalOptions NormalizeConstructorOptions(TerminalOptions options)
    {
        var defaults = new TerminalOptions();
        return new TerminalOptions
        {
            Columns = options.Columns > 0 ? options.Columns : defaults.Columns,
            Rows = options.Rows > 0 ? options.Rows : defaults.Rows,
            Scrollback = options.Scrollback >= 0 ? options.Scrollback : defaults.Scrollback,
            TabStopWidth = options.TabStopWidth > 0 ? options.TabStopWidth : defaults.TabStopWidth,
            ConvertEol = options.ConvertEol,
            ScrollOnUserInput = options.ScrollOnUserInput,
            MacOptionIsMeta = options.MacOptionIsMeta,
            EnableKittyKeyboard = options.EnableKittyKeyboard,
            EnableWin32InputMode = options.EnableWin32InputMode,
            MaxPendingInputBytes = options.MaxPendingInputBytes > 0
                ? options.MaxPendingInputBytes
                : defaults.MaxPendingInputBytes,
            UnicodeVersion = string.IsNullOrWhiteSpace(options.UnicodeVersion)
                ? defaults.UnicodeVersion
                : options.UnicodeVersion,
            Logger = options.Logger
        };
    }

    private object GetValue(TerminalOption option) => option switch
    {
        TerminalOption.Scrollback => _options.Scrollback,
        TerminalOption.TabStopWidth => _options.TabStopWidth,
        TerminalOption.ConvertEol => _options.ConvertEol,
        TerminalOption.ScrollOnUserInput => _options.ScrollOnUserInput,
        TerminalOption.MacOptionIsMeta => _options.MacOptionIsMeta,
        TerminalOption.UnicodeVersion => _options.UnicodeVersion,
        _ => throw new ArgumentOutOfRangeException(nameof(option))
    };

    private void FireIfChanged<T>(TerminalOption option, T previous, T next)
    {
        if (!EqualityComparer<T>.Default.Equals(previous, next))
        {
            _optionChanged.Fire(option);
        }
    }
}

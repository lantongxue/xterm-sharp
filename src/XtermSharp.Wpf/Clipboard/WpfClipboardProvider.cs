using System.Windows;
using System.Windows.Threading;

namespace XtermSharp.Wpf.Clipboard;

/// <summary>Adapts the WPF system clipboard for the OSC 52 clipboard addon.</summary>
public sealed class WpfClipboardProvider : IClipboardProvider
{
    private readonly Dispatcher _dispatcher;
    private readonly Func<string> _readText;
    private readonly Action<string> _writeText;

    /// <summary>Creates a provider that dispatches clipboard access through the supplied UI object.</summary>
    public WpfClipboardProvider(DispatcherObject dispatcher)
        : this(dispatcher?.Dispatcher ?? throw new ArgumentNullException(nameof(dispatcher)), ReadClipboardText, WriteClipboardText)
    {
    }

    internal WpfClipboardProvider(
        Dispatcher dispatcher,
        Func<string> readText,
        Action<string> writeText)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _readText = readText ?? throw new ArgumentNullException(nameof(readText));
        _writeText = writeText ?? throw new ArgumentNullException(nameof(writeText));
    }

    public async ValueTask<string> ReadTextAsync(
        string selection,
        CancellationToken cancellationToken = default)
    {
        _ = selection;
        cancellationToken.ThrowIfCancellationRequested();
        string text = await InvokeAsync(_readText, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return text;
    }

    public async ValueTask WriteTextAsync(
        string selection,
        string text,
        CancellationToken cancellationToken = default)
    {
        _ = selection;
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();
        await InvokeAsync(
            () =>
            {
                _writeText(text);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private Task<T> InvokeAsync<T>(Func<T> action, CancellationToken cancellationToken)
    {
        if (_dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished)
        {
            return Task.FromException<T>(new InvalidOperationException(
                "The clipboard dispatcher is no longer available."));
        }
        if (_dispatcher.CheckAccess())
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(action());
        }
        return _dispatcher.InvokeAsync(action, DispatcherPriority.Normal, cancellationToken).Task;
    }

    private static string ReadClipboardText() =>
        System.Windows.Clipboard.ContainsText(TextDataFormat.UnicodeText)
            ? System.Windows.Clipboard.GetText(TextDataFormat.UnicodeText)
            : string.Empty;

    private static void WriteClipboardText(string text)
    {
        if (text.Length == 0)
        {
            System.Windows.Clipboard.Clear();
        }
        else
        {
            System.Windows.Clipboard.SetText(text, TextDataFormat.UnicodeText);
        }
    }
}

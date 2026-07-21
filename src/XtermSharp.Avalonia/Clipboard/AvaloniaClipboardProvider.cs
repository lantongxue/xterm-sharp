using Avalonia.Input.Platform;
using Avalonia.Threading;

namespace XtermSharp.Avalonia.Clipboard;

/// <summary>Adapts an Avalonia system clipboard for the OSC 52 clipboard addon.</summary>
public sealed class AvaloniaClipboardProvider : IClipboardProvider
{
    private readonly Func<Task<string?>> _readText;
    private readonly Func<string, Task> _writeText;
    private readonly bool _dispatchToUiThread;

    public AvaloniaClipboardProvider(IClipboard clipboard)
        : this(
            () => clipboard.TryGetTextAsync(),
            text => clipboard.SetTextAsync(text),
            dispatchToUiThread: true)
    {
        ArgumentNullException.ThrowIfNull(clipboard);
    }

    internal AvaloniaClipboardProvider(
        Func<Task<string?>> readText,
        Func<string, Task> writeText,
        bool dispatchToUiThread = false)
    {
        _readText = readText ?? throw new ArgumentNullException(nameof(readText));
        _writeText = writeText ?? throw new ArgumentNullException(nameof(writeText));
        _dispatchToUiThread = dispatchToUiThread;
    }

    public async ValueTask<string> ReadTextAsync(
        string selection,
        CancellationToken cancellationToken = default)
    {
        _ = selection;
        cancellationToken.ThrowIfCancellationRequested();
        string? text = !_dispatchToUiThread || Dispatcher.UIThread.CheckAccess()
            ? await _readText().ConfigureAwait(false)
            : await Dispatcher.UIThread.InvokeAsync(_readText);
        cancellationToken.ThrowIfCancellationRequested();
        return text ?? string.Empty;
    }

    public async ValueTask WriteTextAsync(
        string selection,
        string text,
        CancellationToken cancellationToken = default)
    {
        _ = selection;
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_dispatchToUiThread || Dispatcher.UIThread.CheckAccess())
        {
            await _writeText(text).ConfigureAwait(false);
        }
        else
        {
            await Dispatcher.UIThread.InvokeAsync(() => _writeText(text));
        }
        cancellationToken.ThrowIfCancellationRequested();
    }
}

using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Dispatching;

namespace XtermSharp.Maui.Clipboard;

/// <summary>Adapts a .NET MAUI system clipboard for the OSC 52 clipboard addon.</summary>
public sealed class MauiClipboardProvider : IClipboardProvider
{
    private readonly IClipboard _clipboard;
    private readonly IDispatcher? _dispatcher;

    public MauiClipboardProvider(IClipboard? clipboard = null, IDispatcher? dispatcher = null)
    {
        _clipboard = clipboard ?? global::Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard.Default;
        _dispatcher = dispatcher;
    }

    public async ValueTask<string> ReadTextAsync(
        string selection,
        CancellationToken cancellationToken = default)
    {
        _ = selection;
        cancellationToken.ThrowIfCancellationRequested();
        string? text = await InvokeAsync(_clipboard.GetTextAsync).ConfigureAwait(false);
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
        await InvokeAsync(() => _clipboard.SetTextAsync(text)).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private Task<T> InvokeAsync<T>(Func<Task<T>> operation)
    {
        if (_dispatcher is null || !_dispatcher.IsDispatchRequired)
        {
            return operation();
        }
        return _dispatcher.DispatchAsync(operation);
    }

    private Task InvokeAsync(Func<Task> operation)
    {
        if (_dispatcher is null || !_dispatcher.IsDispatchRequired)
        {
            return operation();
        }
        return _dispatcher.DispatchAsync(operation);
    }
}

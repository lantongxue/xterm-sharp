using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;

namespace XtermSharp.WinUI.Clipboard;

/// <summary>Adapts the WinUI system clipboard for the OSC 52 clipboard addon.</summary>
public sealed class WinUIClipboardProvider : IClipboardProvider
{
    private readonly DispatcherQueue _dispatcher;
    private readonly Func<CancellationToken, Task<string>> _readText;
    private readonly Func<string, CancellationToken, Task> _writeText;

    /// <summary>Creates a provider that dispatches clipboard access through the supplied UI object.</summary>
    public WinUIClipboardProvider(DependencyObject dispatcher)
        : this(
            dispatcher?.DispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcher)),
            ReadClipboardTextAsync,
            WriteClipboardTextAsync)
    {
    }

    internal WinUIClipboardProvider(
        DispatcherQueue dispatcher,
        Func<CancellationToken, Task<string>> readText,
        Func<string, CancellationToken, Task> writeText)
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
            token => _writeText(text, token),
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private Task<T> InvokeAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        if (_dispatcher.HasThreadAccess)
        {
            return action(cancellationToken);
        }

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationTokenRegistration registration = cancellationToken.Register(
            () => completion.TrySetCanceled(cancellationToken));
        if (!_dispatcher.TryEnqueue(async () =>
        {
            try
            {
                completion.TrySetResult(await action(cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                completion.TrySetCanceled(cancellationToken);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }))
        {
            registration.Dispose();
            return Task.FromException<T>(new InvalidOperationException(
                "The clipboard dispatcher is no longer available."));
        }
        return AwaitAndDisposeAsync(completion.Task, registration);
    }

    private async Task InvokeAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        await InvokeAsync(
            async token =>
            {
                await action(token).ConfigureAwait(false);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<T> AwaitAndDisposeAsync<T>(
        Task<T> task,
        CancellationTokenRegistration registration)
    {
        try
        {
            return await task.ConfigureAwait(false);
        }
        finally
        {
            registration.Dispose();
        }
    }

    private static async Task<string> ReadClipboardTextAsync(CancellationToken cancellationToken)
    {
        DataPackageView content = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
        if (!content.Contains(StandardDataFormats.Text))
        {
            return string.Empty;
        }
        string text = await content.GetTextAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return text;
    }

    private static Task WriteClipboardTextAsync(string text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (text.Length == 0)
        {
            Windows.ApplicationModel.DataTransfer.Clipboard.Clear();
            return Task.CompletedTask;
        }
        var package = new DataPackage
        {
            RequestedOperation = DataPackageOperation.Copy
        };
        package.SetText(text);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
        Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
        return Task.CompletedTask;
    }
}

namespace XtermSharp.WinForms.Clipboard;

/// <summary>Adapts the Windows Forms system clipboard for the OSC 52 clipboard addon.</summary>
public sealed class WinFormsClipboardProvider : IClipboardProvider
{
    private readonly Control _dispatcher;
    private readonly Func<string> _readText;
    private readonly Action<string> _writeText;
    private readonly bool _requireHandle;

    public WinFormsClipboardProvider(Control dispatcher)
        : this(dispatcher, ReadClipboardText, WriteClipboardText, requireHandle: true)
    {
    }

    internal WinFormsClipboardProvider(
        Control dispatcher,
        Func<string> readText,
        Action<string> writeText,
        bool requireHandle = false)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _readText = readText ?? throw new ArgumentNullException(nameof(readText));
        _writeText = writeText ?? throw new ArgumentNullException(nameof(writeText));
        _requireHandle = requireHandle;
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
        if (_dispatcher.IsDisposed || _requireHandle && !_dispatcher.IsHandleCreated)
        {
            return Task.FromException<T>(new InvalidOperationException(
                "The clipboard dispatcher must have a live Windows Forms handle."));
        }
        if (!_dispatcher.InvokeRequired)
        {
            return Task.FromResult(action());
        }
        if (!_dispatcher.IsHandleCreated)
        {
            return Task.FromException<T>(new InvalidOperationException(
                "The clipboard dispatcher must have a live Windows Forms handle."));
        }

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationTokenRegistration registration = cancellationToken.Register(
            () => completion.TrySetCanceled(cancellationToken));
        try
        {
            _dispatcher.BeginInvoke(() =>
            {
                try
                {
                    if (!completion.Task.IsCompleted)
                    {
                        completion.TrySetResult(action());
                    }
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
                finally
                {
                    registration.Dispose();
                }
            });
        }
        catch (Exception exception)
        {
            registration.Dispose();
            completion.TrySetException(exception);
        }
        return completion.Task;
    }

    private static string ReadClipboardText() =>
        System.Windows.Forms.Clipboard.ContainsText(TextDataFormat.UnicodeText)
            ? System.Windows.Forms.Clipboard.GetText(TextDataFormat.UnicodeText)
            : string.Empty;

    private static void WriteClipboardText(string text)
    {
        if (text.Length == 0)
        {
            System.Windows.Forms.Clipboard.Clear();
        }
        else
        {
            System.Windows.Forms.Clipboard.SetText(text, TextDataFormat.UnicodeText);
        }
    }
}

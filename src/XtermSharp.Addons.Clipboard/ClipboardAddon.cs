using System.Text;

namespace XtermSharp.Addons.Clipboard;

/// <summary>Handles OSC 52 clipboard read and write requests.</summary>
public sealed class ClipboardAddon : ITerminalAddon
{
    private const int OscIdentifier = 52;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly object _gate = new();
    private readonly IClipboardProvider? _provider;
    private readonly ClipboardAddonOptions _options;
    private readonly IClipboardBase64 _base64;
    private IDisposable? _registration;
    private CancellationTokenSource? _lifetime;
    private bool _disposed;

    public ClipboardAddon(
        IClipboardProvider? provider = null,
        ClipboardAddonOptions? options = null,
        IClipboardBase64? base64 = null)
    {
        _options = (options ?? new ClipboardAddonOptions()).Validate();
        if ((_options.AllowRead || _options.AllowWrite) && provider is null)
        {
            throw new ArgumentNullException(nameof(provider), "An enabled clipboard permission requires a provider.");
        }
        _provider = provider;
        _base64 = base64 ?? new ClipboardBase64();
    }

    public void Activate(Terminal terminal)
    {
        ArgumentNullException.ThrowIfNull(terminal);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            DetachLocked();
            _lifetime = new CancellationTokenSource();
            _registration = terminal.Parser.RegisterOscHandler(OscIdentifier, HandleOscAsync);
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
            DetachLocked();
        }
    }

    private async ValueTask<bool> HandleOscAsync(string data, ITerminalParserContext context)
    {
        CancellationToken cancellationToken;
        lock (_gate)
        {
            if (_disposed || _lifetime is null)
            {
                return true;
            }
            cancellationToken = _lifetime.Token;
        }

        if (!TrySplitPayload(data, out string selection, out string payload) ||
            !IsValidSelection(selection))
        {
            return true;
        }

        try
        {
            if (payload == "?")
            {
                await HandleReadAsync(selection, context, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await HandleWriteAsync(selection, payload, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        return true;
    }

    private async ValueTask HandleReadAsync(
        string selection,
        ITerminalParserContext context,
        CancellationToken cancellationToken)
    {
        if (!_options.AllowRead)
        {
            return;
        }

        string text = await _provider!.ReadTextAsync(selection, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsWithinPayloadLimit(text))
        {
            return;
        }

        string encoded;
        try
        {
            encoded = _base64.EncodeText(text);
        }
        catch (EncoderFallbackException)
        {
            return;
        }
        if (encoded.Length > MaximumEncodedLength(_options.MaxPayloadBytes))
        {
            return;
        }
        context.SendResponse($"\x1b]52;{selection};{encoded}\x07");
    }

    private async ValueTask HandleWriteAsync(
        string selection,
        string payload,
        CancellationToken cancellationToken)
    {
        if (!_options.AllowWrite)
        {
            return;
        }

        string text;
        if (payload.Length == 0 || payload == "!")
        {
            text = string.Empty;
        }
        else
        {
            if (payload.Length > MaximumEncodedLength(_options.MaxPayloadBytes))
            {
                return;
            }
            try
            {
                text = _base64.DecodeText(payload);
            }
            catch (FormatException)
            {
                return;
            }
            catch (DecoderFallbackException)
            {
                return;
            }
            if (!IsWithinPayloadLimit(text))
            {
                return;
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        await _provider!.WriteTextAsync(selection, text, cancellationToken).ConfigureAwait(false);
    }

    private bool IsWithinPayloadLimit(string text)
    {
        try
        {
            return StrictUtf8.GetByteCount(text) <= _options.MaxPayloadBytes;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    private void DetachLocked()
    {
        _registration?.Dispose();
        _registration = null;
        _lifetime?.Cancel();
        _lifetime?.Dispose();
        _lifetime = null;
    }

    private static bool TrySplitPayload(string data, out string selection, out string payload)
    {
        int firstSeparator = data.IndexOf(';');
        if (firstSeparator < 0)
        {
            selection = string.Empty;
            payload = string.Empty;
            return false;
        }

        selection = data[..firstSeparator];
        int secondSeparator = data.IndexOf(';', firstSeparator + 1);
        payload = secondSeparator < 0
            ? data[(firstSeparator + 1)..]
            : data[(firstSeparator + 1)..secondSeparator];
        return true;
    }

    private static bool IsValidSelection(string selection)
    {
        foreach (char value in selection)
        {
            if (value is not ('c' or 'p' or 'q' or 's' or >= '0' and <= '7'))
            {
                return false;
            }
        }
        return true;
    }

    private static long MaximumEncodedLength(int bytes) => ((long)bytes + 2) / 3 * 4;
}

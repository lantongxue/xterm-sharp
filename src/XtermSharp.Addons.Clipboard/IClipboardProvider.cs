namespace XtermSharp.Addons.Clipboard;

/// <summary>Provides platform-neutral access to clipboard text selections.</summary>
public interface IClipboardProvider
{
    ValueTask<string> ReadTextAsync(string selection, CancellationToken cancellationToken = default);
    ValueTask WriteTextAsync(string selection, string text, CancellationToken cancellationToken = default);
}

namespace XtermSharp.Addons.Clipboard.Tests.Support;

internal sealed class RecordingClipboardProvider : IClipboardProvider
{
    public string Text { get; set; } = string.Empty;
    public List<string> Reads { get; } = [];
    public List<(string Selection, string Text)> Writes { get; } = [];
    public Func<string, CancellationToken, ValueTask<string>>? ReadOverride { get; set; }

    public ValueTask<string> ReadTextAsync(string selection, CancellationToken cancellationToken = default)
    {
        Reads.Add(selection);
        return ReadOverride?.Invoke(selection, cancellationToken) ?? ValueTask.FromResult(Text);
    }

    public ValueTask WriteTextAsync(
        string selection,
        string text,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Text = text;
        Writes.Add((selection, text));
        return ValueTask.CompletedTask;
    }
}

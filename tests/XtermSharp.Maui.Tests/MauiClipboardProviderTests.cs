using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace XtermSharp.Maui.Tests;

public sealed class MauiClipboardProviderTests
{
    [Fact]
    public async Task ProviderMapsClipboardTextAndIgnoresSelectionKinds()
    {
        var clipboard = new TestClipboard("initial");
        var provider = new MauiClipboardProvider(clipboard);

        Assert.Equal(
            "initial",
            await provider.ReadTextAsync("p", TestContext.Current.CancellationToken));
        await provider.WriteTextAsync("c", "updated", TestContext.Current.CancellationToken);

        Assert.Equal("updated", clipboard.Text);
    }

    private sealed class TestClipboard(string? text) : IClipboard
    {
        public bool HasText => !string.IsNullOrEmpty(Text);
        public string? Text { get; private set; } = text;

        public event EventHandler<EventArgs>? ClipboardContentChanged;

        public Task<string?> GetTextAsync() => Task.FromResult(Text);

        public Task SetTextAsync(string? text)
        {
            Text = text;
            ClipboardContentChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }
}

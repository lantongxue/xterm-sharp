namespace XtermSharp.Avalonia.Tests;

public sealed class AvaloniaClipboardProviderTests
{
    [Fact]
    public async Task ProviderMapsClipboardTextAndIgnoresSelectionKinds()
    {
        string? clipboardText = "initial";
        var provider = new AvaloniaClipboardProvider(
            () => Task.FromResult<string?>(clipboardText),
            text =>
            {
                clipboardText = text;
                return Task.CompletedTask;
            });

        Assert.Equal(
            "initial",
            await provider.ReadTextAsync("p", TestContext.Current.CancellationToken));
        await provider.WriteTextAsync("c", "updated", TestContext.Current.CancellationToken);

        Assert.Equal("updated", clipboardText);
    }
}

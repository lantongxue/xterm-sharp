namespace XtermSharp.Maui.Tests;

public sealed class TerminalViewTests
{
    [Fact]
    public void TerminalPropertyDoesNotTransferOwnership()
    {
        using var terminal = new Terminal();
        var view = new TerminalView { Terminal = terminal };

        view.Terminal = null;

        Assert.False(terminal.IsDisposed);
        Assert.False(view.ShowRenderingDebugOverlay);
        Assert.Equal(SkiaRenderModePreference.Auto, view.RequestedRenderMode);
        Assert.Equal(SkiaRenderMode.Unknown, view.ActiveRenderMode);
        Assert.False(view.IsGpuAccelerated);
        view.ShowRenderingDebugOverlay = true;
        view.RequestedRenderMode = SkiaRenderModePreference.Software;
        Assert.True(view.ShowRenderingDebugOverlay);
        Assert.Equal(SkiaRenderModePreference.Software, view.RequestedRenderMode);
        Assert.True((bool)view.GetValue(TerminalView.ShowRenderingDebugOverlayProperty));
        Assert.Equal(
            SkiaRenderModePreference.Software,
            (SkiaRenderModePreference)view.GetValue(TerminalView.RequestedRenderModeProperty));
    }

    [Fact]
    public async Task PublicKeyInputIsForwardedToTerminal()
    {
        using var terminal = new Terminal();
        var data = new List<string>();
        terminal.Data += (_, args) => data.Add(args.Data);
        var view = new TerminalView { Terminal = terminal };

        await view.SendKeyAsync(
            new TerminalKeyEvent("Backspace", "Backspace", 8),
            TestContext.Current.CancellationToken);

        Assert.Equal(["\x7f"], data);
    }
}

namespace XtermSharp.Tests.Addon;

public sealed class AddonManagerTests
{
    [UpstreamFact("XTJS-1238", "AddonManager loadAddon should call addon constructor")]
    public async Task LoadAddon_ActivatesTheAddonWithItsTerminal()
    {
        await using var terminal = new Terminal();
        var addon = new RecordingAddon();

        terminal.LoadAddon(addon);

        Assert.Same(terminal, addon.ActivatedTerminal);
        Assert.Equal(1, addon.ActivationCount);
    }

    [UpstreamFact("XTJS-1239", "AddonManager dispose should dispose all loaded addons")]
    public async Task Dispose_DisposesEveryLoadedAddon()
    {
        var terminal = new Terminal();
        var addons = new[] { new RecordingAddon(), new RecordingAddon(), new RecordingAddon() };
        foreach (RecordingAddon addon in addons)
        {
            terminal.LoadAddon(addon);
        }

        await terminal.DisposeAsync();

        Assert.All(addons, addon => Assert.Equal(1, addon.DisposeCount));
    }

}

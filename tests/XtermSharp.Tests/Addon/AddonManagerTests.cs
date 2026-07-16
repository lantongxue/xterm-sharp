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

    private sealed class RecordingAddon : ITerminalAddon
    {
        public Terminal? ActivatedTerminal { get; private set; }

        public int ActivationCount { get; private set; }

        public int DisposeCount { get; private set; }

        public void Activate(Terminal terminal)
        {
            ActivatedTerminal = terminal;
            ActivationCount++;
        }

        public void Dispose() => DisposeCount++;
    }
}

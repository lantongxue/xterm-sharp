namespace XtermSharp.Addons.Progress.Tests;

public sealed class ProgressAddonTests
{
    [Fact]
    public async Task InitialValuesAreRemoveAndZero()
    {
        await using var terminal = CreateTerminal();
        using ProgressAddon addon = LoadAddon(terminal);

        Assert.Equal(new ProgressState(ProgressType.Remove, 0), addon.Progress);
    }

    [Fact]
    public async Task RemoveResetsProgressAndIgnoresValue()
    {
        await using var terminal = CreateTerminal();
        using ProgressAddon addon = LoadAddon(terminal);
        List<ProgressState> changes = RecordChanges(addon);

        await WriteAsync(terminal, "\x1b]9;4;0\x1b\\");
        await WriteAsync(terminal, "\x1b]9;4;0;12\x1b\\");

        Assert.Equal(
            [
                new ProgressState(ProgressType.Remove, 0),
                new ProgressState(ProgressType.Remove, 0)
            ],
            changes);
    }

    [Fact]
    public async Task SetReportsPercentageValues()
    {
        await using var terminal = CreateTerminal();
        using ProgressAddon addon = LoadAddon(terminal);
        List<ProgressState> changes = RecordChanges(addon);

        await WriteAsync(terminal, "\x1b]9;4;1;10\x1b\\");
        await WriteAsync(terminal, "\x1b]9;4;1;50\x1b\\");
        await WriteAsync(terminal, "\x1b]9;4;1;23\x1b\\");

        Assert.Equal(
            [
                new ProgressState(ProgressType.Set, 10),
                new ProgressState(ProgressType.Set, 50),
                new ProgressState(ProgressType.Set, 23)
            ],
            changes);
    }

    [Fact]
    public async Task SetHandlesMissingMalformedAndOutOfBoundsValues()
    {
        await using var terminal = CreateTerminal();
        using ProgressAddon addon = LoadAddon(terminal);
        List<ProgressState> changes = RecordChanges(addon);

        await WriteAsync(terminal, "\x1b]9;4;1\x1b\\");
        await WriteAsync(terminal, "\x1b]9;4;1;12x\x1b\\");
        await WriteAsync(terminal, "\x1b]9;4;1;123\x1b\\");

        Assert.Equal(
            [
                new ProgressState(ProgressType.Set, 0),
                new ProgressState(ProgressType.Set, 100)
            ],
            changes);
    }

    [Fact]
    public async Task ErrorPreservesPreviousValueWhenOmittedEmptyOrZero()
    {
        await using var terminal = CreateTerminal();
        using ProgressAddon addon = LoadAddon(terminal);
        List<ProgressState> changes = RecordChanges(addon);

        await WriteAsync(terminal, "\x1b]9;4;1;12\x1b\\");
        await WriteAsync(terminal, "\x1b]9;4;2\x1b\\");
        await WriteAsync(terminal, "\x1b]9;4;2;\x1b\\");
        await WriteAsync(terminal, "\x1b]9;4;2;0\x1b\\");

        Assert.Equal(
            [
                new ProgressState(ProgressType.Set, 12),
                new ProgressState(ProgressType.Error, 12),
                new ProgressState(ProgressType.Error, 12),
                new ProgressState(ProgressType.Error, 12)
            ],
            changes);
    }

    [Fact]
    public async Task ErrorAcceptsAndClampsNewValues()
    {
        await using var terminal = CreateTerminal();
        using ProgressAddon addon = LoadAddon(terminal);
        List<ProgressState> changes = RecordChanges(addon);

        await WriteAsync(terminal, "\x1b]9;4;1;12\x1b\\");
        await WriteAsync(terminal, "\x1b]9;4;2;25\x1b\\");
        await WriteAsync(terminal, "\x1b]9;4;2;123\x1b\\");

        Assert.Equal(
            [
                new ProgressState(ProgressType.Set, 12),
                new ProgressState(ProgressType.Error, 25),
                new ProgressState(ProgressType.Error, 100)
            ],
            changes);
    }

    [Fact]
    public async Task IndeterminateKeepsThePreviousValue()
    {
        await using var terminal = CreateTerminal();
        using ProgressAddon addon = LoadAddon(terminal);
        List<ProgressState> changes = RecordChanges(addon);

        await WriteAsync(terminal, "\x1b]9;4;1;12\x1b\\");
        await WriteAsync(terminal, "\x1b]9;4;3\x1b\\");
        await WriteAsync(terminal, "\x1b]9;4;3;123\x1b\\");

        Assert.Equal(
            [
                new ProgressState(ProgressType.Set, 12),
                new ProgressState(ProgressType.Indeterminate, 12),
                new ProgressState(ProgressType.Indeterminate, 12)
            ],
            changes);
    }

    [Fact]
    public async Task PausePreservesPreviousValueWhenOmittedEmptyOrZero()
    {
        await using var terminal = CreateTerminal();
        using ProgressAddon addon = LoadAddon(terminal);
        List<ProgressState> changes = RecordChanges(addon);

        await WriteAsync(terminal, "\x1b]9;4;1;12\x1b\\");
        await WriteAsync(terminal, "\x1b]9;4;4\x1b\\");
        await WriteAsync(terminal, "\x1b]9;4;4;\x1b\\");
        await WriteAsync(terminal, "\x1b]9;4;4;0\x1b\\");

        Assert.Equal(
            [
                new ProgressState(ProgressType.Set, 12),
                new ProgressState(ProgressType.Pause, 12),
                new ProgressState(ProgressType.Pause, 12),
                new ProgressState(ProgressType.Pause, 12)
            ],
            changes);
    }

    [Fact]
    public async Task PauseAcceptsAndClampsNewValues()
    {
        await using var terminal = CreateTerminal();
        using ProgressAddon addon = LoadAddon(terminal);
        List<ProgressState> changes = RecordChanges(addon);

        await WriteAsync(terminal, "\x1b]9;4;1;12\x1b\\");
        await WriteAsync(terminal, "\x1b]9;4;4;25\x1b\\");
        await WriteAsync(terminal, "\x1b]9;4;4;123\x1b\\");

        Assert.Equal(
            [
                new ProgressState(ProgressType.Set, 12),
                new ProgressState(ProgressType.Pause, 25),
                new ProgressState(ProgressType.Pause, 100)
            ],
            changes);
    }

    [Fact]
    public async Task InvalidSequencesDoNotEmitChanges()
    {
        await using var terminal = CreateTerminal();
        using ProgressAddon addon = LoadAddon(terminal);
        List<ProgressState> changes = RecordChanges(addon);

        await WriteAsync(terminal, "\x1b]9;4;5;12\x1b\\");
        await WriteAsync(terminal, "\x1b]9;4;1; 123xxxx\x1b\\");
        await WriteAsync(terminal, "\x1b]9;4;1;12;extra\x1b\\");

        Assert.Empty(changes);
        Assert.Equal(new ProgressState(ProgressType.Remove, 0), addon.Progress);
    }

    [Fact]
    public async Task ProgrammaticUpdatesNormalizeNotifyResetAndRestore()
    {
        await using var terminal = CreateTerminal();
        using ProgressAddon addon = LoadAddon(terminal);
        List<ProgressState> changes = RecordChanges(addon);

        addon.Progress = new ProgressState(ProgressType.Set, 120);
        addon.Progress = new ProgressState(ProgressType.Error, -1);
        addon.Progress = new ProgressState((ProgressType)5, 50);
        addon.Progress = new ProgressState(ProgressType.Remove, 0);
        addon.Progress = new ProgressState(ProgressType.Pause, 42);

        Assert.Equal(
            [
                new ProgressState(ProgressType.Set, 100),
                new ProgressState(ProgressType.Error, 0),
                new ProgressState(ProgressType.Remove, 0),
                new ProgressState(ProgressType.Pause, 42)
            ],
            changes);
        Assert.Equal(new ProgressState(ProgressType.Pause, 42), addon.Progress);
    }

    [Fact]
    public async Task NonProgressSequencesFallThroughAndDisposeUnregistersTheHandler()
    {
        await using var terminal = CreateTerminal();
        var fallbackPayloads = new List<string>();
        using IDisposable fallback = terminal.Parser.RegisterOscHandler(9, data =>
        {
            fallbackPayloads.Add(data);
            return ValueTask.FromResult(true);
        });
        var addon = new ProgressAddon();
        terminal.LoadAddon(addon);
        List<ProgressState> changes = RecordChanges(addon);

        await WriteAsync(terminal, "\x1b]9;unrelated\x1b\\");
        await WriteAsync(terminal, "\x1b]9;4;1;25\x1b\\");
        addon.Dispose();
        await WriteAsync(terminal, "\x1b]9;4;1;50\x1b\\");

        Assert.Equal(["unrelated", "4;1;50"], fallbackPayloads);
        Assert.Equal([new ProgressState(ProgressType.Set, 25)], changes);
    }

    private static Terminal CreateTerminal() =>
        new(new TerminalOptions { Columns = 10, Rows = 2 });

    private static ProgressAddon LoadAddon(Terminal terminal)
    {
        var addon = new ProgressAddon();
        terminal.LoadAddon(addon);
        return addon;
    }

    private static List<ProgressState> RecordChanges(ProgressAddon addon)
    {
        var changes = new List<ProgressState>();
        addon.ProgressChanged += (_, args) => changes.Add(args.Progress);
        return changes;
    }

    private static ValueTask WriteAsync(Terminal terminal, string data) =>
        terminal.WriteAsync(data, TestContext.Current.CancellationToken);
}

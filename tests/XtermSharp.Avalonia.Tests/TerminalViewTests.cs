using Avalonia.Input;

namespace XtermSharp.Avalonia.Tests;

public sealed class TerminalViewTests
{
    [Fact]
    public void TerminalPropertyDoesNotTransferOwnership()
    {
        using var terminal = new Terminal();
        var view = new TerminalView { Terminal = terminal };

        view.Terminal = null;

        Assert.False(terminal.IsDisposed);
    }

    [Fact]
    public void KeyMapperProducesTerminalProtocolCoordinates()
    {
        TerminalKeyEvent key = AvaloniaKeyMapper.Create(
            Key.Left,
            PhysicalKey.ArrowLeft,
            null,
            KeyModifiers.Control | KeyModifiers.Shift,
            TerminalKeyEventType.Press);

        Assert.Equal("ArrowLeft", key.Key);
        Assert.Equal(37, key.KeyCode);
        Assert.Equal(TerminalModifiers.Control | TerminalModifiers.Shift, key.Modifiers);
    }

    [Fact]
    public async Task BackspaceKeyProducesLegacyDelInput()
    {
        using var terminal = new Terminal();
        var data = new List<string>();
        terminal.Data += (_, args) => data.Add(args.Data);
        TerminalKeyEvent key = AvaloniaKeyMapper.Create(
            Key.Back,
            PhysicalKey.Backspace,
            null,
            KeyModifiers.None,
            TerminalKeyEventType.Press);

        await terminal.SendKeyAsync(key, TestContext.Current.CancellationToken);

        Assert.Equal("Backspace", key.Key);
        Assert.Equal(8, key.KeyCode);
        Assert.Equal(["\x7f"], data);
        Assert.False(AvaloniaKeyMapper.ShouldUseTextInput(Key.Back, "\b", KeyModifiers.None));
    }
}

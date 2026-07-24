namespace XtermSharp.Maui.Tests;

public sealed class MauiTerminalInputTests
{
    [Fact]
    public void PlainEnterDefersToCommittedTextPath()
    {
        var key = new TerminalKeyEvent("Enter", "Enter", 13);

        Assert.True(MauiTerminalInput.ShouldDeferHardwareKey(key, enhancedKeyboardMode: false));
    }

    [Theory]
    [InlineData(TerminalModifiers.Control)]
    [InlineData(TerminalModifiers.Alt)]
    [InlineData(TerminalModifiers.Meta)]
    public void ModifiedEnterStaysOnHardwarePath(TerminalModifiers modifiers)
    {
        var key = new TerminalKeyEvent("Enter", "Enter", 13, modifiers);

        Assert.False(MauiTerminalInput.ShouldDeferHardwareKey(key, enhancedKeyboardMode: false));
    }

    [Fact]
    public void EnhancedEnterStaysOnHardwarePath()
    {
        var key = new TerminalKeyEvent("Enter", "Enter", 13);

        Assert.False(MauiTerminalInput.ShouldDeferHardwareKey(key, enhancedKeyboardMode: true));
    }

    [Theory]
    [InlineData(120, 3)]
    [InlineData(-240, 6)]
    [InlineData(1, 1)]
    [InlineData(0, 0)]
    public void WheelDeltaUsesStableLineSteps(int delta, int expected)
    {
        Assert.Equal(expected, MauiTerminalInput.GetWheelLines(delta));
    }
}

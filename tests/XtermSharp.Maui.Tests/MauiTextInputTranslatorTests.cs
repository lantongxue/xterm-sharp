namespace XtermSharp.Maui.Tests;

public sealed class MauiTextInputTranslatorTests
{
    [Fact]
    public void SentinelOnlyDoesNotEmitInput()
    {
        MauiTextInput input = MauiTextInputTranslator.Translate(MauiTextInputTranslator.Sentinel);

        Assert.Equal(MauiTextInputKind.None, input.Kind);
        Assert.Empty(input.Text);
    }

    [Fact]
    public void AppendedTextEmitsCommittedTextWithoutSentinel()
    {
        MauiTextInput input = MauiTextInputTranslator.Translate(MauiTextInputTranslator.Sentinel + "你好");

        Assert.Equal(MauiTextInputKind.Text, input.Kind);
        Assert.Equal("你好", input.Text);
    }

    [Theory]
    [InlineData("l\u200B", "l")]
    [InlineData("l\u200Bs", "ls")]
    [InlineData("\u200Bl\u200B", "l")]
    public void SentinelIsRemovedRegardlessOfNativeCaretPosition(string value, string expected)
    {
        MauiTextInput input = MauiTextInputTranslator.Translate(value);

        Assert.Equal(MauiTextInputKind.Text, input.Kind);
        Assert.Equal(expected, input.Text);
    }

    [Fact]
    public void RemovingSentinelEmitsBackspace()
    {
        MauiTextInput input = MauiTextInputTranslator.Translate(string.Empty);

        Assert.Equal(MauiTextInputKind.Backspace, input.Kind);
        Assert.Empty(input.Text);
    }

    [Fact]
    public void NativeBackspaceAtSentinelCanBeInterceptedRepeatedly()
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            Assert.Equal(
                1,
                MauiTextInputTranslator.GetNativeBackspaceCount(
                    MauiTextInputTranslator.Sentinel,
                    1,
                    0));
        }
    }

    [Theory]
    [InlineData("text", 1, 0)]
    [InlineData("\u200B", 0, 0)]
    [InlineData("\u200B", 1, 1)]
    public void NativeDeletionOutsideIdleSentinelIsNotIntercepted(
        string value,
        int beforeLength,
        int afterLength)
    {
        Assert.Equal(
            0,
            MauiTextInputTranslator.GetNativeBackspaceCount(
                value,
                beforeLength,
                afterLength));
    }
}

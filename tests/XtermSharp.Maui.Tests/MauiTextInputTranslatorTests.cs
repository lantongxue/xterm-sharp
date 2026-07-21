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

    [Fact]
    public void RemovingSentinelEmitsBackspace()
    {
        MauiTextInput input = MauiTextInputTranslator.Translate(string.Empty);

        Assert.Equal(MauiTextInputKind.Backspace, input.Kind);
        Assert.Empty(input.Text);
    }
}

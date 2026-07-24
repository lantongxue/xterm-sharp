namespace XtermSharp.Maui.Input;

internal static class MauiTextInputTranslator
{
    public const string Sentinel = "\u200B";

    public static int GetNativeBackspaceCount(
        string? value,
        int beforeLength,
        int afterLength)
    {
        return value == Sentinel &&
            beforeLength > 0 &&
            afterLength == 0
                ? beforeLength
                : 0;
    }

    public static MauiTextInput Translate(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return MauiTextInput.Backspace;
        }
        if (value == Sentinel)
        {
            return MauiTextInput.None;
        }
        string text = value.Replace(Sentinel, string.Empty, StringComparison.Ordinal);
        return text.Length == 0
            ? MauiTextInput.None
            : new MauiTextInput(MauiTextInputKind.Text, text);
    }
}

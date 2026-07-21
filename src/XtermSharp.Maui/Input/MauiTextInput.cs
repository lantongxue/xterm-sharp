namespace XtermSharp.Maui.Input;

internal readonly record struct MauiTextInput(MauiTextInputKind Kind, string Text)
{
    public static MauiTextInput None { get; } = new(MauiTextInputKind.None, string.Empty);
    public static MauiTextInput Backspace { get; } = new(MauiTextInputKind.Backspace, string.Empty);
}

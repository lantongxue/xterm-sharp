namespace XtermSharp.Maui.Input;

internal static class MauiTerminalInput
{
    public static bool ShouldDeferHardwareKey(TerminalKeyEvent key, bool enhancedKeyboardMode) =>
        !enhancedKeyboardMode &&
        string.Equals(key.Key, "Enter", StringComparison.Ordinal) &&
        (key.Modifiers & (TerminalModifiers.Control | TerminalModifiers.Alt | TerminalModifiers.Meta)) ==
        TerminalModifiers.None;

    public static int GetWheelLines(int delta)
    {
        if (delta == 0)
        {
            return 0;
        }
        return Math.Max(1, Math.Abs(delta) * 3 / 120);
    }

    public static int GetTouchScrollLines(double accumulatedPixels, double cellHeight)
    {
        if (!double.IsFinite(accumulatedPixels) || !double.IsFinite(cellHeight) || cellHeight <= 0)
        {
            return 0;
        }
        return (int)(accumulatedPixels / Math.Max(1, cellHeight));
    }
}

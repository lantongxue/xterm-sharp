namespace XtermSharp.Internal.Engine;

/// <summary>
/// Implements xterm.js' winpty wrapped-line heuristic after a line feed.
/// </summary>
internal static class WindowsMode
{
    public static void UpdateWrappedState(BufferLine? previousLine, BufferLine? nextLine)
    {
        if (previousLine is null || nextLine is null || previousLine.Length == 0)
        {
            return;
        }

        int lastCodePoint = previousLine[previousLine.Length - 1].CodePoint;
        nextLine.IsWrapped = lastCodePoint is not (0 or 0x20);
    }
}

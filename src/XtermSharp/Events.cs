namespace XtermSharp;

public class TerminalEventArgs(long revision) : EventArgs
{
    public long Revision { get; } = revision;
}

public sealed class TerminalDataEventArgs(long revision, string data, bool isBinary = false)
    : TerminalEventArgs(revision)
{
    public string Data { get; } = data;
    public bool IsBinary { get; } = isBinary;
}

public sealed class TerminalTitleChangedEventArgs(long revision, string title)
    : TerminalEventArgs(revision)
{
    public string Title { get; } = title;
}

public sealed class TerminalResizeEventArgs(long revision, int columns, int rows)
    : TerminalEventArgs(revision)
{
    public int Columns { get; } = columns;
    public int Rows { get; } = rows;
}

public sealed class TerminalScrollEventArgs(long revision, int viewportY)
    : TerminalEventArgs(revision)
{
    public int ViewportY { get; } = viewportY;
}

public sealed class TerminalRenderEventArgs(long revision, int startRow, int endRow)
    : TerminalEventArgs(revision)
{
    public int StartRow { get; } = startRow;
    public int EndRow { get; } = endRow;
}

public sealed class TerminalOptionsChangedEventArgs(
    long revision,
    TerminalOptions previous,
    TerminalOptions current)
    : TerminalEventArgs(revision)
{
    public TerminalOptions Previous { get; } = previous;
    public TerminalOptions Current { get; } = current;
}

/// <summary>The action requested by an OSC color control sequence.</summary>
public enum TerminalColorRequestType
{
    Report,
    Set,
    Restore
}

/// <summary>Indexes used by OSC 10, 11 and 12 for non-palette colors.</summary>
public enum TerminalSpecialColorIndex
{
    Foreground = 256,
    Background = 257,
    Cursor = 258
}

/// <summary>A renderer-independent request to report, set or restore a terminal color.</summary>
public sealed record TerminalColorRequest(
    TerminalColorRequestType Type,
    int? Index = null,
    TerminalColor? Color = null);

public sealed class TerminalColorRequestEventArgs(
    long revision,
    IReadOnlyList<TerminalColorRequest> requests)
    : TerminalEventArgs(revision)
{
    public IReadOnlyList<TerminalColorRequest> Requests { get; } = requests;
}

namespace XtermSharp.Links;

/// <summary>
/// A one-based cell position in the active buffer. Xterm-compatible link ranges use an inclusive
/// end position.
/// </summary>
public readonly record struct TerminalLinkPosition(int X, int Y);

using System.Collections.Immutable;

namespace XtermSharp.Rendering.Display;

public sealed record TerminalDisplayRow(int Row, ImmutableArray<TerminalDrawCommand> Commands);

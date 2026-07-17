using System.Collections.Immutable;

namespace XtermSharp.Rendering;

public sealed record TerminalDisplayRow(int Row, ImmutableArray<TerminalDrawCommand> Commands);

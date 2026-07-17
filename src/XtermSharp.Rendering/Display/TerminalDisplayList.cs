using System.Collections.Immutable;

namespace XtermSharp.Rendering.Display;

public sealed record TerminalDisplayList(ImmutableArray<TerminalDisplayRow> Rows)
{
    public static TerminalDisplayList Empty { get; } = new(ImmutableArray<TerminalDisplayRow>.Empty);
}

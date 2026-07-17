namespace XtermSharp.Decorations;

/// <summary>Provides immutable, buffer-relative decorations to terminal renderers.</summary>
public interface ITerminalDecorationProvider
{
    IReadOnlyList<TerminalDecoration> Decorations { get; }

    event EventHandler<EventArgs>? DecorationsChanged;
}

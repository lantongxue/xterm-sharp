namespace XtermSharp.Addons.Progress;

/// <summary>Progress state tracked by <see cref="ProgressAddon"/>.</summary>
/// <param name="State">The current progress state.</param>
/// <param name="Value">The percentage value, normalized to the range 0 through 100 by the addon.</param>
public readonly record struct ProgressState(ProgressType State, int Value);

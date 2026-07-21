namespace XtermSharp.Addons.Progress;

/// <summary>Provides the normalized progress state reported by a terminal or application.</summary>
public sealed class ProgressChangedEventArgs(ProgressState progress) : EventArgs
{
    public ProgressState Progress { get; } = progress;
    public ProgressType State => Progress.State;
    public int Value => Progress.Value;
}

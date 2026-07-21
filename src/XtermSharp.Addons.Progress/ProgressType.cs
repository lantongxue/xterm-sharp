namespace XtermSharp.Addons.Progress;

/// <summary>Describes the state of a terminal-reported operation.</summary>
public enum ProgressType
{
    /// <summary>Removes the progress indication.</summary>
    Remove = 0,

    /// <summary>Reports normal percentage-based progress.</summary>
    Set = 1,

    /// <summary>Reports that the operation failed.</summary>
    Error = 2,

    /// <summary>Reports activity whose percentage cannot be determined.</summary>
    Indeterminate = 3,

    /// <summary>Reports that the operation is paused or needs attention.</summary>
    Pause = 4
}

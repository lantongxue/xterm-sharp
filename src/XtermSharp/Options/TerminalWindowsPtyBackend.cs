namespace XtermSharp.Options;

/// <summary>Identifies the Windows pseudo-terminal backend connected to the terminal.</summary>
public enum TerminalWindowsPtyBackend
{
    /// <summary>The Windows Console pseudo-terminal backend.</summary>
    ConPty,

    /// <summary>The legacy winpty backend.</summary>
    WinPty
}

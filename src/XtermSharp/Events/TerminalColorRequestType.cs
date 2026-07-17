namespace XtermSharp.Events;

/// <summary>The action requested by an OSC color control sequence.</summary>
public enum TerminalColorRequestType
{
    Report,
    Set,
    Restore
}

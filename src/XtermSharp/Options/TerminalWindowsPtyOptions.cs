namespace XtermSharp.Options;

/// <summary>
/// Describes a Windows pseudo-terminal so resize behavior can match the connected backend.
/// </summary>
public sealed class TerminalWindowsPtyOptions
{
    public TerminalWindowsPtyBackend? Backend { get; init; }
    public int? BuildNumber { get; init; }

    internal bool IsConfigured => Backend.HasValue || BuildNumber.HasValue;

    internal int ReflowBuildNumber
    {
        get
        {
            if (!BuildNumber.HasValue || BuildNumber.Value == 0)
            {
                return int.MaxValue;
            }
            return Backend == TerminalWindowsPtyBackend.ConPty ? BuildNumber.Value : 0;
        }
    }

    internal TerminalWindowsPtyOptions Clone() => new()
    {
        Backend = Backend,
        BuildNumber = BuildNumber
    };
}

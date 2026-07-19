namespace XtermSharp.Options;

/// <summary>Controls opt-in and compatibility behavior for vendor VT extensions.</summary>
public sealed class TerminalVtExtensions
{
    /// <summary>
    /// Enables Kitty SGR 221 and 222, which independently clear bold and faint attributes.
    /// </summary>
    public bool KittySgrBoldFaintControl { get; init; } = true;

    internal TerminalVtExtensions Clone() => new()
    {
        KittySgrBoldFaintControl = KittySgrBoldFaintControl
    };
}

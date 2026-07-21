namespace XtermSharp.Parsing;

/// <summary>Provides operations that are valid while a custom parser handler is executing.</summary>
public interface ITerminalParserContext
{
    /// <summary>
    /// Sends a terminal response toward the backing session as part of the current parser commit.
    /// </summary>
    void SendResponse(string data);
}

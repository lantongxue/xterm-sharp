using XtermSharp.TestSupport;

internal sealed record PortMapEntry(
    string Id,
    UpstreamTestStatus Status,
    string CsharpTest,
    string? Difference);

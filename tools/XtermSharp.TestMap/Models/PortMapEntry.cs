using XtermSharp.TestSupport;

namespace XtermSharp.TestMap.Models;

internal sealed record PortMapEntry(
    string Id,
    UpstreamTestStatus Status,
    string CsharpTest,
    string? Difference);

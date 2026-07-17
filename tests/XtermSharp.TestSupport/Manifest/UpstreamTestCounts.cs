using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XtermSharp.TestSupport.Manifest;

public sealed record UpstreamTestCounts
{
    public required int DiscoveredFiles { get; init; }

    public required int Discovered { get; init; }

    public required int ExcludedRenderer { get; init; }

    public required int Required { get; init; }

    public required int Pending { get; init; }

    public required int Ported { get; init; }

    public required int ArchitectureEquivalent { get; init; }
}

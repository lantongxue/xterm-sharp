using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XtermSharp.TestSupport;

public sealed record UpstreamTestCase
{
    public required string Id { get; init; }

    public required string File { get; init; }

    public required string FullTitle { get; init; }

    public required int Occurrence { get; init; }

    public required string Area { get; init; }

    public required UpstreamTestStatus Status { get; init; }

    public string? CsharpTest { get; init; }

    public string? Difference { get; init; }

    public string? ExclusionReason { get; init; }
}

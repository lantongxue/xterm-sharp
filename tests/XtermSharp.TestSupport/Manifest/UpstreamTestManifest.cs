using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XtermSharp.TestSupport.Manifest;

public sealed record UpstreamTestManifest
{
    public required int SchemaVersion { get; init; }

    public required UpstreamBaseline Upstream { get; init; }

    public required string Generator { get; init; }

    public required UpstreamTestCounts Counts { get; init; }

    public required IReadOnlyList<UpstreamTestCase> Tests { get; init; }
}

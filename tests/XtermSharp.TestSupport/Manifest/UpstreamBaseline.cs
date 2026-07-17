using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XtermSharp.TestSupport;

public sealed record UpstreamBaseline
{
    public required string Repository { get; init; }

    public required string Commit { get; init; }

    public required string Version { get; init; }
}

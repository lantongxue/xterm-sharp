using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XtermSharp.TestSupport.Manifest;

[JsonConverter(typeof(JsonStringEnumConverter<UpstreamTestStatus>))]
public enum UpstreamTestStatus
{
    Pending,
    Ported,
    ArchitectureEquivalent,
    [JsonStringEnumMemberName("Excluded.Renderer")]
    ExcludedRenderer
}

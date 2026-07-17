using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XtermSharp.TestSupport.Manifest;

public static class UpstreamManifest
{
    private const string ResourceName = "XtermSharp.TestSupport.upstream-tests.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static UpstreamTestManifest LoadEmbedded()
    {
        using Stream stream = typeof(UpstreamManifest).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded manifest {ResourceName} was not found.");
        return Load(stream);
    }

    public static UpstreamTestManifest Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using FileStream stream = File.OpenRead(path);
        return Load(stream);
    }

    public static UpstreamTestManifest Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return JsonSerializer.Deserialize<UpstreamTestManifest>(stream, SerializerOptions)
            ?? throw new InvalidDataException("The upstream test manifest is empty.");
    }
}

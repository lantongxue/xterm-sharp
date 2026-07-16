using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XtermSharp.TestSupport;

public sealed record UpstreamTestManifest
{
    public required int SchemaVersion { get; init; }

    public required UpstreamBaseline Upstream { get; init; }

    public required string Generator { get; init; }

    public required UpstreamTestCounts Counts { get; init; }

    public required IReadOnlyList<UpstreamTestCase> Tests { get; init; }
}

public sealed record UpstreamBaseline
{
    public required string Repository { get; init; }

    public required string Commit { get; init; }

    public required string Version { get; init; }
}

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

[JsonConverter(typeof(JsonStringEnumConverter<UpstreamTestStatus>))]
public enum UpstreamTestStatus
{
    Pending,
    Ported,
    ArchitectureEquivalent,
    [JsonStringEnumMemberName("Excluded.Renderer")]
    ExcludedRenderer
}

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

public sealed record UpstreamTestBinding(
    string AssemblyName,
    string TypeName,
    string MethodName,
    string Id,
    string Title)
{
    public string CsharpTest => $"{AssemblyName}:{TypeName}.{MethodName}";
}

public static class UpstreamTestBindingDiscovery
{
    public static IReadOnlyList<UpstreamTestBinding> Discover(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        var result = new List<UpstreamTestBinding>();

        foreach (Assembly assembly in assemblies)
        {
            ArgumentNullException.ThrowIfNull(assembly);
            foreach (Type type in assembly.GetTypes())
            {
                foreach (MethodInfo method in type.GetMethods(
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Instance |
                    BindingFlags.Static |
                    BindingFlags.DeclaredOnly))
                {
                    UpstreamFactAttribute? attribute = method.GetCustomAttribute<UpstreamFactAttribute>();
                    if (attribute is null)
                    {
                        DiscoverTheoryRows(result, assembly, type, method);
                        continue;
                    }

                    result.Add(new UpstreamTestBinding(
                        assembly.GetName().Name ?? assembly.FullName ?? "unknown",
                        type.FullName ?? type.Name,
                        method.Name,
                        attribute.UpstreamId,
                        attribute.UpstreamTitle));
                }
            }
        }

        return result;
    }

    private static void DiscoverTheoryRows(
        ICollection<UpstreamTestBinding> result,
        Assembly assembly,
        Type testType,
        MethodInfo testMethod)
    {
        foreach (MemberDataAttribute attribute in testMethod.GetCustomAttributes<MemberDataAttribute>())
        {
            Type memberType = attribute.MemberType ?? testType;
            object? dataSource = GetMemberValue(memberType, attribute.MemberName, attribute.Arguments);
            if (dataSource is not IEnumerable rows)
            {
                continue;
            }

            foreach (object? item in rows)
            {
                if (item is not ITheoryDataRow row)
                {
                    continue;
                }

                object?[] data = row.GetData();
                if (data.Length == 0 || data[0] is not string id || !id.StartsWith("XTJS-", StringComparison.Ordinal))
                {
                    continue;
                }

                string displayName = row.TestDisplayName ?? string.Empty;
                string prefix = $"{id} ";
                string title = displayName.StartsWith(prefix, StringComparison.Ordinal)
                    ? displayName[prefix.Length..]
                    : displayName;
                result.Add(new UpstreamTestBinding(
                    assembly.GetName().Name ?? assembly.FullName ?? "unknown",
                    testType.FullName ?? testType.Name,
                    $"{testMethod.Name}[{id}]",
                    id,
                    title));
            }
        }
    }

    private static object? GetMemberValue(Type memberType, string memberName, object?[] arguments)
    {
        const BindingFlags Flags =
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Static |
            BindingFlags.FlattenHierarchy;

        PropertyInfo? property = memberType.GetProperty(memberName, Flags);
        if (property is not null)
        {
            return property.GetValue(null);
        }

        FieldInfo? field = memberType.GetField(memberName, Flags);
        if (field is not null)
        {
            return field.GetValue(null);
        }

        MethodInfo? method = memberType
            .GetMethods(Flags)
            .FirstOrDefault(candidate =>
                candidate.Name == memberName && candidate.GetParameters().Length == arguments.Length);
        return method?.Invoke(null, arguments);
    }
}

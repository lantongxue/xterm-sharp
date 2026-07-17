using System.Text.Json;
using System.Text.Json.Serialization;
using XtermSharp.ReferenceTests.Infrastructure;
using XtermSharp.TestSupport;
using XtermSharp.Tests.Infrastructure;

string repositoryRoot = FindRepositoryRoot(Environment.CurrentDirectory);
string outputPath = Path.Combine(repositoryRoot, "tests", "upstream-port-map.json");
bool check = false;

for (int index = 0; index < args.Length; index++)
{
    switch (args[index])
    {
        case "--check":
            check = true;
            break;
        case "--output" when index + 1 < args.Length:
            outputPath = Path.GetFullPath(args[++index]);
            break;
        default:
            Console.Error.WriteLine($"Unknown or incomplete argument: {args[index]}");
            return 2;
    }
}

UpstreamTestManifest manifest = UpstreamManifest.LoadEmbedded();
IReadOnlyList<UpstreamTestBinding> bindings = UpstreamTestBindingDiscovery.Discover(
    typeof(UpstreamManifestAuditTests).Assembly,
    typeof(ReferenceTestInfrastructureTests).Assembly);
IReadOnlyList<string> bindingErrors = UpstreamManifestValidator.ValidateBindingIdentities(manifest, bindings);
if (bindingErrors.Count > 0)
{
    Console.Error.WriteLine(string.Join(Environment.NewLine, bindingErrors));
    return 1;
}

PortMapFile existing = File.Exists(outputPath)
    ? JsonSerializer.Deserialize<PortMapFile>(File.ReadAllText(outputPath), JsonOptions())
        ?? throw new InvalidDataException($"Port map {outputPath} is empty.")
    : new PortMapFile(1, []);
if (existing.SchemaVersion != 1)
{
    Console.Error.WriteLine($"Unsupported port-map schemaVersion {existing.SchemaVersion}; expected 1.");
    return 1;
}
Dictionary<string, PortMapEntry> existingById = existing.Entries.ToDictionary(entry => entry.Id, StringComparer.Ordinal);
Dictionary<string, UpstreamTestBinding> bindingById = bindings.ToDictionary(binding => binding.Id, StringComparer.Ordinal);

var entries = new List<PortMapEntry>(bindings.Count);
foreach (UpstreamTestCase test in manifest.Tests)
{
    if (!bindingById.TryGetValue(test.Id, out UpstreamTestBinding? binding))
    {
        continue;
    }

    existingById.TryGetValue(test.Id, out PortMapEntry? previous);
    bool architectureEquivalent = previous?.Status == UpstreamTestStatus.ArchitectureEquivalent;
    entries.Add(new PortMapEntry(
        test.Id,
        architectureEquivalent ? UpstreamTestStatus.ArchitectureEquivalent : UpstreamTestStatus.Ported,
        binding.CsharpTest,
        architectureEquivalent ? previous!.Difference : null));
}

var portMap = new PortMapFile(1, entries);
string serialized = JsonSerializer.Serialize(portMap, JsonOptions()) + Environment.NewLine;
if (check)
{
    if (!File.Exists(outputPath) || File.ReadAllText(outputPath) != serialized)
    {
        Console.Error.WriteLine(
            $"{Path.GetRelativePath(repositoryRoot, outputPath)} is stale. " +
            "Run `dotnet run --project tools/XtermSharp.TestMap`." );
        return 1;
    }

    IReadOnlyList<string> coverageErrors = UpstreamManifestValidator.ValidateBindings(manifest, bindings);
    if (coverageErrors.Count > 0)
    {
        Console.Error.WriteLine(string.Join(Environment.NewLine, coverageErrors));
        return 1;
    }
    Console.WriteLine($"Verified {entries.Count} C# upstream bindings in {Path.GetRelativePath(repositoryRoot, outputPath)}.");
    return 0;
}

File.WriteAllText(outputPath, serialized);
Console.WriteLine($"Wrote {entries.Count} C# upstream bindings to {Path.GetRelativePath(repositoryRoot, outputPath)}.");
return 0;

static JsonSerializerOptions JsonOptions() => new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true,
    Converters = { new JsonStringEnumConverter<UpstreamTestStatus>() }
};

static string FindRepositoryRoot(string start)
{
    DirectoryInfo? directory = new(start);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "XtermSharp.sln")))
        {
            return directory.FullName;
        }
        directory = directory.Parent;
    }
    throw new DirectoryNotFoundException("Could not find the xterm-sharp repository root.");
}

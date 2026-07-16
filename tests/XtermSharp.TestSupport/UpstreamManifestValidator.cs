namespace XtermSharp.TestSupport;

public static class UpstreamManifestValidator
{
    public const string ExpectedRepository = "https://github.com/xtermjs/xterm.js";
    public const string ExpectedCommit = "b1aee19ac6d6f4e4d11e4a10a3731b852956bdb7";
    public const string ExpectedVersion = "6.0.0";
    public const int ExpectedDiscoveredFiles = 37;
    public const int ExpectedDiscovered = 1361;
    public const int ExpectedExcludedRenderer = 54;
    public const int ExpectedRequired = 1307;

    private static readonly IReadOnlyDictionary<string, int> ExpectedRequiredAreaCounts =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Utilities"] = 43,
            ["Decoder/Unicode/WriteBuffer/XParseColor"] = 315,
            ["WindowsMode"] = 3,
            ["Parser"] = 244,
            ["Buffer/Line/Cell/Reflow"] = 138,
            ["Services"] = 35,
            ["Addon"] = 2,
            ["InputHandler"] = 192,
            ["Keyboard/Kitty/Win32"] = 290,
            ["Headless Terminal"] = 45
        };

    private static readonly IReadOnlyDictionary<string, int> ExpectedRendererFileCounts =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["src/common/Color.test.ts"] = 28,
            ["src/common/MultiKeyMap.test.ts"] = 4,
            ["src/common/SortedList.test.ts"] = 7,
            ["src/common/services/CoreService.test.ts"] = 3,
            ["src/common/services/DecorationService.test.ts"] = 10,
            ["src/common/services/OptionsService.test.ts"] = 2
        };

    public static IReadOnlyList<string> Validate(UpstreamTestManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var errors = new List<string>();

        AddIf(errors, manifest.SchemaVersion != 1, $"schemaVersion must be 1, found {manifest.SchemaVersion}.");
        AddIf(errors, manifest.Upstream.Repository != ExpectedRepository, "The upstream repository is not pinned correctly.");
        AddIf(errors, manifest.Upstream.Commit != ExpectedCommit, "The upstream commit is not pinned correctly.");
        AddIf(errors, manifest.Upstream.Version != ExpectedVersion, "The upstream version is not pinned correctly.");
        AddIf(errors, manifest.Counts.DiscoveredFiles != ExpectedDiscoveredFiles,
            $"discoveredFiles must be {ExpectedDiscoveredFiles}.");
        AddIf(errors, manifest.Tests.Count != ExpectedDiscovered,
            $"The manifest must contain {ExpectedDiscovered} tests, found {manifest.Tests.Count}.");

        for (int index = 0; index < manifest.Tests.Count; index++)
        {
            string expectedId = $"XTJS-{index + 1:0000}";
            AddIf(errors, manifest.Tests[index].Id != expectedId,
                $"Manifest entry {index + 1} must have ID {expectedId}, found {manifest.Tests[index].Id}.");
        }

        foreach (IGrouping<string, UpstreamTestCase> group in manifest.Tests.GroupBy(
                     test => $"{test.File}\0{test.FullTitle}",
                     StringComparer.Ordinal))
        {
            int[] occurrences = group.Select(test => test.Occurrence).Order().ToArray();
            for (int index = 0; index < occurrences.Length; index++)
            {
                AddIf(errors, occurrences[index] != index + 1,
                    $"Occurrences for {group.First().File} / {group.First().FullTitle} are not consecutive.");
            }
        }

        int excluded = CountStatus(manifest, UpstreamTestStatus.ExcludedRenderer);
        int pending = CountStatus(manifest, UpstreamTestStatus.Pending);
        int ported = CountStatus(manifest, UpstreamTestStatus.Ported);
        int equivalent = CountStatus(manifest, UpstreamTestStatus.ArchitectureEquivalent);
        AddIf(errors, excluded != ExpectedExcludedRenderer,
            $"Expected {ExpectedExcludedRenderer} renderer exclusions, found {excluded}.");
        AddIf(errors, manifest.Counts.Discovered != manifest.Tests.Count, "counts.discovered does not match the entries.");
        AddIf(errors, manifest.Counts.ExcludedRenderer != excluded, "counts.excludedRenderer does not match the entries.");
        AddIf(errors, manifest.Counts.Required != ExpectedRequired, $"counts.required must be {ExpectedRequired}.");
        AddIf(errors, manifest.Counts.Required != manifest.Tests.Count - excluded,
            "counts.required does not match discovered minus renderer exclusions.");
        AddIf(errors, manifest.Counts.Pending != pending, "counts.pending does not match the entries.");
        AddIf(errors, manifest.Counts.Ported != ported, "counts.ported does not match the entries.");
        AddIf(errors, manifest.Counts.ArchitectureEquivalent != equivalent,
            "counts.architectureEquivalent does not match the entries.");
        AddIf(errors, pending + ported + equivalent != ExpectedRequired,
            "Required test statuses do not add up to 1307.");

        foreach ((string area, int expected) in ExpectedRequiredAreaCounts)
        {
            int actual = manifest.Tests.Count(test =>
                test.Area == area && test.Status != UpstreamTestStatus.ExcludedRenderer);
            AddIf(errors, actual != expected, $"Area {area} must contain {expected} required tests, found {actual}.");
        }

        UpstreamTestCase[] rendererTests = manifest.Tests
            .Where(test => test.Status == UpstreamTestStatus.ExcludedRenderer)
            .ToArray();
        foreach ((string file, int expected) in ExpectedRendererFileCounts)
        {
            int actual = rendererTests.Count(test => test.File == file);
            AddIf(errors, actual != expected, $"Renderer exclusion file {file} must contain {expected} entries, found {actual}.");
        }

        foreach (UpstreamTestCase test in manifest.Tests)
        {
            bool renderer = test.Status == UpstreamTestStatus.ExcludedRenderer;
            AddIf(errors, renderer && test.Area != "Renderer", $"{test.Id} is excluded but not assigned to Renderer.");
            AddIf(errors, !renderer && test.Area == "Renderer", $"{test.Id} is assigned to Renderer but not excluded.");
            AddIf(errors, renderer && string.IsNullOrWhiteSpace(test.ExclusionReason),
                $"{test.Id} is excluded without an exclusion reason.");
            AddIf(errors, renderer && test.CsharpTest is not null,
                $"{test.Id} is excluded but names a C# test.");
            AddIf(errors, !renderer && test.ExclusionReason is not null,
                $"{test.Id} is not excluded but has an exclusion reason.");
            AddIf(errors,
                test.Status is UpstreamTestStatus.Ported or UpstreamTestStatus.ArchitectureEquivalent &&
                string.IsNullOrWhiteSpace(test.CsharpTest),
                $"{test.Id} is {test.Status} but does not name a C# test.");
            AddIf(errors,
                test.Status == UpstreamTestStatus.Pending && test.CsharpTest is not null,
                $"{test.Id} is Pending but names a C# test.");
            AddIf(errors,
                test.Status == UpstreamTestStatus.ArchitectureEquivalent && string.IsNullOrWhiteSpace(test.Difference),
                $"{test.Id} is ArchitectureEquivalent but does not explain the difference.");
        }

        UpstreamTestCase[] optionExclusions = rendererTests
            .Where(test => test.File == "src/common/services/OptionsService.test.ts")
            .ToArray();
        AddIf(errors, optionExclusions.Any(test => !test.FullTitle.Contains("fontWeight", StringComparison.Ordinal)),
            "Only the two fontWeight OptionsService tests may be renderer exclusions.");

        return errors;
    }

    public static IReadOnlyList<string> ValidateBindings(
        UpstreamTestManifest manifest,
        IEnumerable<UpstreamTestBinding> bindings)
    {
        UpstreamTestBinding[] bindingArray = bindings.ToArray();
        var errors = new List<string>(ValidateBindingIdentities(manifest, bindingArray));
        var manifestById = manifest.Tests.ToDictionary(test => test.Id, StringComparer.Ordinal);

        foreach (UpstreamTestBinding binding in bindingArray)
        {
            if (!manifestById.TryGetValue(binding.Id, out UpstreamTestCase? test))
            {
                continue;
            }
            AddIf(errors,
                test.Status is not UpstreamTestStatus.Ported and not UpstreamTestStatus.ArchitectureEquivalent,
                $"{FormatBinding(binding)} has a C# binding but {binding.Id} is {test.Status} in the manifest.");
            AddIf(errors, test.CsharpTest != binding.CsharpTest,
                $"{binding.Id} csharpTest must be '{binding.CsharpTest}', found '{test.CsharpTest ?? "null"}'.");
        }

        foreach (UpstreamTestCase test in manifest.Tests.Where(test =>
                     test.Status is UpstreamTestStatus.Ported or UpstreamTestStatus.ArchitectureEquivalent))
        {
            UpstreamTestBinding[] matches = bindingArray.Where(binding => binding.Id == test.Id).ToArray();
            AddIf(errors, matches.Length != 1,
                $"{test.Id} is {test.Status} but has {matches.Length} C# bindings; exactly one is required.");
            if (matches.Length == 1)
            {
                AddIf(errors, test.CsharpTest != matches[0].CsharpTest,
                    $"{test.Id} csharpTest does not match its discovered binding '{matches[0].CsharpTest}'.");
            }
        }

        return errors;
    }

    public static IReadOnlyList<string> ValidateBindingIdentities(
        UpstreamTestManifest manifest,
        IEnumerable<UpstreamTestBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(bindings);
        var errors = new List<string>();
        UpstreamTestBinding[] bindingArray = bindings.ToArray();
        var manifestById = manifest.Tests.ToDictionary(test => test.Id, StringComparer.Ordinal);

        foreach (IGrouping<string, UpstreamTestBinding> duplicate in bindingArray
                     .GroupBy(binding => binding.Id, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            errors.Add($"Upstream ID {duplicate.Key} is used by multiple C# tests: " +
                       string.Join(", ", duplicate.Select(FormatBinding)));
        }

        foreach (UpstreamTestBinding binding in bindingArray)
        {
            if (!manifestById.TryGetValue(binding.Id, out UpstreamTestCase? test))
            {
                errors.Add($"{FormatBinding(binding)} uses unknown upstream ID {binding.Id}.");
                continue;
            }
            AddIf(errors, test.Status == UpstreamTestStatus.ExcludedRenderer,
                $"{FormatBinding(binding)} maps excluded renderer test {binding.Id}.");
            AddIf(errors, binding.Title != test.FullTitle,
                $"{FormatBinding(binding)} title does not match {binding.Id}: expected '{test.FullTitle}'.");
        }

        return errors;
    }

    private static int CountStatus(UpstreamTestManifest manifest, UpstreamTestStatus status) =>
        manifest.Tests.Count(test => test.Status == status);

    private static string FormatBinding(UpstreamTestBinding binding) =>
        binding.CsharpTest;

    private static void AddIf(ICollection<string> errors, bool condition, string message)
    {
        if (condition)
        {
            errors.Add(message);
        }
    }
}

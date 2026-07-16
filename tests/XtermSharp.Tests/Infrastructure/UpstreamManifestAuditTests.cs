using XtermSharp.TestSupport;

namespace XtermSharp.Tests.Infrastructure;

public sealed class UpstreamManifestAuditTests
{
    [Fact]
    public void ManifestMatchesPinnedExpandedUpstreamInventory()
    {
        UpstreamTestManifest manifest = UpstreamManifest.LoadEmbedded();

        IReadOnlyList<string> errors = UpstreamManifestValidator.Validate(manifest);

        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }

    [Fact]
    public void UpstreamFactBindingsUseKnownUniqueIdsAndExactTitles()
    {
        UpstreamTestManifest manifest = UpstreamManifest.LoadEmbedded();
        IReadOnlyList<UpstreamTestBinding> bindings = UpstreamTestBindingDiscovery.Discover(
            typeof(UpstreamManifestAuditTests).Assembly,
            typeof(XtermSharp.ReferenceTests.Infrastructure.ReferenceTestInfrastructureTests).Assembly);

        IReadOnlyList<string> errors = UpstreamManifestValidator.ValidateBindings(manifest, bindings);

        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }
}

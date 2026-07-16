using XtermSharp.TestSupport;

namespace XtermSharp.ReferenceTests.Infrastructure;

public sealed class ReferenceTestInfrastructureTests
{
    [Fact]
    public void EmbeddedUpstreamManifestIsAvailable()
    {
        UpstreamTestManifest manifest = UpstreamManifest.LoadEmbedded();

        Assert.Equal(UpstreamManifestValidator.ExpectedDiscovered, manifest.Tests.Count);
    }
}

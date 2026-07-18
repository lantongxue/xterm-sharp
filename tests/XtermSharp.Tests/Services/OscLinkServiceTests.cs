using XtermSharp.Internal;

namespace XtermSharp.Tests.Services;

public sealed class OscLinkServiceTests
{
    [UpstreamFact("XTJS-1282", "OscLinkService constructor link IDs are created and fetched consistently")]
    public void RegisterLink_ReusesIdForTheSameExplicitLink()
    {
        using BufferService buffers = CreateBufferService();
        var links = new OscLinkService(buffers);
        var data = new OscLinkData("bar", "foo");

        int linkId = links.RegisterLink(data);

        Assert.True(linkId > 0);
        Assert.Equal(linkId, links.RegisterLink(data));
        Assert.Single(buffers.Buffer.Markers);
    }

    [UpstreamFact("XTJS-1283", "OscLinkService constructor should dispose the link ID when the last marker is trimmed from the buffer")]
    public void RegisterLink_DropsIdAfterItsLastLineMarkerIsTrimmed()
    {
        using BufferService buffers = CreateBufferService();
        buffers.Buffers.ActivateAlternateBuffer();
        var links = new OscLinkService(buffers);
        var data = new OscLinkData("bar", "foo");
        int firstId = links.RegisterLink(data);

        buffers.Scroll(CellStyle.Default);
        int secondId = links.RegisterLink(data);

        Assert.NotEqual(firstId, secondId);
        Assert.Null(links.GetLinkData(firstId));
        Assert.Equal(data, links.GetLinkData(secondId));
    }

    [UpstreamFact("XTJS-1284", "OscLinkService constructor should fetch link data from link id")]
    public void GetLinkData_ReturnsRegisteredMetadata()
    {
        using BufferService buffers = CreateBufferService();
        var links = new OscLinkService(buffers);
        var data = new OscLinkData("bar", "foo");

        int linkId = links.RegisterLink(data);

        Assert.Equal(data, links.GetLinkData(linkId));
    }

    [Fact]
    public void MarkerObserverFailure_DoesNotInterruptLinkMetadataCleanup()
    {
        using BufferService buffers = CreateBufferService();
        var links = new OscLinkService(buffers);
        int linkId = links.RegisterLink(new OscLinkData("bar", "foo"));
        TerminalMarker marker = Assert.Single(buffers.Buffer.Markers);
        marker.Disposed += (_, _) => throw new InvalidOperationException("observer failure");

        buffers.Buffer.NotifyTrim(1);

        Assert.True(marker.IsDisposed);
        Assert.Null(links.GetLinkData(linkId));
    }

    private static BufferService CreateBufferService()
    {
        using var options = new OptionsService(new TerminalOptions { Rows = 3, Columns = 10 });
        return new BufferService(options);
    }
}

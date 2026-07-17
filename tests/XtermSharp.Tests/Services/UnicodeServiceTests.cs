using System.Text;

namespace XtermSharp.Tests.Services;

public sealed class UnicodeServiceTests
{
    [UpstreamFact("XTJS-1285", "unicode provider default to V6")]
    public void DefaultsToUnicodeV6()
    {
        var service = new UnicodeRegistry(UnicodeV6Provider.VersionName);

        Assert.Equal(UnicodeV6Provider.VersionName, service.ActiveVersion);
        Assert.Contains(UnicodeV6Provider.VersionName, service.Versions);
        service.ActiveVersion = UnicodeV6Provider.VersionName;
        Assert.Equal(5, service.GetStringCellWidth("hello"));
    }

    [UpstreamFact("XTJS-1286", "unicode provider activate should throw for unknown version")]
    public void ActivatingUnknownVersion_Throws()
    {
        var service = new UnicodeRegistry(UnicodeV6Provider.VersionName);

        ArgumentException exception = Assert.Throws<ArgumentException>(() => service.ActiveVersion = "55");
        Assert.Contains("55", exception.Message, StringComparison.Ordinal);
    }

    [UpstreamFact("XTJS-1287", "unicode provider should notify about version change")]
    public void ActiveVersionChange_NotifiesListeners()
    {
        var service = new UnicodeRegistry(UnicodeV6Provider.VersionName);
        var versions = new List<string>();
        service.ActiveVersionChanged += versions.Add;
        var provider = new DoubleWidthProvider();
        using IDisposable registration = service.Register(provider);

        service.ActiveVersion = provider.Version;

        Assert.Equal([provider.Version], versions);
    }

    [UpstreamFact("XTJS-1288", "unicode provider correctly changes provider impl")]
    public void ActiveVersionChange_UsesTheSelectedProvider()
    {
        var service = new UnicodeRegistry(UnicodeV6Provider.VersionName);
        Assert.Equal(5, service.GetStringCellWidth("hello"));
        var provider = new DoubleWidthProvider();
        using IDisposable registration = service.Register(provider);

        service.ActiveVersion = provider.Version;

        Assert.Equal(10, service.GetStringCellWidth("hello"));
    }

    [UpstreamFact("XTJS-1289", "unicode provider wcwidth V6 emoji test")]
    public void UnicodeV6_TreatsModernEmojiAsSingleWidth()
    {
        var service = new UnicodeRegistry(UnicodeV6Provider.VersionName);

        Assert.Equal(10, service.GetStringCellWidth("🤣🤣🤣🤣🤣🤣🤣🤣🤣🤣"));
    }

}

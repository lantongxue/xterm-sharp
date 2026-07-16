using System.Text;
using XtermSharp.Internal;

namespace XtermSharp.Tests.Services;

public sealed class CharsetServiceTests
{
    [UpstreamFact("XTJS-1247", "CharsetService should not update active charset when designating an inactive glevel")]
    public void DesignatingInactiveLevel_DoesNotChangeActiveCharset()
    {
        var service = new CharsetService();
        service.SetGCharset(1, CharsetMaps.DecSpecialGraphics);

        Assert.Equal(0, service.GLevel);
        Assert.Null(service.Charset);
    }

    [UpstreamFact("XTJS-1248", "CharsetService should expose the designated charset after setgLevel")]
    public void SelectingLevel_ExposesItsDesignatedCharset()
    {
        var service = new CharsetService();
        service.SetGCharset(1, CharsetMaps.DecSpecialGraphics);
        service.SetGLevel(1);

        Assert.Same(CharsetMaps.DecSpecialGraphics, service.Charset);
        Assert.Equal("─", service.Translate(new Rune('q')));
    }

    [UpstreamFact("XTJS-1249", "CharsetService should update active charset when designating the current glevel")]
    public void DesignatingCurrentLevel_UpdatesActiveCharset()
    {
        var service = new CharsetService();
        service.SetGLevel(1);
        service.SetGCharset(1, CharsetMaps.DecSpecialGraphics);

        Assert.Same(CharsetMaps.DecSpecialGraphics, service.Charset);
    }

    [UpstreamFact("XTJS-1250", "CharsetService should reset glevel, charsets, and active charset")]
    public void Reset_ClearsLevelsDesignationsAndActiveCharset()
    {
        var service = new CharsetService();
        service.SetGCharset(1, CharsetMaps.DecSpecialGraphics);
        service.SetGLevel(1);

        service.Reset();

        Assert.Equal(0, service.GLevel);
        Assert.Empty(service.Charsets);
        Assert.Null(service.Charset);
    }
}

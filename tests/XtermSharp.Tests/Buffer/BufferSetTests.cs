using XtermSharp.Internal;

namespace XtermSharp.Tests.Buffer;

public sealed class BufferSetTests
{
    [UpstreamFact("XTJS-0127", "BufferSet constructor should create two different buffers: alt and normal")]
    public void Constructor_CreatesDifferentNormalAndAlternateBuffers()
    {
        using var buffers = CreateBufferSet();
        Assert.NotSame(buffers.Normal, buffers.Alternate);
        Assert.Equal(TerminalBufferKind.Normal, buffers.Normal.Kind);
        Assert.Equal(TerminalBufferKind.Alternate, buffers.Alternate.Kind);
    }

    [UpstreamFact("XTJS-0128", "BufferSet activateNormalBuffer should set the normal buffer as the currently active buffer")]
    public void ActivateNormalBuffer_SetsNormalAsActive()
    {
        using var buffers = CreateBufferSet();
        buffers.ActivateAlternateBuffer();
        buffers.ActivateNormalBuffer();
        Assert.Same(buffers.Normal, buffers.Active);
    }

    [UpstreamFact("XTJS-0129", "BufferSet activateAltBuffer should set the alt buffer as the currently active buffer")]
    public void ActivateAlternateBuffer_SetsAlternateAsActive()
    {
        using var buffers = CreateBufferSet();
        buffers.ActivateAlternateBuffer();
        Assert.Same(buffers.Alternate, buffers.Active);
    }

    [UpstreamFact("XTJS-0130", "BufferSet cursor handling when swapping buffers should keep the cursor stationary when activating alt buffer")]
    public void CursorHandling_KeepsCursorWhenActivatingAlternate()
    {
        using var buffers = CreateBufferSet();
        buffers.Normal.CursorX = 30;
        buffers.Normal.CursorY = 10;
        buffers.ActivateAlternateBuffer();
        Assert.Equal(30, buffers.Active.CursorX);
        Assert.Equal(10, buffers.Active.CursorY);
    }

    [UpstreamFact("XTJS-0131", "BufferSet cursor handling when swapping buffers should keep the cursor stationary when activating normal buffer")]
    public void CursorHandling_KeepsCursorWhenActivatingNormal()
    {
        using var buffers = CreateBufferSet();
        buffers.ActivateAlternateBuffer();
        buffers.Alternate.CursorX = 30;
        buffers.Alternate.CursorY = 10;
        buffers.ActivateNormalBuffer();
        Assert.Equal(30, buffers.Active.CursorX);
        Assert.Equal(10, buffers.Active.CursorY);
    }

    [UpstreamFact("XTJS-0132", "BufferSet markers should clear the markers when the buffer is switched")]
    public void Markers_AreClearedWhenSwitchingAwayFromAlternate()
    {
        using var buffers = CreateBufferSet();
        buffers.ActivateAlternateBuffer();
        buffers.Alternate.AddMarker(1);
        Assert.Single(buffers.Alternate.Markers);
        buffers.ActivateNormalBuffer();
        Assert.Empty(buffers.Alternate.Markers);
    }

    [UpstreamFact("XTJS-0133", "BufferSet lifecycle should dispose previous buffers on reset")]
    public void Lifecycle_ResetDisposesPreviousBuffers()
    {
        using var buffers = CreateBufferSet();
        TerminalBuffer previous = buffers.Normal;
        previous.GetLine(0).SetCell(0, CellData.FromText("a", 1, CellStyle.Default));
        previous.TranslateBufferLineToString(0, false);
        Assert.Equal(1, previous.StringCache.EntryCount);
        buffers.Reset();
        Assert.NotSame(previous, buffers.Normal);
        Assert.Equal(0, previous.StringCache.EntryCount);
        Assert.False(previous.StringCache.IsCleanupScheduled);
    }

    [UpstreamFact("XTJS-0134", "BufferSet lifecycle should dispose both buffers when disposed")]
    public void Lifecycle_DisposeDisposesBothBuffers()
    {
        var buffers = CreateBufferSet();
        TerminalBuffer normal = buffers.Normal;
        TerminalBuffer alternate = buffers.Alternate;
        normal.TranslateBufferLineToString(0, false);
        buffers.ActivateAlternateBuffer();
        alternate.TranslateBufferLineToString(0, false);
        Assert.True(normal.StringCache.IsCleanupScheduled);
        Assert.True(alternate.StringCache.IsCleanupScheduled);
        buffers.Dispose();
        Assert.Equal(0, normal.StringCache.EntryCount);
        Assert.Equal(0, alternate.StringCache.EntryCount);
        Assert.False(normal.StringCache.IsCleanupScheduled);
        Assert.False(alternate.StringCache.IsCleanupScheduled);
    }

    private static BufferSet CreateBufferSet() => new(80, 24, 1000, CellStyle.Default);
}

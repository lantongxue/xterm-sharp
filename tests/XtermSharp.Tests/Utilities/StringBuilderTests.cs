using XtermSharp.Internal.Utilities;
using XtermSharp.TestSupport;
using XtermStringBuilder = XtermSharp.Internal.Utilities.StringBuilder;

namespace XtermSharp.Tests.Utilities;

public sealed class StringBuilderTests
{
    [UpstreamFact("XTJS-1297", "StringBuilder should start empty")]
    public void StringBuilder_ShouldStartEmpty()
    {
        var builder = new XtermStringBuilder();
        Assert.Equal(0, builder.Length);
        Assert.Equal(string.Empty, builder.ToString());
    }

    [UpstreamFact("XTJS-1298", "StringBuilder should append a single chunk")]
    public void StringBuilder_ShouldAppendASingleChunk()
    {
        var builder = new XtermStringBuilder();
        builder.Append("hello");
        Assert.Equal(5, builder.Length);
        Assert.Equal("hello", builder.ToString());
    }

    [UpstreamFact("XTJS-1299", "StringBuilder should join multiple chunks in order")]
    public void StringBuilder_ShouldJoinMultipleChunksInOrder()
    {
        var builder = new XtermStringBuilder();
        builder.Append("foo");
        builder.Append("bar");
        builder.Append("baz");
        Assert.Equal(9, builder.Length);
        Assert.Equal("foobarbaz", builder.ToString());
    }

    [UpstreamFact("XTJS-1300", "StringBuilder should handle empty chunks")]
    public void StringBuilder_ShouldHandleEmptyChunks()
    {
        var builder = new XtermStringBuilder();
        builder.Append(string.Empty);
        builder.Append("a");
        builder.Append(string.Empty);
        Assert.Equal(1, builder.Length);
        Assert.Equal("a", builder.ToString());
    }

    [UpstreamFact("XTJS-1301", "StringBuilder should reset accumulated data")]
    public void StringBuilder_ShouldResetAccumulatedData()
    {
        var builder = new XtermStringBuilder();
        builder.Append("hello");
        builder.Reset();
        Assert.Equal(0, builder.Length);
        Assert.Equal(string.Empty, builder.ToString());
    }

    [UpstreamFact("XTJS-1302", "StringBuilder should allow appending after reset")]
    public void StringBuilder_ShouldAllowAppendingAfterReset()
    {
        var builder = new XtermStringBuilder();
        builder.Append("old");
        builder.Reset();
        builder.Append("new");
        Assert.Equal("new", builder.ToString());
    }

    [UpstreamFact("XTJS-1303", "StringBuilder should accumulate many small chunks without quadratic concatenation")]
    public void StringBuilder_ShouldAccumulateManySmallChunksWithoutQuadraticConcatenation()
    {
        var builder = new XtermStringBuilder();
        const string chunk = "x";
        const int count = 10_000;
        for (int i = 0; i < count; i++)
        {
            builder.Append(chunk);
        }
        Assert.Equal(count, builder.Length);
        Assert.Equal(new string('x', count), builder.ToString());
    }

    [UpstreamFact("XTJS-1304", "LimitedStringBuilder should expose the configured limit")]
    public void LimitedStringBuilder_ShouldExposeTheConfiguredLimit()
    {
        var builder = new LimitedStringBuilder(42);
        Assert.Equal(42, builder.Limit);
    }

    [UpstreamFact("XTJS-1305", "LimitedStringBuilder should start empty")]
    public void LimitedStringBuilder_ShouldStartEmpty()
    {
        var builder = new LimitedStringBuilder(10);
        Assert.Equal(0, builder.Length);
        Assert.Equal(string.Empty, builder.ToString());
    }

    [UpstreamFact("XTJS-1306", "LimitedStringBuilder should accept data up to the limit")]
    public void LimitedStringBuilder_ShouldAcceptDataUpToTheLimit()
    {
        var builder = new LimitedStringBuilder(10);
        Assert.False(builder.Append("12345"));
        Assert.False(builder.Append("67890"));
        Assert.Equal(10, builder.Length);
        Assert.Equal("1234567890", builder.ToString());
    }

    [UpstreamFact("XTJS-1307", "LimitedStringBuilder should accept a single chunk exactly at the limit")]
    public void LimitedStringBuilder_ShouldAcceptASingleChunkExactlyAtTheLimit()
    {
        var builder = new LimitedStringBuilder(5);
        Assert.False(builder.Append("abcde"));
        Assert.Equal(5, builder.Length);
        Assert.Equal("abcde", builder.ToString());
    }

    [UpstreamFact("XTJS-1308", "LimitedStringBuilder should reject data exceeding the limit and clear the buffer")]
    public void LimitedStringBuilder_ShouldRejectDataExceedingTheLimitAndClearTheBuffer()
    {
        var builder = new LimitedStringBuilder(5);
        builder.Append("abc");
        Assert.True(builder.Append("def"));
        Assert.Equal(0, builder.Length);
        Assert.Equal(string.Empty, builder.ToString());
    }

    [UpstreamFact("XTJS-1309", "LimitedStringBuilder should reject a single chunk larger than the limit")]
    public void LimitedStringBuilder_ShouldRejectASingleChunkLargerThanTheLimit()
    {
        var builder = new LimitedStringBuilder(3);
        Assert.True(builder.Append("toolong"));
        Assert.Equal(0, builder.Length);
        Assert.Equal(string.Empty, builder.ToString());
    }

    [UpstreamFact("XTJS-1310", "LimitedStringBuilder should allow appending again after reset following a limit breach")]
    public void LimitedStringBuilder_ShouldAllowAppendingAgainAfterResetFollowingALimitBreach()
    {
        var builder = new LimitedStringBuilder(3);
        Assert.True(builder.Append("abcd"));
        builder.Reset();
        Assert.False(builder.Append("ab"));
        Assert.Equal("ab", builder.ToString());
    }

    [UpstreamFact("XTJS-1311", "LimitedStringBuilder should accumulate many chunks before hitting the limit")]
    public void LimitedStringBuilder_ShouldAccumulateManyChunksBeforeHittingTheLimit()
    {
        const int limit = 100;
        var builder = new LimitedStringBuilder(limit);
        const string chunk = "A";
        for (int i = 0; i < limit; i++)
        {
            Assert.False(builder.Append(chunk));
        }
        Assert.Equal(new string('A', limit), builder.ToString());
        Assert.True(builder.Append("B"));
        Assert.Equal(string.Empty, builder.ToString());
    }

    [UpstreamFact("XTJS-1312", "LimitedStringBuilder should reject when limit is zero and any data is appended")]
    public void LimitedStringBuilder_ShouldRejectWhenLimitIsZeroAndAnyDataIsAppended()
    {
        var builder = new LimitedStringBuilder(0);
        Assert.True(builder.Append("a"));
        Assert.Equal(0, builder.Length);
    }

    [UpstreamFact("XTJS-1313", "LimitedStringBuilder should allow zero-length appends at the limit")]
    public void LimitedStringBuilder_ShouldAllowZeroLengthAppendsAtTheLimit()
    {
        var builder = new LimitedStringBuilder(0);
        Assert.False(builder.Append(string.Empty));
        Assert.Equal(0, builder.Length);
        Assert.Equal(string.Empty, builder.ToString());
    }

    [Fact]
    public void LimitedStringBuilder_AppendsUtf32WithoutIntermediateStringsAndCountsUtf16Units()
    {
        var builder = new LimitedStringBuilder(3);

        Assert.False(builder.AppendUtf32(['A', 0x1F600]));
        Assert.Equal(3, builder.Length);
        Assert.Equal("A😀", builder.ToString());
        Assert.True(builder.AppendUtf32(['B']));
        Assert.Equal(string.Empty, builder.ToString());
    }
}

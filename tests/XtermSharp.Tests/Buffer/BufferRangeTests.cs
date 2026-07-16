using XtermSharp.Internal;

namespace XtermSharp.Tests.Buffer;

public sealed class BufferRangeTests
{
    [UpstreamFact("XTJS-0116", "BufferRange getRangeLength should get range for single line")]
    public void GetRangeLength_GetsRangeForSingleLine() =>
        Assert.Equal(4, BufferRangeOperations.GetRangeLength(CreateRange(1, 1, 4, 1), 0));

    [UpstreamFact("XTJS-0117", "BufferRange getRangeLength should throw for invalid range")]
    public void GetRangeLength_ThrowsForInvalidRange() =>
        Assert.Throws<ArgumentException>(() => BufferRangeOperations.GetRangeLength(CreateRange(1, 3, 1, 1), 0));

    [UpstreamFact("XTJS-0118", "BufferRange getRangeLength should get range multiple lines")]
    public void GetRangeLength_GetsRangeAcrossMultipleLines() =>
        Assert.Equal(24, BufferRangeOperations.GetRangeLength(CreateRange(1, 1, 4, 5), 5));

    [UpstreamFact("XTJS-0119", "BufferRange getRangeLength should get range for end line right after start line")]
    public void GetRangeLength_GetsRangeWhenEndLineImmediatelyFollowsStartLine() =>
        Assert.Equal(12, BufferRangeOperations.GetRangeLength(CreateRange(1, 1, 7, 2), 5));

    private static BufferRange CreateRange(int startX, int startY, int endX, int endY) =>
        new(new BufferPosition(startX, startY), new BufferPosition(endX, endY));
}

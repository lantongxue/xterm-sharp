using XtermSharp.Internal;

namespace XtermSharp.Tests.Buffer;

public sealed class BufferReflowTests
{
    private static readonly CellData Blank = CellData.Blank(CellStyle.Default);

    [UpstreamFact("XTJS-0120", "BufferReflow reflowSmallerGetNewLineLengths should return correct line lengths for a small line with wide characters")]
    public void Smaller_ReturnsLengthsForSmallWideLine()
    {
        BufferLine line = CreateWideLine("汉语");
        Assert.Equal([2, 2], BufferReflow.GetNewLineLengths([line], 4, 3));
        Assert.Equal([2, 2], BufferReflow.GetNewLineLengths([line], 4, 2));
    }

    [UpstreamFact("XTJS-0121", "BufferReflow reflowSmallerGetNewLineLengths should return correct line lengths for a large line with wide characters")]
    public void Smaller_ReturnsLengthsForLargeWideLine()
    {
        BufferLine line = CreateWideLine("汉语汉语汉语");
        int[][] expected = [[10, 2], [10, 2], [8, 4], [8, 4], [6, 6], [6, 6], [4, 4, 4], [4, 4, 4], [2, 2, 2, 2, 2, 2], [2, 2, 2, 2, 2, 2]];
        for (int width = 11, index = 0; width >= 2; width--, index++)
        {
            Assert.Equal(expected[index], BufferReflow.GetNewLineLengths([line], 12, width));
        }
    }

    [UpstreamFact("XTJS-0122", "BufferReflow reflowSmallerGetNewLineLengths should return correct line lengths for a string with wide and single characters")]
    public void Smaller_ReturnsLengthsForMixedWideAndSingleCharacters()
    {
        BufferLine line = CreateMixedLine();
        Assert.Equal([5, 1], BufferReflow.GetNewLineLengths([line], 6, 5));
        Assert.Equal([3, 3], BufferReflow.GetNewLineLengths([line], 6, 4));
        Assert.Equal([3, 3], BufferReflow.GetNewLineLengths([line], 6, 3));
        Assert.Equal([1, 2, 2, 1], BufferReflow.GetNewLineLengths([line], 6, 2));
    }

    [UpstreamFact("XTJS-0123", "BufferReflow reflowSmallerGetNewLineLengths should return correct line lengths for a wrapped line with wide and single characters")]
    public void Smaller_ReturnsLengthsForWrappedMixedLines()
    {
        BufferLine first = CreateMixedLine();
        BufferLine second = CreateMixedLine();
        second.IsWrapped = true;
        Assert.Equal([5, 4, 3], BufferReflow.GetNewLineLengths([first, second], 6, 5));
        Assert.Equal([3, 4, 4, 1], BufferReflow.GetNewLineLengths([first, second], 6, 4));
        Assert.Equal([3, 3, 3, 3], BufferReflow.GetNewLineLengths([first, second], 6, 3));
        Assert.Equal([1, 2, 2, 2, 2, 2, 1], BufferReflow.GetNewLineLengths([first, second], 6, 2));
    }

    [UpstreamFact("XTJS-0124", "BufferReflow reflowSmallerGetNewLineLengths should work on lines ending in null space")]
    public void Smaller_WorksOnLinesEndingInNullSpace()
    {
        BufferLine line = CreateWideLine("汉语", 5);
        Assert.Equal("汉语", line.TranslateToString(true));
        Assert.Equal("汉语 ", line.TranslateToString());
        Assert.Equal([2, 2], BufferReflow.GetNewLineLengths([line], 4, 3));
        Assert.Equal([2, 2], BufferReflow.GetNewLineLengths([line], 4, 2));
    }

    [UpstreamFact("XTJS-0125", "BufferReflow reflowLargerGetLinesToRemove should skip reflow when the cursor is in a wrapped block and reflowCursorLine is false")]
    public void Larger_SkipsCursorWrappedBlockUnlessEnabled()
    {
        List<BufferLine> skippedLines = CreateWrappedLines("abcde");
        List<BufferLine> reflowedLines = CreateWrappedLines("abcde");
        Assert.Empty(BufferReflow.GetLinesToRemove(skippedLines, 1, 5, 2, Blank, false));
        Assert.NotEmpty(BufferReflow.GetLinesToRemove(reflowedLines, 1, 5, 2, Blank, true));
    }

    [UpstreamFact("XTJS-0126", "BufferReflow reflowLargerGetLinesToRemove should reflow wrapped blocks when the cursor is outside the block")]
    public void Larger_ReflowsWrappedBlocksOutsideCursor()
    {
        List<BufferLine> lines = CreateWrappedLines("abcde");
        Assert.NotEmpty(BufferReflow.GetLinesToRemove(lines, 1, 5, 10, Blank, false));
    }

    private static BufferLine CreateWideLine(string text, int? columns = null)
    {
        var runes = text.EnumerateRunes().ToArray();
        var line = new BufferLine(columns ?? runes.Length * 2, CellStyle.Default);
        for (int index = 0; index < runes.Length; index++)
        {
            line.SetCell(index * 2, CellData.FromRune(runes[index], 2, CellStyle.Default));
            line.SetCell(index * 2 + 1, new CellData { Width = 0, Style = CellStyle.Default });
        }
        return line;
    }

    private static BufferLine CreateMixedLine()
    {
        var line = new BufferLine(6, CellStyle.Default);
        line.SetCell(0, CellData.FromText("a", 1, CellStyle.Default));
        line.SetCell(1, CellData.FromText("汉", 2, CellStyle.Default));
        line.SetCell(2, new CellData { Width = 0, Style = CellStyle.Default });
        line.SetCell(3, CellData.FromText("语", 2, CellStyle.Default));
        line.SetCell(4, new CellData { Width = 0, Style = CellStyle.Default });
        line.SetCell(5, CellData.FromText("b", 1, CellStyle.Default));
        return line;
    }

    private static List<BufferLine> CreateWrappedLines(string text)
    {
        var lines = new List<BufferLine>();
        for (int index = 0; index < text.Length; index++)
        {
            var line = new BufferLine(1, CellStyle.Default, index > 0);
            line.SetCell(0, CellData.FromText(text[index].ToString(), 1, CellStyle.Default));
            lines.Add(line);
        }
        return lines;
    }
}

using System.Text;
using XtermSharp.Internal;

namespace XtermSharp.Tests.Common;

public sealed class WindowsModeTests
{
    [UpstreamFact("XTJS-1314", "WindowsMode updateWindowsModeWrappedState should mark the next line wrapped when the previous line ends in a non-whitespace character")]
    public void Marks_next_line_wrapped_after_non_whitespace()
    {
        BufferLine previous = FilledLine('a');
        var next = new BufferLine(10, CellStyle.Default);

        WindowsMode.UpdateWrappedState(previous, next);

        Assert.True(next.IsWrapped);
    }

    [UpstreamFact("XTJS-1315", "WindowsMode updateWindowsModeWrappedState should not mark the next line wrapped when the previous line ends in whitespace")]
    public void Does_not_mark_next_line_wrapped_after_whitespace()
    {
        BufferLine previous = FilledLine('a');
        previous[previous.Length - 1] = CellData.FromRune(new Rune(' '), 1, CellStyle.Default);
        var next = new BufferLine(10, CellStyle.Default);

        WindowsMode.UpdateWrappedState(previous, next);

        Assert.False(next.IsWrapped);
    }

    [UpstreamFact("XTJS-1316", "WindowsMode updateWindowsModeWrappedState should not mark the next line wrapped when the previous line ends in a null cell")]
    public void Does_not_mark_next_line_wrapped_after_null_cell()
    {
        var previous = new BufferLine(10, CellStyle.Default);
        var next = new BufferLine(10, CellStyle.Default);

        WindowsMode.UpdateWrappedState(previous, next);

        Assert.False(next.IsWrapped);
    }

    private static BufferLine FilledLine(char value)
    {
        var line = new BufferLine(10, CellStyle.Default);
        for (int i = 0; i < line.Length; i++)
        {
            line[i] = CellData.FromRune(new Rune(value), 1, CellStyle.Default);
        }
        return line;
    }
}

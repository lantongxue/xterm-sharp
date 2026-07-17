using System.Text;

namespace XtermSharp.Addons.Search;

internal sealed record SearchLineCacheEntry(string Text, int[] Offsets);

internal sealed class SearchLineCache
{
    private long _revision = -1;
    private TerminalBufferSnapshot? _buffer;
    private SearchLineCacheEntry?[] _lines = [];

    public void Initialize(TerminalSnapshot snapshot)
    {
        if (_revision == snapshot.Revision &&
            ReferenceEquals(_buffer, snapshot.ActiveBuffer) &&
            _lines.Length == snapshot.ActiveBuffer.Lines.Length)
        {
            return;
        }
        _revision = snapshot.Revision;
        _buffer = snapshot.ActiveBuffer;
        _lines = new SearchLineCacheEntry?[snapshot.ActiveBuffer.Lines.Length];
    }

    public SearchLineCacheEntry GetLine(TerminalSnapshot snapshot, int row)
    {
        Initialize(snapshot);
        if ((uint)row >= (uint)_lines.Length)
        {
            return new SearchLineCacheEntry(string.Empty, [0]);
        }
        return _lines[row] ??= TranslateBufferLineToStringWithWrap(snapshot, row, trimRight: true);
    }

    internal static SearchLineCacheEntry TranslateBufferLineToStringWithWrap(
        TerminalSnapshot snapshot,
        int lineIndex,
        bool trimRight)
    {
        var text = new StringBuilder();
        var offsets = new List<int> { 0 };
        TerminalLineSnapshot? line = snapshot.ActiveBuffer.GetLine(lineIndex);
        while (line is not null)
        {
            TerminalLineSnapshot? nextLine = snapshot.ActiveBuffer.GetLine(lineIndex + 1);
            bool wrapsToNext = nextLine?.IsWrapped == true;
            string value = line.TranslateToString(!wrapsToNext && trimRight);
            if (wrapsToNext && nextLine is not null && line.Cells.Length != 0)
            {
                TerminalCellSnapshot lastCell = line.Cells[^1];
                bool lastCellIsNull = lastCell.CodePoint == 0 && lastCell.Width == 1;
                if (lastCellIsNull && nextLine.GetCell(0) is TerminalCellSnapshot { Width: 2 } && value.Length != 0)
                {
                    value = value[..^1];
                }
            }
            text.Append(value);
            if (!wrapsToNext)
            {
                break;
            }
            offsets.Add(text.Length);
            lineIndex++;
            line = nextLine;
        }
        return new SearchLineCacheEntry(text.ToString(), offsets.ToArray());
    }
}

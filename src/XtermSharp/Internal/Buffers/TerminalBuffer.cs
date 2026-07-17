using System.Collections.Immutable;

namespace XtermSharp.Internal;

internal sealed class TerminalBuffer : IDisposable
{
    private readonly List<BufferLine> _lines = [];
    private readonly List<TerminalMarker> _markers = [];
    private readonly BufferLineStringCache _stringCache = new();
    private int _columns;
    private int _rows;
    private int _scrollback;
    private int _cursorX;
    private bool _wrapPending;
    private bool _logicalRightMargin;

    public TerminalBuffer(TerminalBufferKind kind, int columns, int rows, int scrollback, CellStyle eraseStyle)
    {
        Kind = kind;
        _columns = columns;
        _rows = rows;
        _scrollback = kind == TerminalBufferKind.Normal ? scrollback : 0;
        ScrollBottom = rows - 1;
        for (int i = 0; i < rows; i++)
        {
            _lines.Add(new BufferLine(columns, eraseStyle, stringCache: _stringCache));
        }
    }

    public TerminalBufferKind Kind { get; }
    public int CursorX
    {
        get => _cursorX;
        set
        {
            _cursorX = value;
            _logicalRightMargin = false;
        }
    }
    public int LogicalCursorX => _wrapPending || _logicalRightMargin ? _columns : _cursorX;
    public int CursorY { get; set; }
    public int YBase { get; internal set; }
    public int YDisp { get; internal set; }
    public int ScrollTop { get; set; }
    public int ScrollBottom { get; set; }
    public int SavedCursorX { get; set; }
    public int SavedCursorY { get; set; }
    public CellStyle SavedStyle { get; set; } = CellStyle.Default;
    public bool SavedOriginMode { get; set; }
    public bool SavedWraparoundMode { get; set; } = true;
    public CharsetState? SavedCharsetState { get; set; }
    public bool WrapPending
    {
        get => _wrapPending;
        set
        {
            _wrapPending = value;
            _logicalRightMargin = false;
        }
    }
    public int LineCount => _lines.Count;
    public int Columns => _columns;
    public int Rows => _rows;
    public int Scrollback => _scrollback;
    public int MaximumLineCount => _rows + _scrollback;
    public IList<BufferLine> Lines => _lines;
    public IReadOnlyList<TerminalMarker> Markers => _markers;
    public BufferLineStringCache StringCache => _stringCache;

    public BufferLine CursorLine => _lines[YBase + CursorY];

    public void CancelWrapPendingPreservingLogicalCursor()
    {
        bool atRightMargin = _wrapPending || _logicalRightMargin;
        _wrapPending = false;
        _logicalRightMargin = atRightMargin;
    }

    public BufferLine GetViewportLine(int row) => _lines[YBase + row];

    public BufferLine GetLine(int row) => _lines[row];

    public void SetLine(int row, BufferLine line) => _lines[row] = line;

    public void FillViewportRows(CellStyle eraseStyle)
    {
        while (_lines.Count < _rows)
        {
            _lines.Add(new BufferLine(_columns, eraseStyle, stringCache: _stringCache));
        }
    }

    public void Reset(CellStyle eraseStyle)
    {
        _stringCache.Clear();
        DisposeMarkers();
        _lines.Clear();
        for (int i = 0; i < _rows; i++)
        {
            _lines.Add(new BufferLine(_columns, eraseStyle, stringCache: _stringCache));
        }
        CursorX = 0;
        CursorY = 0;
        YBase = 0;
        YDisp = 0;
        ScrollTop = 0;
        ScrollBottom = _rows - 1;
        SavedCursorX = 0;
        SavedCursorY = 0;
        SavedStyle = CellStyle.Default;
        SavedOriginMode = false;
        SavedWraparoundMode = true;
        SavedCharsetState = null;
        WrapPending = false;
    }

    public void ScrollLines(int amount)
    {
        YDisp = Math.Clamp(YDisp + amount, 0, YBase);
    }

    public void ScrollTo(int line) => YDisp = Math.Clamp(line, 0, YBase);

    public void ScrollToBottom() => YDisp = YBase;

    public void ClearScrollback()
    {
        if (YBase == 0)
        {
            return;
        }
        int removed = YBase;
        _lines.RemoveRange(0, removed);
        TrimMarkers(removed);
        YBase = 0;
        YDisp = 0;
    }

    public void Clear(CellStyle eraseStyle)
    {
        _stringCache.Clear();
        DisposeMarkers();
        BufferLine cursorLine = CursorLine;
        _lines.Clear();
        _lines.Add(cursorLine);
        cursorLine.IsWrapped = false;
        while (_lines.Count < _rows)
        {
            _lines.Add(new BufferLine(_columns, eraseStyle, stringCache: _stringCache));
        }
        CursorX = 0;
        CursorY = 0;
        YBase = 0;
        YDisp = 0;
        ScrollTop = 0;
        ScrollBottom = _rows - 1;
        WrapPending = false;
    }

    public void ScrollUp(int count, CellStyle eraseStyle)
    {
        for (int n = 0; n < count; n++)
        {
            if (ScrollTop == 0 && ScrollBottom == _rows - 1)
            {
                if (Kind == TerminalBufferKind.Normal)
                {
                    bool wasAtBottom = YDisp == YBase;
                    _lines.Add(new BufferLine(_columns, eraseStyle, stringCache: _stringCache));
                    YBase++;
                    if (wasAtBottom)
                    {
                        YDisp = YBase;
                    }
                    TrimScrollback();
                }
                else
                {
                    _lines.RemoveAt(0);
                    TrimMarkers(1);
                    _lines.Add(new BufferLine(_columns, eraseStyle, stringCache: _stringCache));
                }
            }
            else
            {
                int start = YBase + ScrollTop;
                int end = YBase + ScrollBottom;
                _lines.RemoveAt(start);
                DeleteMarkers(start, 1);
                _lines.Insert(end, new BufferLine(_columns, eraseStyle, stringCache: _stringCache));
                InsertMarkers(end, 1);
            }
        }
    }

    public void ScrollDown(int count, CellStyle eraseStyle)
    {
        for (int n = 0; n < count; n++)
        {
            int start = YBase + ScrollTop;
            int end = YBase + ScrollBottom;
            _lines.RemoveAt(end);
            DeleteMarkers(end, 1);
            _lines.Insert(start, new BufferLine(_columns, eraseStyle, stringCache: _stringCache));
            InsertMarkers(start, 1);
        }
    }

    public void InsertLines(int count, CellStyle eraseStyle)
    {
        if (CursorY < ScrollTop || CursorY > ScrollBottom)
        {
            return;
        }
        count = Math.Min(count, ScrollBottom - CursorY + 1);
        int cursorIndex = YBase + CursorY;
        int bottomIndex = YBase + ScrollBottom;
        for (int i = 0; i < count; i++)
        {
            _lines.RemoveAt(bottomIndex);
            DeleteMarkers(bottomIndex, 1);
            _lines.Insert(cursorIndex, new BufferLine(_columns, eraseStyle, stringCache: _stringCache));
            InsertMarkers(cursorIndex, 1);
        }
    }

    public void DeleteLines(int count, CellStyle eraseStyle)
    {
        if (CursorY < ScrollTop || CursorY > ScrollBottom)
        {
            return;
        }
        count = Math.Min(count, ScrollBottom - CursorY + 1);
        int cursorIndex = YBase + CursorY;
        int bottomIndex = YBase + ScrollBottom;
        for (int i = 0; i < count; i++)
        {
            _lines.RemoveAt(cursorIndex);
            DeleteMarkers(cursorIndex, 1);
            _lines.Insert(bottomIndex, new BufferLine(_columns, eraseStyle, stringCache: _stringCache));
            InsertMarkers(bottomIndex, 1);
        }
    }

    public void Resize(
        int columns,
        int rows,
        int scrollback,
        CellStyle eraseStyle,
        BufferResizeOptions resizeOptions = default)
    {
        _stringCache.Clear();
        int oldColumns = _columns;
        int oldRows = _rows;
        _scrollback = Kind == TerminalBufferKind.Normal ? scrollback : 0;
        int maximum = rows + _scrollback;

        int addToCursorY = 0;
        if (rows > oldRows)
        {
            for (int row = oldRows; row < rows; row++)
            {
                if (_lines.Count >= rows + YBase)
                {
                    continue;
                }
                if (resizeOptions.IsWindowsPty)
                {
                    _lines.Add(new BufferLine(columns, eraseStyle, stringCache: _stringCache));
                }
                else if (YBase > 0 && _lines.Count <= YBase + CursorY + addToCursorY + 1)
                {
                    YBase--;
                    addToCursorY++;
                    if (YDisp > 0)
                    {
                        YDisp--;
                    }
                }
                else
                {
                    _lines.Add(new BufferLine(columns, eraseStyle, stringCache: _stringCache));
                }
            }
        }
        else if (rows < oldRows)
        {
            for (int row = oldRows; row > rows; row--)
            {
                if (_lines.Count <= rows + YBase)
                {
                    continue;
                }
                if (_lines.Count > YBase + CursorY + 1)
                {
                    int last = _lines.Count - 1;
                    _lines.RemoveAt(last);
                    DeleteMarkers(last, 1);
                }
                else
                {
                    YBase++;
                    YDisp++;
                }
            }
        }

        if (_lines.Count > maximum)
        {
            int amount = _lines.Count - maximum;
            _lines.RemoveRange(0, amount);
            TrimMarkers(amount);
            YBase = Math.Max(0, YBase - amount);
            YDisp = Math.Max(0, YDisp - amount);
        }

        CursorX = Math.Min(CursorX, columns - 1);
        CursorY = Math.Min(CursorY, rows - 1);
        CursorY = Math.Min(rows - 1, CursorY + addToCursorY);
        SavedCursorX = Math.Min(SavedCursorX, columns - 1);
        ScrollTop = 0;
        ScrollBottom = rows - 1;

        bool reflowEnabled = Kind == TerminalBufferKind.Normal &&
            (!resizeOptions.IsWindowsPty || resizeOptions.WindowsBuildNumber >= 21376);
        bool useSingleColumnTruncation = columns == 1 && ContainsNonNarrowCells();
        if (columns != oldColumns)
        {
            if (reflowEnabled && !useSingleColumnTruncation)
            {
                Reflow(columns, rows, eraseStyle, resizeOptions.ReflowCursorLine);
            }
            else if (useSingleColumnTruncation)
            {
                ResizeToSingleColumn(eraseStyle);
            }
            else
            {
                foreach (BufferLine line in _lines)
                {
                    line.Resize(columns, eraseStyle);
                }
            }
        }
        else if (useSingleColumnTruncation)
        {
            ResizeToSingleColumn(eraseStyle);
        }
        else if (WrapPending && columns > oldColumns)
        {
            CursorX = oldColumns;
            WrapPending = false;
        }

        _columns = columns;
        _rows = rows;
        CursorX = Math.Clamp(CursorX, 0, columns - 1);
        CursorY = Math.Clamp(CursorY, 0, rows - 1);
        EnsureViewport(eraseStyle);
        TrimScrollback();
        if (_lines.Count > 0)
        {
            CursorY = Math.Min(CursorY, Math.Max(0, _lines.Count - YBase - 1));
        }
    }

    public TerminalBufferSnapshot CreateSnapshot(SnapshotScope scope)
    {
        int start = scope == SnapshotScope.Viewport ? YDisp : 0;
        int count = scope == SnapshotScope.Viewport ? _rows : _lines.Count;
        count = Math.Min(count, _lines.Count - start);
        var lines = ImmutableArray.CreateBuilder<TerminalLineSnapshot>(count);
        for (int i = 0; i < count; i++)
        {
            lines.Add(_lines[start + i].CreateSnapshot());
        }
        return new TerminalBufferSnapshot(Kind, LogicalCursorX, CursorY, YDisp, YBase, lines.MoveToImmutable());
    }

    public TerminalMarker AddMarker(int line)
    {
        var marker = new TerminalMarker(line);
        marker.Disposed += RemoveMarker;
        _markers.Add(marker);
        return marker;
    }

    public (int First, int Last) GetWrappedRangeForLine(int row)
    {
        if ((uint)row >= (uint)_lines.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(row));
        }
        int first = row;
        int last = row;
        while (first > 0 && _lines[first].IsWrapped)
        {
            first--;
        }
        while (last + 1 < _lines.Count && _lines[last + 1].IsWrapped)
        {
            last++;
        }
        return (first, last);
    }

    public string TranslateBufferLineToString(
        int lineIndex,
        bool trimRight,
        int startColumn = 0,
        int? endColumn = null)
    {
        if ((uint)lineIndex >= (uint)_lines.Count)
        {
            return string.Empty;
        }
        return _lines[lineIndex].TranslateToString(trimRight, startColumn, endColumn);
    }

    public void ClearAllMarkers() => DisposeMarkers();

    public void NotifyTrim(int amount)
    {
        if (amount > 0)
        {
            TrimMarkers(amount);
        }
    }

    public void CleanupMemory()
    {
        foreach (BufferLine line in _lines)
        {
            line.CleanupMemory();
        }
    }

    public void Dispose()
    {
        DisposeMarkers();
        _stringCache.Dispose();
        _lines.Clear();
    }

    private void EnsureViewport(CellStyle eraseStyle)
    {
        while (_lines.Count < YBase + _rows)
        {
            _lines.Add(new BufferLine(_columns, eraseStyle, stringCache: _stringCache));
        }
    }

    private void TrimScrollback()
    {
        int maximum = _rows + _scrollback;
        int removed = 0;
        while (_lines.Count > maximum)
        {
            _lines.RemoveAt(0);
            removed++;
            YBase = Math.Max(0, YBase - 1);
            YDisp = Math.Max(0, YDisp - 1);
        }
        if (removed > 0)
        {
            TrimMarkers(removed);
        }
    }

    private void Reflow(int columns, int rows, CellStyle eraseStyle, bool reflowCursorLine)
    {
        int oldColumns = _columns;
        int oldCursorLine = YBase + CursorY;
        int oldViewport = YDisp;
        bool viewportAtBottom = YDisp == YBase;
        var reflowed = new List<BufferLine>(_lines.Count);
        Dictionary<TerminalMarker, int> originalMarkerLines = _markers.ToDictionary(marker => marker, marker => marker.Line);
        int mappedCursorLine = 0;
        int mappedCursorColumn = 0;
        bool mappedWrapPending = false;

        for (int lineIndex = 0; lineIndex < _lines.Count;)
        {
            int groupEnd = lineIndex + 1;
            while (groupEnd < _lines.Count && _lines[groupEnd].IsWrapped)
            {
                groupEnd++;
            }
            if (columns > oldColumns &&
                !reflowCursorLine &&
                groupEnd - lineIndex > 1 &&
                oldCursorLine >= lineIndex &&
                oldCursorLine < groupEnd)
            {
                int preservedGroupStart = reflowed.Count;
                for (int sourceLine = lineIndex; sourceLine < groupEnd; sourceLine++)
                {
                    BufferLine clone = _lines[sourceLine].Clone();
                    clone.Resize(columns, eraseStyle);
                    reflowed.Add(clone);
                    foreach (TerminalMarker marker in _markers)
                    {
                        if (originalMarkerLines[marker] == sourceLine)
                        {
                            marker.SetLine(preservedGroupStart + sourceLine - lineIndex);
                        }
                    }
                }
                mappedCursorLine = preservedGroupStart + oldCursorLine - lineIndex;
                mappedCursorColumn = WrapPending ? oldColumns : CursorX;
                mappedWrapPending = false;
                lineIndex = groupEnd;
                continue;
            }

            var cells = new List<CellData>();
            var groupMarkerOffsets = new Dictionary<TerminalMarker, int>();
            int groupCursorOffset = -1;
            do
            {
                BufferLine line = _lines[lineIndex];
                int markerOffset = cells.Count;
                foreach (TerminalMarker marker in _markers)
                {
                    if (originalMarkerLines[marker] == lineIndex)
                    {
                        groupMarkerOffsets[marker] = markerOffset;
                    }
                }
                bool hasWrappedSuccessor = lineIndex + 1 < _lines.Count && _lines[lineIndex + 1].IsWrapped;
                int length;
                if (hasWrappedSuccessor)
                {
                    bool endsInNull = !line.HasContent(oldColumns - 1) && line.GetWidth(oldColumns - 1) == 1;
                    bool nextStartsWide = _lines[lineIndex + 1].GetWidth(0) == 2;
                    length = endsInNull && nextStartsWide ? oldColumns - 1 : oldColumns;
                }
                else
                {
                    length = line.GetTrimmedLength();
                }
                if (lineIndex == oldCursorLine)
                {
                    int logicalColumn = WrapPending ? oldColumns : CursorX;
                    groupCursorOffset = cells.Count + logicalColumn;
                }
                cells.AddRange(line.CopyCells(length));
                lineIndex++;
            }
            while (lineIndex < _lines.Count && _lines[lineIndex].IsWrapped);

            int newGroupStart = reflowed.Count;
            if (cells.Count == 0)
            {
                reflowed.Add(new BufferLine(columns, eraseStyle, stringCache: _stringCache));
            }
            else
            {
                int source = 0;
                bool wrapped = false;
                while (source < cells.Count)
                {
                    var target = new BufferLine(columns, eraseStyle, wrapped, _stringCache);
                    int column = 0;
                    while (source < cells.Count && column < columns)
                    {
                        CellData cell = cells[source];
                        if (cell.Width == 0)
                        {
                            source++;
                            continue;
                        }
                        int width = Math.Max(1, (int)cell.Width);
                        if (width == 2 && column == columns - 1)
                        {
                            break;
                        }
                        target.SetCell(column, cell);
                        if (width == 2 && column + 1 < columns)
                        {
                            target.SetCell(column + 1, source + 1 < cells.Count && cells[source + 1].Width == 0
                                ? cells[source + 1]
                                : new CellData { Width = 0, Style = cell.Style });
                        }
                        source += width;
                        column += width;
                    }
                    reflowed.Add(target);
                    wrapped = true;
                }
            }

            if (groupCursorOffset >= 0)
            {
                if (groupCursorOffset > 0 && groupCursorOffset % columns == 0 && groupCursorOffset >= cells.Count)
                {
                    mappedCursorLine = newGroupStart + groupCursorOffset / columns - 1;
                    mappedCursorColumn = columns - 1;
                    mappedWrapPending = true;
                }
                else
                {
                    mappedCursorLine = newGroupStart + groupCursorOffset / columns;
                    mappedCursorColumn = groupCursorOffset % columns;
                    mappedWrapPending = false;
                }
                while (reflowed.Count <= mappedCursorLine)
                {
                    reflowed.Add(new BufferLine(columns, eraseStyle, reflowed.Count > newGroupStart, _stringCache));
                }
            }

            foreach ((TerminalMarker marker, int offset) in groupMarkerOffsets)
            {
                marker.SetLine(newGroupStart + offset / columns);
            }
        }

        _lines.Clear();
        _lines.AddRange(reflowed);
        while (_lines.Count > rows && _lines.Count - 1 > mappedCursorLine && _lines[^1].GetTrimmedLength() == 0)
        {
            _lines.RemoveAt(_lines.Count - 1);
        }
        while (_lines.Count < rows)
        {
            _lines.Add(new BufferLine(columns, eraseStyle, stringCache: _stringCache));
        }

        int maximum = rows + _scrollback;
        int removed = Math.Max(0, _lines.Count - maximum);
        if (removed > 0)
        {
            _lines.RemoveRange(0, removed);
            TrimMarkers(removed);
            mappedCursorLine = Math.Max(0, mappedCursorLine - removed);
        }

        YBase = Math.Max(0, _lines.Count - rows);
        YDisp = viewportAtBottom ? YBase : Math.Clamp(oldViewport, 0, YBase);
        CursorY = Math.Clamp(mappedCursorLine - YBase, 0, rows - 1);
        CursorX = Math.Clamp(mappedCursorColumn, 0, columns - 1);
        WrapPending = mappedWrapPending;
    }

    private bool ContainsNonNarrowCells()
    {
        foreach (BufferLine line in _lines)
        {
            for (int column = 0; column < line.Length; column++)
            {
                if (line.GetWidth(column) != 1)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void ResizeToSingleColumn(CellStyle eraseStyle)
    {
        CellData blank = CellData.Blank(eraseStyle);
        foreach (BufferLine line in _lines)
        {
            line.Resize(1, blank);
            if (line.GetWidth(0) != 1)
            {
                line.SetCell(0, blank);
            }
        }
    }

    private void InsertMarkers(int index, int count)
    {
        foreach (TerminalMarker marker in _markers.ToArray())
        {
            if (marker.Line >= index)
            {
                marker.SetLine(marker.Line + count);
            }
        }
    }

    private void DeleteMarkers(int index, int count)
    {
        int end = index + count;
        foreach (TerminalMarker marker in _markers.ToArray())
        {
            if (marker.Line >= index && marker.Line < end)
            {
                marker.Dispose();
            }
            else if (marker.Line >= end)
            {
                marker.SetLine(marker.Line - count);
            }
        }
    }

    private void TrimMarkers(int count)
    {
        foreach (TerminalMarker marker in _markers.ToArray())
        {
            int line = marker.Line - count;
            if (line < 0)
            {
                marker.Dispose();
            }
            else
            {
                marker.SetLine(line);
            }
        }
    }

    private void DisposeMarkers()
    {
        foreach (TerminalMarker marker in _markers.ToArray())
        {
            marker.Dispose();
        }
        _markers.Clear();
    }

    private void RemoveMarker(object? sender, EventArgs args)
    {
        if (sender is TerminalMarker marker)
        {
            marker.Disposed -= RemoveMarker;
            _markers.Remove(marker);
        }
    }
}

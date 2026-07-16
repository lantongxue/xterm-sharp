using System.Collections.Immutable;
using System.Text;

namespace XtermSharp.Internal;

internal sealed class BufferLine
{
    private CellData[] _cells;
    private readonly BufferLineStringCache? _stringCache;
    private string? _cachedString;
    private bool _cachedStringIsTrimmed;
    private TerminalLineSnapshot? _cachedSnapshot;
    private int _allocatedColumns;
    private bool _isWrapped;

    public BufferLine(int columns, CellStyle eraseStyle, bool isWrapped = false, BufferLineStringCache? stringCache = null)
        : this(columns, CellData.Blank(eraseStyle), isWrapped, stringCache)
    {
    }

    public BufferLine(int columns, CellData fillCell, bool isWrapped = false, BufferLineStringCache? stringCache = null)
    {
        _cells = new CellData[columns];
        _allocatedColumns = columns;
        _stringCache = stringCache;
        Fill(0, columns, fillCell);
        IsWrapped = isWrapped;
    }

    public bool IsWrapped
    {
        get => _isWrapped;
        set
        {
            if (_isWrapped == value)
            {
                return;
            }
            _isWrapped = value;
            _cachedSnapshot = null;
        }
    }
    public int Length => _cells.Length;
    public int AllocatedColumns => _allocatedColumns;
    public string? CachedString => _cachedString;
    public bool IsCachedStringTrimmed => _cachedStringIsTrimmed;

    public CellData this[int index]
    {
        get => _cells[index];
        set => SetCell(index, value);
    }

    public CellData GetCell(int index) => _cells[index];

    public void SetCell(int index, CellData cell)
    {
        InvalidateStringCache();
        _cells[index] = cell;
    }

    public byte GetWidth(int index) => _cells[index].Width;

    public bool HasContent(int index) => _cells[index].HasContent;

    public bool IsProtected(int index) => _cells[index].Style.IsProtected;

    public bool Resize(int columns, CellStyle eraseStyle) => Resize(columns, CellData.Blank(eraseStyle));

    public bool Resize(int columns, CellData fillCell)
    {
        InvalidateStringCache();
        int oldLength = _cells.Length;
        Array.Resize(ref _cells, columns);
        if (columns > oldLength)
        {
            Fill(oldLength, columns, fillCell);
            _allocatedColumns = Math.Max(_allocatedColumns, columns);
        }
        return columns * 2 < _allocatedColumns;
    }

    public int CleanupMemory()
    {
        if (_cells.Length * 2 >= _allocatedColumns)
        {
            return 0;
        }
        _cells = _cells.ToArray();
        _allocatedColumns = _cells.Length;
        return 1;
    }

    public void Fill(int start, int end, CellStyle eraseStyle)
        => Fill(start, end, CellData.Blank(eraseStyle));

    public void Fill(int start, int end, CellData fillCell)
    {
        InvalidateStringCache();
        start = Math.Clamp(start, 0, _cells.Length);
        end = Math.Clamp(end, start, _cells.Length);
        for (int i = start; i < end; i++)
        {
            _cells[i] = fillCell;
        }
    }

    public void Fill(CellData fillCell, bool respectProtected = false)
    {
        InvalidateStringCache();
        for (int index = 0; index < _cells.Length; index++)
        {
            if (!respectProtected || !_cells[index].Style.IsProtected)
            {
                _cells[index] = fillCell;
            }
        }
    }

    public void FillUnprotected(int start, int end, CellStyle eraseStyle)
    {
        InvalidateStringCache();
        start = Math.Clamp(start, 0, _cells.Length);
        end = Math.Clamp(end, start, _cells.Length);
        CellData blank = CellData.Blank(eraseStyle);
        for (int index = start; index < end; index++)
        {
            if (_cells[index].Style.IsProtected)
            {
                continue;
            }
            _cells[index] = blank;
        }
    }

    public void InsertCells(int column, int count, CellStyle eraseStyle)
        => InsertCells(column, count, CellData.Blank(eraseStyle));

    public void InsertCells(int column, int count, CellData fillCell)
    {
        InvalidateStringCache();
        if (_cells.Length == 0 || count <= 0)
        {
            return;
        }
        column %= _cells.Length;
        if (column > 0 && _cells[column - 1].Width == 2)
        {
            _cells[column - 1] = EmptyWithWidthOne(fillCell);
        }
        count = Math.Min(count, _cells.Length - column);
        Array.Copy(_cells, column, _cells, column + count, _cells.Length - column - count);
        Fill(column, column + count, fillCell);
        if (_cells[^1].Width == 2)
        {
            _cells[^1] = EmptyWithWidthOne(fillCell);
        }
    }

    public void DeleteCells(int column, int count, CellStyle eraseStyle)
        => DeleteCells(column, count, CellData.Blank(eraseStyle));

    public void DeleteCells(int column, int count, CellData fillCell)
    {
        InvalidateStringCache();
        if (_cells.Length == 0 || count <= 0)
        {
            return;
        }
        column %= _cells.Length;
        count = Math.Min(count, _cells.Length - column);
        Array.Copy(_cells, column + count, _cells, column, _cells.Length - column - count);
        Fill(_cells.Length - count, _cells.Length, fillCell);
        if (column > 0 && _cells[column - 1].Width == 2)
        {
            _cells[column - 1] = EmptyWithWidthOne(fillCell);
        }
        if (column < _cells.Length && _cells[column].Width == 0 && !_cells[column].HasContent)
        {
            _cells[column] = EmptyWithWidthOne(fillCell);
        }
    }

    public void ReplaceCells(int start, int end, CellData fillCell, bool respectProtected = false)
    {
        InvalidateStringCache();
        start = Math.Clamp(start, 0, _cells.Length);
        end = Math.Clamp(end, start, _cells.Length);
        if (start > 0 && _cells[start - 1].Width == 2 && (!respectProtected || !_cells[start - 1].Style.IsProtected))
        {
            _cells[start - 1] = EmptyWithWidthOne(fillCell);
        }
        if (end < _cells.Length && end > 0 && _cells[end - 1].Width == 2 && (!respectProtected || !_cells[end].Style.IsProtected))
        {
            _cells[end] = EmptyWithWidthOne(fillCell);
        }
        for (int index = start; index < end; index++)
        {
            if (!respectProtected || !_cells[index].Style.IsProtected)
            {
                _cells[index] = fillCell;
            }
        }
    }

    public void AppendCombining(int column, Rune rune)
    {
        if ((uint)column >= (uint)_cells.Length)
        {
            return;
        }
        InvalidateStringCache();
        ref CellData cell = ref _cells[column];
        string baseText = cell.GetText();
        if (baseText.Length == 0)
        {
            cell = CellData.FromRune(rune, 1, cell.Style);
            return;
        }
        cell.CombinedText = string.Concat(baseText, rune.ToString());
        cell.CodePoint = rune.Value;
    }

    public void AddCodePointToCell(int column, int codePoint, byte width)
    {
        if ((uint)column >= (uint)_cells.Length)
        {
            return;
        }
        AppendCombining(column, new Rune(codePoint));
        if (width != 0)
        {
            _cells[column].Width = width;
        }
    }

    public void SetCellFromCodePoint(int column, int codePoint, byte width, CellStyle style)
    {
        InvalidateStringCache();
        _cells[column] = codePoint == 0
            ? new CellData { Width = width, Style = style }
            : CellData.FromRune(new Rune(codePoint), width, style);
    }

    public int GetTrimmedLength()
    {
        for (int index = _cells.Length - 1; index >= 0; index--)
        {
            ref CellData cell = ref _cells[index];
            if (cell.HasContent)
            {
                return index + cell.Width;
            }
        }
        return 0;
    }

    public CellData[] CopyCells(int length)
    {
        length = Math.Clamp(length, 0, _cells.Length);
        var result = new CellData[length];
        Array.Copy(_cells, result, length);
        return result;
    }

    public void CopyCellsFrom(BufferLine source, int sourceColumn, int destinationColumn, int length, bool reverse)
    {
        ArgumentNullException.ThrowIfNull(source);
        InvalidateStringCache();
        if (reverse)
        {
            for (int offset = length - 1; offset >= 0; offset--)
            {
                _cells[destinationColumn + offset] = source._cells[sourceColumn + offset];
            }
        }
        else
        {
            for (int offset = 0; offset < length; offset++)
            {
                _cells[destinationColumn + offset] = source._cells[sourceColumn + offset];
            }
        }
    }

    public BufferLine Clone()
    {
        var clone = new BufferLine(0, CellStyle.Default, IsWrapped, _stringCache)
        {
            _cells = _cells.ToArray(),
            _allocatedColumns = _allocatedColumns
        };
        return clone;
    }

    public void CopyFrom(BufferLine source)
    {
        ArgumentNullException.ThrowIfNull(source);
        InvalidateStringCache();
        _cells = source._cells.ToArray();
        _allocatedColumns = source._allocatedColumns;
        IsWrapped = source.IsWrapped;
    }

    public string TranslateToString(
        bool trimRight = false,
        int? startColumn = null,
        int? endColumn = null,
        List<int>? outputColumns = null)
    {
        bool canonical = (startColumn is null or 0) && endColumn is null && outputColumns is null;
        if (canonical && _cachedString is not null)
        {
            if (trimRight)
            {
                return _cachedStringIsTrimmed ? _cachedString : _cachedString.TrimEnd();
            }
            if (!_cachedStringIsTrimmed)
            {
                return _cachedString;
            }
        }

        int start = Math.Clamp(startColumn ?? 0, 0, _cells.Length);
        int end = Math.Clamp(endColumn ?? _cells.Length, start, _cells.Length);
        if (trimRight)
        {
            end = Math.Min(end, GetTrimmedLength());
        }
        outputColumns?.Clear();
        var builder = new StringBuilder();
        while (start < end)
        {
            CellData cell = _cells[start];
            string text = cell.GetText();
            if (text.Length == 0)
            {
                text = " ";
            }
            builder.Append(text);
            if (outputColumns is not null)
            {
                for (int index = 0; index < text.Length; index++)
                {
                    outputColumns.Add(start);
                }
            }
            start += cell.Width == 0 ? 1 : cell.Width;
        }
        outputColumns?.Add(start);
        string result = builder.ToString();
        if (canonical)
        {
            _cachedString = result;
            _cachedStringIsTrimmed = trimRight;
            _stringCache?.Touch(this);
        }
        return result;
    }

    internal void ClearCachedString()
    {
        _cachedString = null;
        _cachedStringIsTrimmed = false;
    }

    internal void SetCachedString(string? value, bool isTrimmed)
    {
        _cachedString = value;
        _cachedStringIsTrimmed = value is not null && isTrimmed;
    }

    public TerminalLineSnapshot CreateSnapshot()
    {
        if (_cachedSnapshot is not null)
        {
            return _cachedSnapshot;
        }
        var cells = ImmutableArray.CreateBuilder<TerminalCellSnapshot>(_cells.Length);
        foreach (CellData cell in _cells)
        {
            cells.Add(cell.ToSnapshot());
        }
        _cachedSnapshot = new TerminalLineSnapshot(IsWrapped, cells.MoveToImmutable());
        return _cachedSnapshot;
    }

    private void InvalidateStringCache()
    {
        ClearCachedString();
        _cachedSnapshot = null;
    }

    private static CellData EmptyWithWidthOne(CellData fillCell) => new()
    {
        CodePoint = 0,
        CombinedText = null,
        Width = 1,
        Style = fillCell.Style,
        Extended = fillCell.Extended
    };
}

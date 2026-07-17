using System.Text.RegularExpressions;

namespace XtermSharp.Addons.Search;

internal sealed class SearchEngine(SearchLineCache lineCache)
{
    private const string NonWordCharacters = " ~!@#$%^&*()+`-=[]{}|\\;:\"',./<>?";

    public SearchResult? Find(
        TerminalSnapshot snapshot,
        string term,
        int startRow,
        int startColumn,
        SearchOptions? options = null)
    {
        if (term.Length == 0)
        {
            return null;
        }
        if (startColumn >= snapshot.Columns)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startColumn),
                $"Invalid col: {startColumn} to search in terminal of {snapshot.Columns} cols");
        }

        lineCache.Initialize(snapshot);
        var position = new SearchPosition(startRow, startColumn);
        SearchResult? result = FindInLine(snapshot, term, position, options);
        int bufferEnd = GetBufferEnd(snapshot);
        if (result is null)
        {
            for (int row = startRow + 1; row < bufferEnd; row++)
            {
                position.StartRow = row;
                position.StartColumn = 0;
                result = FindInLine(snapshot, term, position, options);
                if (result is not null)
                {
                    break;
                }
            }
        }
        return result;
    }

    public SearchResult? FindNextWithSelection(
        TerminalSnapshot snapshot,
        string term,
        SearchOptions? options,
        string? cachedSearchTerm,
        TerminalSelectionRange? previousSelection)
    {
        if (term.Length == 0)
        {
            return null;
        }

        int startColumn = 0;
        int startRow = 0;
        if (previousSelection is TerminalSelectionRange selection)
        {
            selection = selection.Normalize();
            if (string.Equals(cachedSearchTerm, term, StringComparison.Ordinal))
            {
                startColumn = selection.EndColumn;
                startRow = selection.EndLine;
            }
            else
            {
                startColumn = selection.StartColumn;
                startRow = selection.StartLine;
            }
        }

        lineCache.Initialize(snapshot);
        var position = new SearchPosition(startRow, startColumn);
        SearchResult? result = FindInLine(snapshot, term, position, options);
        int bufferEnd = GetBufferEnd(snapshot);
        if (result is null)
        {
            for (int row = startRow + 1; row < bufferEnd; row++)
            {
                position.StartRow = row;
                position.StartColumn = 0;
                result = FindInLine(snapshot, term, position, options);
                if (result is not null)
                {
                    break;
                }
            }
        }
        if (result is null && startRow != 0)
        {
            for (int row = 0; row < startRow; row++)
            {
                position.StartRow = row;
                position.StartColumn = 0;
                result = FindInLine(snapshot, term, position, options);
                if (result is not null)
                {
                    break;
                }
            }
        }
        if (result is null && previousSelection is TerminalSelectionRange previous)
        {
            previous = previous.Normalize();
            position.StartRow = previous.StartLine;
            position.StartColumn = 0;
            result = FindInLine(snapshot, term, position, options);
        }
        return result;
    }

    public SearchResult? FindPreviousWithSelection(
        TerminalSnapshot snapshot,
        string term,
        SearchOptions? options,
        string? cachedSearchTerm,
        TerminalSelectionRange? previousSelection)
    {
        if (term.Length == 0)
        {
            return null;
        }

        int bufferEnd = GetBufferEnd(snapshot);
        int startRow = bufferEnd - 1;
        var position = new SearchPosition(startRow, snapshot.Columns);
        SearchResult? result = null;
        if (previousSelection is TerminalSelectionRange selection)
        {
            selection = selection.Normalize();
            position.StartRow = startRow = selection.StartLine;
            position.StartColumn = selection.StartColumn;
            if (!string.Equals(cachedSearchTerm, term, StringComparison.Ordinal))
            {
                result = FindInLine(snapshot, term, position, options);
                if (result is null)
                {
                    position.StartRow = startRow = selection.EndLine;
                    position.StartColumn = selection.EndColumn;
                }
            }
        }

        result ??= FindInLine(snapshot, term, position, options, reverse: true);
        if (result is null)
        {
            position.StartColumn = Math.Max(position.StartColumn, snapshot.Columns);
            for (int row = startRow - 1; row >= 0; row--)
            {
                position.StartRow = row;
                result = FindInLine(snapshot, term, position, options, reverse: true);
                if (result is not null)
                {
                    break;
                }
            }
        }
        if (result is null && startRow != bufferEnd - 1)
        {
            for (int row = bufferEnd - 1; row >= startRow; row--)
            {
                position.StartRow = row;
                result = FindInLine(snapshot, term, position, options, reverse: true);
                if (result is not null)
                {
                    break;
                }
            }
        }
        return result;
    }

    private SearchResult? FindInLine(
        TerminalSnapshot snapshot,
        string term,
        SearchPosition position,
        SearchOptions? options,
        bool reverse = false)
    {
        int row = position.StartRow;
        int column = position.StartColumn;
        TerminalLineSnapshot? firstLine = snapshot.ActiveBuffer.GetLine(row);
        if (firstLine?.IsWrapped == true)
        {
            if (reverse)
            {
                position.StartColumn += snapshot.Columns;
                return null;
            }
            position.StartRow--;
            position.StartColumn += snapshot.Columns;
            return FindInLine(snapshot, term, position, options);
        }
        if (firstLine is null)
        {
            return null;
        }

        SearchLineCacheEntry entry = lineCache.GetLine(snapshot, row);
        string stringLine = entry.Text;
        int offset = BufferColumnsToStringOffset(snapshot, row, column);
        string searchTerm = term;
        string searchLine = stringLine;
        if (options?.Regex != true)
        {
            if (options?.CaseSensitive != true)
            {
                searchTerm = term.ToLowerInvariant();
                searchLine = stringLine.ToLowerInvariant();
            }
        }

        int resultIndex = -1;
        if (options?.Regex == true)
        {
            RegexOptions regexOptions = RegexOptions.CultureInvariant | RegexOptions.ECMAScript;
            if (!options.CaseSensitive)
            {
                regexOptions |= RegexOptions.IgnoreCase;
            }
            var regex = new Regex(searchTerm, regexOptions);
            if (reverse)
            {
                string prefix = searchLine[..Math.Clamp(offset, 0, searchLine.Length)];
                for (Match match = regex.Match(prefix); match.Success;)
                {
                    if (match.Length != 0)
                    {
                        resultIndex = match.Index;
                        term = match.Value;
                    }
                    int nextStart = match.Index + 1;
                    if (nextStart > prefix.Length)
                    {
                        break;
                    }
                    match = regex.Match(prefix, nextStart);
                }
            }
            else if (offset <= searchLine.Length)
            {
                Match match = regex.Match(searchLine, Math.Max(0, offset));
                if (match.Success && match.Length != 0)
                {
                    resultIndex = match.Index;
                    term = match.Value;
                }
            }
        }
        else if (reverse)
        {
            int maxStart = Math.Min(offset - searchTerm.Length, searchLine.Length - searchTerm.Length);
            if (maxStart >= 0)
            {
                resultIndex = searchLine[..(maxStart + searchTerm.Length)]
                    .LastIndexOf(searchTerm, StringComparison.Ordinal);
            }
        }
        else
        {
            resultIndex = searchLine.IndexOf(searchTerm, Math.Max(0, offset), StringComparison.Ordinal);
        }

        if (resultIndex < 0 || options?.WholeWord == true && !IsWholeWord(resultIndex, searchLine, term))
        {
            return null;
        }

        int startRowOffset = 0;
        while (startRowOffset < entry.Offsets.Length - 1 && resultIndex >= entry.Offsets[startRowOffset + 1])
        {
            startRowOffset++;
        }
        int endRowOffset = startRowOffset;
        while (endRowOffset < entry.Offsets.Length - 1 &&
               resultIndex + term.Length >= entry.Offsets[endRowOffset + 1])
        {
            endRowOffset++;
        }
        int startColumnOffset = resultIndex - entry.Offsets[startRowOffset];
        int endColumnOffset = resultIndex + term.Length - entry.Offsets[endRowOffset];
        int startColumnIndex = StringLengthToBufferSize(snapshot, row + startRowOffset, startColumnOffset);
        int endColumnIndex = StringLengthToBufferSize(snapshot, row + endRowOffset, endColumnOffset);
        int size = endColumnIndex - startColumnIndex + snapshot.Columns * (endRowOffset - startRowOffset);
        return new SearchResult(term, startColumnIndex, row + startRowOffset, size);
    }

    private static bool IsWholeWord(int searchIndex, string line, string term) =>
        (searchIndex == 0 || NonWordCharacters.Contains(line[searchIndex - 1], StringComparison.Ordinal)) &&
        (searchIndex + term.Length == line.Length ||
         NonWordCharacters.Contains(line[searchIndex + term.Length], StringComparison.Ordinal));

    private static int StringLengthToBufferSize(TerminalSnapshot snapshot, int row, int offset)
    {
        TerminalLineSnapshot? line = snapshot.ActiveBuffer.GetLine(row);
        if (line is null)
        {
            return 0;
        }
        for (int index = 0; index < offset; index++)
        {
            TerminalCellSnapshot? cell = line.GetCell(index);
            if (cell is null)
            {
                break;
            }
            if (cell.Value.Text.Length > 1)
            {
                offset -= cell.Value.Text.Length - 1;
            }
            if (line.GetCell(index + 1) is TerminalCellSnapshot { Width: 0 })
            {
                offset++;
            }
        }
        return offset;
    }

    private static int BufferColumnsToStringOffset(TerminalSnapshot snapshot, int startRow, int columns)
    {
        int lineIndex = startRow;
        int offset = 0;
        TerminalLineSnapshot? line = snapshot.ActiveBuffer.GetLine(lineIndex);
        while (columns > 0 && line is not null)
        {
            for (int index = 0; index < columns && index < snapshot.Columns; index++)
            {
                TerminalCellSnapshot? cell = line.GetCell(index);
                if (cell is null)
                {
                    break;
                }
                if (cell.Value.Width != 0)
                {
                    offset += cell.Value.CodePoint == 0 ? 1 : cell.Value.Text.Length;
                }
            }
            lineIndex++;
            line = snapshot.ActiveBuffer.GetLine(lineIndex);
            if (line is { IsWrapped: false })
            {
                break;
            }
            columns -= snapshot.Columns;
        }
        return offset;
    }

    private static int GetBufferEnd(TerminalSnapshot snapshot) =>
        Math.Min(snapshot.ActiveBuffer.Lines.Length, snapshot.ActiveBuffer.BaseY + snapshot.Rows);

    private sealed class SearchPosition(int startRow, int startColumn)
    {
        public int StartRow { get; set; } = startRow;
        public int StartColumn { get; set; } = startColumn;
    }
}

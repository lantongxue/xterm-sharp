namespace XtermSharp.Internal.Buffers;

internal static class BufferReflow
{
    public static int[] GetNewLineLengths(IReadOnlyList<BufferLine> wrappedLines, int oldColumns, int newColumns)
    {
        ArgumentNullException.ThrowIfNull(wrappedLines);
        if (newColumns <= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(newColumns), "Reflow widths must be greater than one.");
        }

        var lengths = new List<int>();
        int cellsNeeded = 0;
        for (int index = 0; index < wrappedLines.Count; index++)
        {
            cellsNeeded += GetWrappedLineTrimmedLength(wrappedLines, index, oldColumns);
        }

        int sourceColumn = 0;
        int sourceLine = 0;
        int cellsAvailable = 0;
        while (cellsAvailable < cellsNeeded)
        {
            if (cellsNeeded - cellsAvailable < newColumns)
            {
                lengths.Add(cellsNeeded - cellsAvailable);
                break;
            }
            sourceColumn += newColumns;
            int oldTrimmedLength = GetWrappedLineTrimmedLength(wrappedLines, sourceLine, oldColumns);
            if (sourceColumn > oldTrimmedLength)
            {
                sourceColumn -= oldTrimmedLength;
                sourceLine++;
            }
            bool endsWithWide = wrappedLines[sourceLine].GetWidth(sourceColumn - 1) == 2;
            if (endsWithWide)
            {
                sourceColumn--;
            }
            int lineLength = endsWithWide ? newColumns - 1 : newColumns;
            lengths.Add(lineLength);
            cellsAvailable += lineLength;
        }
        return lengths.ToArray();
    }

    public static int[] GetLinesToRemove(
        IList<BufferLine> lines,
        int oldColumns,
        int newColumns,
        int absoluteCursorY,
        CellData blank,
        bool reflowCursorLine)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var removals = new List<int>();
        for (int y = 0; y < lines.Count - 1; y++)
        {
            int nextIndex = y + 1;
            if (!lines[nextIndex].IsWrapped)
            {
                continue;
            }
            var wrapped = new List<BufferLine> { lines[y] };
            while (nextIndex < lines.Count && lines[nextIndex].IsWrapped)
            {
                wrapped.Add(lines[nextIndex++]);
            }
            if (!reflowCursorLine && absoluteCursorY >= y && absoluteCursorY < nextIndex)
            {
                y += wrapped.Count - 1;
                continue;
            }

            // Buffer.Resize grows lines before invoking this helper. Keep the standalone helper
            // equally safe; JavaScript typed arrays silently ignore the out-of-range writes that
            // the upstream direct unit test otherwise relies on.
            foreach (BufferLine line in wrapped)
            {
                if (line.Length < newColumns)
                {
                    line.Resize(newColumns, blank);
                }
            }

            int destinationLine = 0;
            int destinationColumn = GetWrappedLineTrimmedLength(wrapped, 0, oldColumns);
            int sourceLine = 1;
            int sourceColumn = 0;
            while (sourceLine < wrapped.Count)
            {
                int sourceLength = GetWrappedLineTrimmedLength(wrapped, sourceLine, oldColumns);
                int count = Math.Min(sourceLength - sourceColumn, newColumns - destinationColumn);
                wrapped[destinationLine].CopyCellsFrom(wrapped[sourceLine], sourceColumn, destinationColumn, count, false);
                destinationColumn += count;
                if (destinationColumn == newColumns)
                {
                    destinationLine++;
                    destinationColumn = 0;
                }
                sourceColumn += count;
                if (sourceColumn == sourceLength)
                {
                    sourceLine++;
                    sourceColumn = 0;
                }
                if (destinationColumn == 0 && destinationLine != 0 && wrapped[destinationLine - 1].GetWidth(newColumns - 1) == 2)
                {
                    wrapped[destinationLine].CopyCellsFrom(wrapped[destinationLine - 1], newColumns - 1, 0, 1, false);
                    destinationColumn++;
                    wrapped[destinationLine - 1].SetCell(newColumns - 1, blank);
                }
            }
            wrapped[destinationLine].ReplaceCells(destinationColumn, newColumns, blank);

            int removeCount = 0;
            for (int index = wrapped.Count - 1; index > 0; index--)
            {
                if (index > destinationLine || wrapped[index].GetTrimmedLength() == 0)
                {
                    removeCount++;
                }
                else
                {
                    break;
                }
            }
            if (removeCount > 0)
            {
                removals.Add(y + wrapped.Count - removeCount);
                removals.Add(removeCount);
            }
            y += wrapped.Count - 1;
        }
        return removals.ToArray();
    }

    public static int GetWrappedLineTrimmedLength(IReadOnlyList<BufferLine> lines, int index, int columns)
    {
        if (index == lines.Count - 1)
        {
            return lines[index].GetTrimmedLength();
        }
        bool endsInNull = !lines[index].HasContent(columns - 1) && lines[index].GetWidth(columns - 1) == 1;
        bool followingStartsWide = lines[index + 1].GetWidth(0) == 2;
        return endsInNull && followingStartsWide ? columns - 1 : columns;
    }
}

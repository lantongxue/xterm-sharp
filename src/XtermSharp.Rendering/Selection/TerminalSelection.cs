namespace XtermSharp.Rendering.Selection;

public readonly record struct TerminalSelection(
    int StartColumn,
    int StartLine,
    int EndColumn,
    int EndLine,
    bool ColumnMode = false)
{
    public bool IsEmpty => StartColumn == EndColumn && StartLine == EndLine;

    public TerminalSelection Normalize()
    {
        if (StartLine < EndLine || StartLine == EndLine && StartColumn <= EndColumn)
        {
            return this;
        }
        return new TerminalSelection(EndColumn, EndLine, StartColumn, StartLine, ColumnMode);
    }

    public bool Contains(int column, int line)
    {
        TerminalSelection value = Normalize();
        if (value.IsEmpty || line < value.StartLine || line > value.EndLine)
        {
            return false;
        }
        if (value.ColumnMode)
        {
            int start = Math.Min(value.StartColumn, value.EndColumn);
            int end = Math.Max(value.StartColumn, value.EndColumn);
            return column >= start && column < end;
        }
        if (value.StartLine == value.EndLine)
        {
            return column >= value.StartColumn && column < value.EndColumn;
        }
        if (line == value.StartLine)
        {
            return column >= value.StartColumn;
        }
        if (line == value.EndLine)
        {
            return column < value.EndColumn;
        }
        return true;
    }
}

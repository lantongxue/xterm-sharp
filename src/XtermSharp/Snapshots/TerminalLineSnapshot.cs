using System.Collections.Immutable;

namespace XtermSharp;

public sealed record TerminalLineSnapshot(
    bool IsWrapped,
    ImmutableArray<TerminalCellSnapshot> Cells)
{
    public int Length => Cells.Length;

    public TerminalCellSnapshot? GetCell(int column) =>
        (uint)column < (uint)Cells.Length ? Cells[column] : null;

    public string TranslateToString(bool trimRight = false, int startColumn = 0, int? endColumn = null)
    {
        int start = Math.Clamp(startColumn, 0, Cells.Length);
        int end = Math.Clamp(endColumn ?? Cells.Length, start, Cells.Length);
        if (trimRight)
        {
            int trimmedLength = 0;
            for (int index = Cells.Length - 1; index >= 0; index--)
            {
                TerminalCellSnapshot cell = Cells[index];
                if (cell.CodePoint != 0 || cell.Text.Length != 0)
                {
                    trimmedLength = index + cell.Width;
                    break;
                }
            }
            end = Math.Min(end, trimmedLength);
        }
        var builder = new System.Text.StringBuilder();
        while (start < end)
        {
            TerminalCellSnapshot cell = Cells[start];
            builder.Append(cell.Text.Length == 0 ? " " : cell.Text);
            start += cell.Width == 0 ? 1 : cell.Width;
        }
        return builder.ToString();
    }
}

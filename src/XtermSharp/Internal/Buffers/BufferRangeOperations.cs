namespace XtermSharp.Internal.Buffers;

internal static class BufferRangeOperations
{
    public static int GetRangeLength(BufferRange range, int bufferColumns)
    {
        if (range.Start.Y > range.End.Y)
        {
            throw new ArgumentException(
                $"Buffer range end ({range.End.X}, {range.End.Y}) cannot be before start ({range.Start.X}, {range.Start.Y})",
                nameof(range));
        }
        return bufferColumns * (range.End.Y - range.Start.Y) + range.End.X - range.Start.X + 1;
    }
}

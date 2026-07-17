namespace XtermSharp.Internal;

internal readonly record struct WriteBufferChunk
{
    private WriteBufferChunk(string? text, ReadOnlyMemory<byte> bytes, bool isBytes)
    {
        Text = text;
        Bytes = bytes;
        IsBytes = isBytes;
    }

    public string? Text { get; }
    public ReadOnlyMemory<byte> Bytes { get; }
    public bool IsBytes { get; }
    public int Length => IsBytes ? Bytes.Length : Text?.Length ?? 0;

    public static WriteBufferChunk FromText(string value) => new(value, default, false);
    public static WriteBufferChunk FromBytes(ReadOnlyMemory<byte> value) => new(null, value.ToArray(), true);
}

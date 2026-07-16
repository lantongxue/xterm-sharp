using System.Buffers;
using System.Text;

namespace XtermSharp.Internal;

/// <summary>
/// Streaming UTF-8 decoder matching xterm.js: malformed sequences are discarded and
/// incomplete sequences are retained for the next input chunk.
/// </summary>
internal sealed class XtermUtf8Decoder
{
    private readonly Utf8ToUtf32 _decoder = new();

    public async ValueTask DecodeAsync(ReadOnlyMemory<byte> input, Func<Rune, ValueTask> emit)
    {
        uint[] codePoints = ArrayPool<uint>.Shared.Rent(Math.Max(1, input.Length));
        try
        {
            int count = _decoder.Decode(input.Span, codePoints);
            for (int index = 0; index < count; index++)
            {
                await emit(new Rune((int)codePoints[index])).ConfigureAwait(false);
            }
        }
        finally
        {
            ArrayPool<uint>.Shared.Return(codePoints);
        }
    }

    public void Reset() => _decoder.Clear();
}

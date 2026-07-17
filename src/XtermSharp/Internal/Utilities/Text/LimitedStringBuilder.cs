namespace XtermSharp.Internal.Utilities.Text;

/// <summary>
/// A string builder that clears its payload when a fixed limit is exceeded.
/// </summary>
internal sealed class LimitedStringBuilder
{
    private readonly StringBuilder _builder = new();

    public LimitedStringBuilder(int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(limit);
        Limit = limit;
    }

    public int Length => _builder.Length;

    public int Limit { get; }

    public void Reset() => _builder.Reset();

    public bool Append(string chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        if (chunk.Length > Limit - _builder.Length)
        {
            _builder.Reset();
            return true;
        }
        _builder.Append(chunk);
        return false;
    }

    public bool AppendUtf32(ReadOnlySpan<uint> data)
    {
        long appendLength = 0;
        foreach (uint codePoint in data)
        {
            appendLength += codePoint > 0xFFFF ? 2 : 1;
        }
        if (appendLength > Limit - _builder.Length)
        {
            _builder.Reset();
            return true;
        }

        foreach (uint value in data)
        {
            uint codePoint = value;
            if (codePoint > 0xFFFF)
            {
                codePoint -= 0x10000;
                _builder.Append((char)((codePoint >> 10) + 0xD800));
                _builder.Append((char)((codePoint % 0x400) + 0xDC00));
            }
            else
            {
                _builder.Append((char)codePoint);
            }
        }
        return false;
    }

    public override string ToString() => _builder.ToString();
}

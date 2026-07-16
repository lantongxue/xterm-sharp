namespace XtermSharp.Internal.Utilities;

/// <summary>
/// Accumulates string chunks without repeated whole-string concatenation.
/// </summary>
internal sealed class StringBuilder
{
    private const int MaximumRetainedCapacity = 4_096;
    private System.Text.StringBuilder _builder = new();

    public int Length => _builder.Length;

    public void Reset()
    {
        if (_builder.Capacity > MaximumRetainedCapacity)
        {
            _builder = new System.Text.StringBuilder();
            return;
        }
        _builder.Clear();
    }

    public void Append(string chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        _builder.Append(chunk);
    }

    public void Append(char value) => _builder.Append(value);

    public override string ToString() => _builder.ToString();
}

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

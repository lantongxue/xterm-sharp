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

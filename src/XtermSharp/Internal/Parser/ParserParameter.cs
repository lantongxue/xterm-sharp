using System.Collections.Immutable;

namespace XtermSharp.Internal.Parser;

internal readonly struct ParserParameter : IEquatable<ParserParameter>
{
    public ParserParameter(int value, ImmutableArray<int> subParameters)
    {
        Value = value;
        SubParameters = subParameters;
    }

    public ParserParameter(int value) : this(value, ImmutableArray<int>.Empty)
    {
    }

    public int Value { get; }
    public ImmutableArray<int> SubParameters { get; }

    public bool Equals(ParserParameter other) =>
        Value == other.Value && SubParameters.AsSpan().SequenceEqual(other.SubParameters.AsSpan());

    public override bool Equals(object? obj) => obj is ParserParameter other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Value);
        foreach (int value in SubParameters)
        {
            hash.Add(value);
        }
        return hash.ToHashCode();
    }

    public static bool operator ==(ParserParameter left, ParserParameter right) => left.Equals(right);
    public static bool operator !=(ParserParameter left, ParserParameter right) => !left.Equals(right);

    public override string ToString() => SubParameters.IsEmpty
        ? Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
        : $"{Value}:[{string.Join(',', SubParameters)}]";
}

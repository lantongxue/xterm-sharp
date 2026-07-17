using System.Collections.Immutable;
using XtermSharp.Internal;

namespace XtermSharp;

public sealed class CsiParameters
{
    private readonly ImmutableDictionary<int, ImmutableArray<int>> _subParameters;

    internal CsiParameters(ImmutableArray<int> values)
        : this(values, ImmutableDictionary<int, ImmutableArray<int>>.Empty)
    {
    }

    internal CsiParameters(ParserParameters parameters)
    {
        var values = ImmutableArray.CreateBuilder<int>(parameters.Length);
        var subParameters = ImmutableDictionary.CreateBuilder<int, ImmutableArray<int>>();
        for (int index = 0; index < parameters.Length; index++)
        {
            values.Add(parameters.GetValue(index));
            if (parameters.HasSubParameters(index))
            {
                subParameters.Add(index, ImmutableArray.CreateRange(parameters.GetSubParameters(index).ToArray()));
            }
        }
        Values = values.MoveToImmutable();
        _subParameters = subParameters.ToImmutable();
    }

    private CsiParameters(
        ImmutableArray<int> values,
        ImmutableDictionary<int, ImmutableArray<int>> subParameters)
    {
        Values = values;
        _subParameters = subParameters;
    }

    public ImmutableArray<int> Values { get; }

    /// <summary>Gets whether the parameter at <paramref name="index"/> has colon sub-parameters.</summary>
    public bool HasSubParameters(int index) => _subParameters.ContainsKey(index);

    /// <summary>
    /// Gets colon sub-parameters for the parameter at <paramref name="index"/>, or an empty array
    /// when no sub-parameters were present.
    /// </summary>
    public ImmutableArray<int> GetSubParameters(int index) =>
        _subParameters.TryGetValue(index, out ImmutableArray<int> values)
            ? values
            : ImmutableArray<int>.Empty;

    /// <summary>Gets a copy of all colon sub-parameters keyed by their parent parameter index.</summary>
    public IReadOnlyDictionary<int, ImmutableArray<int>> SubParameters => _subParameters;

    public int GetOrDefault(int index, int defaultValue = 1)
    {
        if ((uint)index >= (uint)Values.Length || Values[index] == 0)
        {
            return defaultValue;
        }
        return Values[index];
    }
}

using System.Collections.Immutable;

namespace XtermSharp.Internal;

/// <summary>
/// Mutable parameter storage used while parsing CSI and DCS sequences. It mirrors xterm.js'
/// parameter limits and, importantly, keeps colon-separated sub-parameters distinct from
/// semicolon-separated parameters.
/// </summary>
internal sealed class ParserParameters
{
    internal const int MaximumValue = int.MaxValue;
    internal const int MaximumSubParameterCount = 256;

    private readonly int[] _parameters;
    private readonly int[] _subParameters;
    private readonly ushort[] _subParameterRanges;
    private int _subParameterLength;
    private bool _rejectDigits;
    private bool _rejectSubParameterDigits;
    private bool _digitIsSubParameter;

    public ParserParameters(int maximumParameterCount = 32, int maximumSubParameterCount = 32)
    {
        if (maximumParameterCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumParameterCount));
        }
        if (maximumSubParameterCount is < 0 or > MaximumSubParameterCount)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumSubParameterCount));
        }

        _parameters = new int[maximumParameterCount];
        _subParameters = new int[maximumSubParameterCount];
        _subParameterRanges = new ushort[maximumParameterCount];
    }

    public int MaximumParameterCount => _parameters.Length;
    public int MaximumSubParameterCountValue => _subParameters.Length;
    public int Length { get; private set; }
    public int SubParameterLength => _subParameterLength;
    public ReadOnlySpan<int> Values => _parameters.AsSpan(0, Length);

    public static ParserParameters From(IEnumerable<ParserParameter> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var result = new ParserParameters();
        foreach (ParserParameter parameter in values)
        {
            result.AddParameter(parameter.Value);
            foreach (int subParameter in parameter.SubParameters)
            {
                result.AddSubParameter(subParameter);
            }
        }
        return result;
    }

    public ParserParameters Clone()
    {
        var clone = new ParserParameters(_parameters.Length, _subParameters.Length);
        Array.Copy(_parameters, clone._parameters, _parameters.Length);
        Array.Copy(_subParameters, clone._subParameters, _subParameters.Length);
        Array.Copy(_subParameterRanges, clone._subParameterRanges, _subParameterRanges.Length);
        clone.Length = Length;
        clone._subParameterLength = _subParameterLength;
        clone._rejectDigits = _rejectDigits;
        clone._rejectSubParameterDigits = _rejectSubParameterDigits;
        clone._digitIsSubParameter = _digitIsSubParameter;
        return clone;
    }

    public void Reset()
    {
        Length = 0;
        _subParameterLength = 0;
        _rejectDigits = false;
        _rejectSubParameterDigits = false;
        _digitIsSubParameter = false;
    }

    public void ResetZeroDefault()
    {
        Reset();
        Length = 1;
        _parameters[0] = 0;
        _subParameterRanges[0] = 0;
    }

    public void AddParameter(int value)
    {
        _digitIsSubParameter = false;
        if (Length >= _parameters.Length)
        {
            _rejectDigits = true;
            return;
        }
        if (value < -1)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Values less than -1 are not allowed.");
        }

        _subParameterRanges[Length] = (ushort)((_subParameterLength << 8) | _subParameterLength);
        _parameters[Length++] = Math.Min(value, MaximumValue);
    }

    public void AddSubParameter(int value)
    {
        _digitIsSubParameter = true;
        if (Length == 0)
        {
            return;
        }
        if (_rejectDigits || _subParameterLength >= _subParameters.Length)
        {
            _rejectSubParameterDigits = true;
            return;
        }
        if (value < -1)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Values less than -1 are not allowed.");
        }

        _subParameters[_subParameterLength++] = Math.Min(value, MaximumValue);
        _subParameterRanges[Length - 1]++;
    }

    public void AddDigit(int value)
    {
        if ((uint)value > 9)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        int length = _digitIsSubParameter ? _subParameterLength : Length;
        if (_rejectDigits || length == 0 || (_digitIsSubParameter && _rejectSubParameterDigits))
        {
            return;
        }

        int[] store = _digitIsSubParameter ? _subParameters : _parameters;
        int current = store[length - 1];
        store[length - 1] = current == -1
            ? value
            : current > (MaximumValue - value) / 10
                ? MaximumValue
                : current * 10 + value;
    }

    public int GetValue(int index) => _parameters[index];

    public bool HasSubParameters(int index)
    {
        (int start, int end) = GetRange(index);
        return end > start;
    }

    public ReadOnlySpan<int> GetSubParameters(int index)
    {
        (int start, int end) = GetRange(index);
        return _subParameters.AsSpan(start, end - start);
    }

    public ImmutableArray<ParserParameter> ToImmutableArray()
    {
        var result = ImmutableArray.CreateBuilder<ParserParameter>(Length);
        for (int index = 0; index < Length; index++)
        {
            result.Add(new ParserParameter(
                _parameters[index],
                ImmutableArray.CreateRange(GetSubParameters(index).ToArray())));
        }
        return result.MoveToImmutable();
    }

    private (int Start, int End) GetRange(int index)
    {
        if ((uint)index >= (uint)Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        ushort range = _subParameterRanges[index];
        return (range >> 8, range & 0xFF);
    }
}

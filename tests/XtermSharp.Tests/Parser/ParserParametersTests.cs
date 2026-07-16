using System.Collections.Immutable;
using XtermSharp.Internal;

namespace XtermSharp.Tests.Parser;

public sealed class ParserParametersTests
{
    [UpstreamFact("XTJS-1218", "Params should respect ctor args")]
    public void Constructor_respects_capacity_arguments()
    {
        var parameters = new ParserParameters(12, 23);
        Assert.Equal(12, parameters.MaximumParameterCount);
        Assert.Equal(23, parameters.MaximumSubParameterCountValue);
        Assert.Empty(parameters.ToImmutableArray());
    }

    [UpstreamFact("XTJS-1219", "Params addParam")]
    public void Add_parameter()
    {
        var parameters = new ParserParameters();
        parameters.AddParameter(1);
        AssertParameters(parameters, P(1));
        parameters.AddParameter(23);
        AssertParameters(parameters, P(1), P(23));
        Assert.Equal(0, parameters.SubParameterLength);
    }

    [UpstreamFact("XTJS-1220", "Params addSubParam")]
    public void Add_sub_parameter()
    {
        var parameters = new ParserParameters();
        parameters.AddParameter(1);
        parameters.AddSubParameter(2);
        parameters.AddSubParameter(3);
        AssertParameters(parameters, P(1, 2, 3));
        parameters.AddParameter(12345);
        parameters.AddSubParameter(-1);
        AssertParameters(parameters, P(1, 2, 3), P(12345, -1));
    }

    [UpstreamFact("XTJS-1221", "Params should not add sub params without previous param")]
    public void Sub_parameters_without_a_parent_are_ignored()
    {
        var parameters = new ParserParameters();
        parameters.AddSubParameter(2);
        parameters.AddSubParameter(3);
        Assert.Empty(parameters.ToImmutableArray());
        parameters.AddParameter(1);
        parameters.AddSubParameter(2);
        parameters.AddSubParameter(3);
        AssertParameters(parameters, P(1, 2, 3));
    }

    [UpstreamFact("XTJS-1222", "Params reset")]
    public void Reset_clears_all_state()
    {
        var parameters = ParserParameters.From([P(1, 2, 3), P(12345, -1)]);
        parameters.Reset();
        Assert.Empty(parameters.ToImmutableArray());
        parameters.AddParameter(1);
        parameters.AddSubParameter(2);
        parameters.AddSubParameter(3);
        parameters.AddParameter(12345);
        parameters.AddSubParameter(-1);
        AssertParameters(parameters, P(1, 2, 3), P(12345, -1));
    }

    [UpstreamFact("XTJS-1223", "Params Params.fromArray --> toArray")]
    public void From_and_to_array_round_trip()
    {
        Assert.Empty(ParserParameters.From([]).ToImmutableArray());
        ParserParameter[] values = [P(1, 2, 3), P(12345, -1)];
        Assert.Equal(values, ParserParameters.From(values).ToImmutableArray());
        values = [P(38), P(2), P(50), P(100), P(150)];
        Assert.Equal(values, ParserParameters.From(values).ToImmutableArray());
        values = [P(38), P(2), P(50), P(100, 150)];
        Assert.Equal(values, ParserParameters.From(values).ToImmutableArray());
        values = [P(38, 2, 50, 100, 150)];
        Assert.Equal(values, ParserParameters.From(values).ToImmutableArray());
    }

    [UpstreamFact("XTJS-1224", "Params clone")]
    public void Clone_copies_all_state()
    {
        ParserParameters parameters = ParserParameters.From(
            [P(38, 2, 50, 100, 150), P(5), P(6), P(1, 2, 3), P(12345, -1)]);
        Assert.Equal(parameters.ToImmutableArray(), parameters.Clone().ToImmutableArray());
    }

    [UpstreamFact("XTJS-1225", "Params hasSubParams / getSubParams")]
    public void Has_and_get_sub_parameters()
    {
        ParserParameters parameters = ParserParameters.From([P(38, 2, 50, 100, 150), P(5), P(6)]);
        Assert.True(parameters.HasSubParameters(0));
        Assert.Equal([2, 50, 100, 150], parameters.GetSubParameters(0).ToArray());
        Assert.False(parameters.HasSubParameters(1));
        Assert.Empty(parameters.GetSubParameters(1).ToArray());
        Assert.False(parameters.HasSubParameters(2));
    }

    [UpstreamFact("XTJS-1226", "Params getSubParamsAll")]
    public void All_sub_parameters_are_available_by_parent_index()
    {
        ParserParameters parameters = ParserParameters.From([P(1, 2, 3), P(7), P(12345, -1)]);
        Dictionary<int, int[]> actual = Enumerable.Range(0, parameters.Length)
            .Where(parameters.HasSubParameters)
            .ToDictionary(index => index, index => parameters.GetSubParameters(index).ToArray());
        Assert.Equal([2, 3], actual[0]);
        Assert.Equal([-1], actual[2]);
    }

    [UpstreamFact("XTJS-1227", "Params parse tests param defaults to 0 (ZDM - zero default mode)")]
    public void Empty_input_defaults_to_zero()
    {
        AssertParameters(Parse(""), P(0));
    }

    [UpstreamFact("XTJS-1228", "Params parse tests sub param defaults to -1")]
    public void Empty_sub_parameter_defaults_to_minus_one()
    {
        AssertParameters(Parse(":"), P(0, -1));
    }

    [UpstreamFact("XTJS-1229", "Params parse tests should correctly reset on new sequence")]
    public void Parsing_a_new_sequence_resets_previous_state()
    {
        var parameters = new ParserParameters();
        Parse(parameters, "1;2;3");
        AssertParameters(parameters, P(1), P(2), P(3));
        Parse(parameters, "4");
        AssertParameters(parameters, P(4));
        Parse(parameters, "4::123:5;6;7");
        AssertParameters(parameters, P(4, -1, 123, 5), P(6), P(7));
        Parse(parameters, "");
        AssertParameters(parameters, P(0));
    }

    [UpstreamFact("XTJS-1230", "Params parse tests should handle length restrictions correctly")]
    public void Length_restrictions_are_enforced()
    {
        var parameters = new ParserParameters(3, 3);
        Parse(parameters, "1;2;3;4;5;6;7");
        AssertParameters(parameters, P(1), P(2), P(3));
        Parse(parameters, "4;38:2::50:100:150;48:5:22");
        AssertParameters(parameters, P(4), P(38, 2, -1, 50), P(48));
    }

    [UpstreamFact("XTJS-1231", "Params parse tests typical sequences")]
    public void Typical_sgr_parameter_forms_are_distinct()
    {
        AssertParameters(Parse("0;4;38;2;50;100;150;48;5;22"),
            P(0), P(4), P(38), P(2), P(50), P(100), P(150), P(48), P(5), P(22));
        AssertParameters(Parse("0;4;38;2;50:100:150;48;5:22"),
            P(0), P(4), P(38), P(2), P(50, 100, 150), P(48), P(5, 22));
        AssertParameters(Parse("0;4;38:2::50:100:150;48:5:22"),
            P(0), P(4), P(38, 2, -1, 50, 100, 150), P(48, 5, 22));
    }

    [UpstreamFact("XTJS-1232", "Params should not overflow to negative reject params lesser -1")]
    public void Parameters_less_than_minus_one_are_rejected()
    {
        var parameters = new ParserParameters();
        parameters.AddParameter(-1);
        Assert.Throws<ArgumentOutOfRangeException>(() => parameters.AddParameter(-2));
    }

    [UpstreamFact("XTJS-1233", "Params should not overflow to negative reject subparams lesser -1")]
    public void Sub_parameters_less_than_minus_one_are_rejected()
    {
        var parameters = new ParserParameters();
        parameters.AddParameter(-1);
        parameters.AddSubParameter(-1);
        Assert.Throws<ArgumentOutOfRangeException>(() => parameters.AddSubParameter(-2));
        AssertParameters(parameters, P(-1, -1));
    }

    [UpstreamFact("XTJS-1234", "Params should not overflow to negative clamp parsed params")]
    public void Parsed_parameters_are_clamped_to_int32_max()
    {
        AssertParameters(Parse("2147483648"), P(int.MaxValue));
    }

    [UpstreamFact("XTJS-1235", "Params should not overflow to negative clamp parsed subparams")]
    public void Parsed_sub_parameters_are_clamped_to_int32_max()
    {
        AssertParameters(Parse(":2147483648"), P(0, int.MaxValue));
    }

    [UpstreamFact("XTJS-1236", "Params issue 2389 should cancel subdigits if beyond params limit")]
    public void Digits_and_subdigits_beyond_parameter_limit_are_ignored()
    {
        ParserParameters parameters = Parse(";;;;;;;;;10;;;;;;;;;;20;;;;;;;;;;30;31;32;33;34;35::::::::");
        Assert.Equal(32, parameters.Length);
        Assert.Equal(10, parameters.GetValue(9));
        Assert.Equal(20, parameters.GetValue(19));
        Assert.Equal(30, parameters.GetValue(29));
        Assert.Equal(31, parameters.GetValue(30));
        Assert.Equal(32, parameters.GetValue(31));
        Assert.False(parameters.HasSubParameters(31));
    }

    [UpstreamFact("XTJS-1237", "Params issue 2389 should carry forward isSub state")]
    public void Sub_parameter_digit_state_carries_across_chunks()
    {
        var parameters = new ParserParameters();
        Parse(parameters, "1:22:33", "44");
        AssertParameters(parameters, P(1, 22, 3344));
    }

    private static ParserParameters Parse(params string[] chunks)
    {
        var parameters = new ParserParameters();
        Parse(parameters, chunks);
        return parameters;
    }

    private static void Parse(ParserParameters parameters, params string[] chunks)
    {
        parameters.ResetZeroDefault();
        foreach (string chunk in chunks)
        {
            foreach (char value in chunk)
            {
                switch (value)
                {
                    case ';':
                        parameters.AddParameter(0);
                        break;
                    case ':':
                        parameters.AddSubParameter(-1);
                        break;
                    default:
                        parameters.AddDigit(value - '0');
                        break;
                }
            }
        }
    }

    private static ParserParameter P(int value, params int[] subParameters) =>
        new(value, ImmutableArray.Create(subParameters));

    private static void AssertParameters(ParserParameters actual, params ParserParameter[] expected) =>
        Assert.Equal(expected, actual.ToImmutableArray());
}

using System.Collections.Immutable;
using XtermSharp.Internal;
using XtermSharp.Internal.Parser;
using XtermSharp.TestSupport;

namespace XtermSharp.Tests.Parser.StringSequences;

public sealed class DcsParserTests
{
    public static TheoryData<string, string, int> Cases { get; } = ParserCaseData.For("src/common/parser/DcsParser.test.ts");

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Matches_upstream_dcs_scenarios(string upstreamId, string title, int scenario)
    {
        Assert.StartsWith("XTJS-", upstreamId, StringComparison.Ordinal);
        Assert.NotEmpty(title);
        if (scenario <= 4) await RegistrationAsync(scenario);
        else if (scenario <= 8) await FactoryAsync(scenario);
        else if (scenario <= 10) await PayloadLimitAsync(scenario == 10);
        else if (scenario <= 14) await MixedAsync(firstHandled: scenario is 11 or 13);
        else if (scenario <= 18) await FactoryAsync(scenario - 10, asynchronous: true);
        else Reset_aborts_active_handler();
    }

    private static CsiParameters Parameters() => new(ParserParameters.From([new ParserParameter(1), new ParserParameter(2)]));

    private static async Task RegistrationAsync(int scenario)
    {
        var reports = new List<string>();
        using var parser = new DcsParser();
        parser.SetHandlerFallback((id, action, data) => reports.Add($"fb:{id}:{action}:{Format(data)}"));
        parser.RegisterHandler(1234, new RecordingDcsHandler("one", reports, false));
        IDisposable? second = scenario >= 2 ? parser.RegisterHandler(1234, new RecordingDcsHandler("two", reports, scenario == 3)) : null;
        if (scenario == 1) parser.ClearHandler(1234);
        if (scenario == 4) second!.Dispose();
        parser.Hook(1234, Parameters());
        parser.Put(ApcParserTests.ToUtf32("payload"));
        await parser.UnhookAsync(true);
        if (scenario == 1) Assert.Equal(3, reports.Count);
        else if (scenario is 0 or 4) Assert.Equal(["one:hook:1,2", "one:put:payload", "one:unhook:True"], reports);
        else Assert.Equal(scenario == 3 ? 2 : 1, reports.Count(value => value.EndsWith("True", StringComparison.Ordinal)));
    }

    private static async Task FactoryAsync(int scenario, bool asynchronous = false)
    {
        bool success = scenario != 6;
        bool disposable = scenario == 7;
        bool fallsThrough = scenario == 8;
        var reports = new List<string>();
        using var parser = new DcsParser();
        parser.RegisterHandler(1234, new DcsStringHandler((value, parameters) => Callback("one", value, true)));
        IDisposable? second = null;
        if (disposable || fallsThrough)
            second = parser.RegisterHandler(1234, new DcsStringHandler((value, parameters) => Callback("two", value, !fallsThrough)));

        Run("first");
        await parser.UnhookAsync(success);
        if (disposable)
        {
            second!.Dispose();
            Run("second");
            await parser.UnhookAsync(true);
            Assert.Equal(["two:first", "one:second"], reports);
        }
        else if (!success) Assert.Empty(reports);
        else if (fallsThrough) Assert.Equal(["two:first", "one:first"], reports);
        else Assert.Equal(["one:first"], reports);

        ValueTask<bool> Callback(string name, string value, bool result)
        {
            reports.Add($"{name}:{value}");
            return asynchronous ? Yield(result) : ValueTask.FromResult(result);
        }
        void Run(string value)
        {
            parser.Hook(1234, Parameters());
            parser.Put(ApcParserTests.ToUtf32(value));
        }
    }

    private static async Task PayloadLimitAsync(bool exceed)
    {
        var reports = new List<string>();
        using var parser = new DcsParser();
        parser.RegisterHandler(1234, new DcsStringHandler((value, _) =>
        {
            reports.Add(value);
            return ValueTask.FromResult(true);
        }, 100));
        parser.Hook(1234, Parameters());
        for (int index = 0; index < 10; index++) parser.Put(ApcParserTests.ToUtf32("0123456789"));
        if (exceed) parser.Put(ApcParserTests.ToUtf32("X"));
        await parser.UnhookAsync(true);
        if (exceed) Assert.Empty(reports); else Assert.Single(reports);
    }

    private static async Task MixedAsync(bool firstHandled)
    {
        var reports = new List<string>();
        using var parser = new DcsParser();
        parser.RegisterHandler(1234, new RecordingDcsHandler("low", reports, !firstHandled));
        parser.RegisterHandler(1234, new AsyncDcsHandler("async", reports, !firstHandled));
        parser.RegisterHandler(1234, new RecordingDcsHandler("high", reports, !firstHandled));
        parser.Hook(1234, Parameters());
        parser.Put(ApcParserTests.ToUtf32("x"));
        await parser.UnhookAsync(true);
        Assert.Equal(firstHandled ? 1 : 3, reports.Count(value => value.EndsWith("True", StringComparison.Ordinal)));
    }

    private static void Reset_aborts_active_handler()
    {
        var reports = new List<string>();
        using var parser = new DcsParser();
        parser.RegisterHandler(1234, new RecordingDcsHandler("one", reports, false));
        parser.Hook(1234, Parameters());
        parser.Put(ApcParserTests.ToUtf32("x"));
        parser.Reset();
        Assert.EndsWith("one:unhook:False", reports[^1], StringComparison.Ordinal);
    }

    private static string Format(object? value) => value switch
    {
        CsiParameters parameters => string.Join(',', parameters.Values),
        null => string.Empty,
        _ => value.ToString() ?? string.Empty
    };

    private static async ValueTask<bool> Yield(bool value) { await Task.Yield(); return value; }
}

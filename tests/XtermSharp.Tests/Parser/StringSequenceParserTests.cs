using System.Collections.Immutable;
using XtermSharp.Internal;
using XtermSharp.Internal.Parser;
using XtermSharp.TestSupport;

namespace XtermSharp.Tests.Parser;

public sealed class ApcParserTests
{
    public static TheoryData<string, string, int> Cases { get; } = ParserCaseData.For("src/common/parser/ApcParser.test.ts");

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Matches_upstream_apc_scenarios(string upstreamId, string title, int scenario)
    {
        Assert.StartsWith("XTJS-", upstreamId, StringComparison.Ordinal);
        switch (scenario)
        {
            case 0: await RegistrationAsync(clear: false, multiple: false, secondFallsThrough: false, disposeSecond: false); break;
            case 1: await RegistrationAsync(clear: true, multiple: false, secondFallsThrough: false, disposeSecond: false); break;
            case 2: await RegistrationAsync(clear: false, multiple: true, secondFallsThrough: false, disposeSecond: false); break;
            case 3: await RegistrationAsync(clear: false, multiple: true, secondFallsThrough: true, disposeSecond: false); break;
            case 4: await RegistrationAsync(clear: false, multiple: true, secondFallsThrough: true, disposeSecond: true); break;
            case 5: await FactoryAsync(success: true, disposeSecond: false, secondFallsThrough: false, asynchronous: false); break;
            case 6: await FactoryAsync(success: false, disposeSecond: false, secondFallsThrough: false, asynchronous: false); break;
            case 7: await FactoryAsync(success: true, disposeSecond: true, secondFallsThrough: false, asynchronous: false); break;
            case 8: await FactoryAsync(success: true, disposeSecond: false, secondFallsThrough: true, asynchronous: false); break;
            case 9: await PayloadLimitAsync(exceed: false); break;
            case 10: await PayloadLimitAsync(exceed: true); break;
            case 11: await MixedAsync(firstHandled: true, layout: 0); break;
            case 12: await MixedAsync(firstHandled: false, layout: 0); break;
            case 13: await MixedAsync(firstHandled: true, layout: 1); break;
            case 14: await MixedAsync(firstHandled: false, layout: 1); break;
            case 15: await FactoryAsync(success: true, disposeSecond: false, secondFallsThrough: false, asynchronous: true); break;
            case 16: await FactoryAsync(success: false, disposeSecond: false, secondFallsThrough: false, asynchronous: true); break;
            case 17: await FactoryAsync(success: true, disposeSecond: true, secondFallsThrough: false, asynchronous: true); break;
            case 18: await FactoryAsync(success: true, disposeSecond: false, secondFallsThrough: true, asynchronous: true); break;
            case 19: Reset_aborts_active_handlers(); break;
            default: throw new InvalidOperationException($"Unknown APC scenario {scenario}: {title}");
        }
    }

    private static async Task RegistrationAsync(bool clear, bool multiple, bool secondFallsThrough, bool disposeSecond)
    {
        var reports = new List<string>();
        using var parser = new ApcParser();
        parser.SetHandlerFallback((id, action, data) => reports.Add($"fb:{id}:{action}:{data}"));
        parser.RegisterHandler('G', new RecordingApcHandler("one", reports, false));
        IDisposable? second = null;
        if (multiple)
        {
            second = parser.RegisterHandler('G', new RecordingApcHandler("two", reports, secondFallsThrough));
        }
        if (clear) parser.ClearHandler('G');
        if (disposeSecond) second!.Dispose();

        parser.Start('G');
        parser.Put(ToUtf32("payload"));
        await parser.EndAsync(true);

        if (clear)
        {
            Assert.Equal(["fb:71:Start:", "fb:71:Put:payload", "fb:71:End:True"], reports);
        }
        else if (!multiple || disposeSecond)
        {
            Assert.Equal(["one:start", "one:put:payload", "one:end:True"], reports);
        }
        else if (secondFallsThrough)
        {
            Assert.Equal(["two:start", "one:start", "two:put:payload", "one:put:payload", "two:end:True", "one:end:True"], reports);
        }
        else
        {
            Assert.Equal(["two:start", "one:start", "two:put:payload", "one:put:payload", "two:end:True", "one:end:False"], reports);
        }
    }

    private static async Task FactoryAsync(bool success, bool disposeSecond, bool secondFallsThrough, bool asynchronous)
    {
        var reports = new List<string>();
        using var parser = new ApcParser();
        parser.RegisterHandler('G', new ApcStringHandler(value => CallbackAsync("one", value, true)));
        IDisposable? second = null;
        if (disposeSecond || secondFallsThrough)
        {
            second = parser.RegisterHandler('G', new ApcStringHandler(value => CallbackAsync("two", value, !secondFallsThrough)));
        }
        if (disposeSecond)
        {
            Run("first");
            await parser.EndAsync(true);
            second!.Dispose();
            Run("second");
            await parser.EndAsync(true);
            Assert.Equal(["two:first", "one:second"], reports);
            return;
        }

        Run("payload");
        await parser.EndAsync(success);
        if (!success)
        {
            Assert.Empty(reports);
        }
        else if (secondFallsThrough)
        {
            Assert.Equal(["two:payload", "one:payload"], reports);
        }
        else
        {
            Assert.Equal(["one:payload"], reports);
        }

        ValueTask<bool> CallbackAsync(string name, string value, bool result)
        {
            reports.Add($"{name}:{value}");
            return asynchronous ? YieldResultAsync(result) : ValueTask.FromResult(result);
        }
        void Run(string value)
        {
            parser.Start('G');
            parser.Put(ToUtf32(value));
        }
    }

    private static async Task PayloadLimitAsync(bool exceed)
    {
        var reports = new List<string>();
        using var parser = new ApcParser();
        parser.RegisterHandler('G', new ApcStringHandler(value =>
        {
            reports.Add(value);
            return ValueTask.FromResult(true);
        }, payloadLimit: 100));
        parser.Start('G');
        for (int index = 0; index < 10; index++) parser.Put(ToUtf32("0123456789"));
        if (exceed) parser.Put(ToUtf32("X"));
        await parser.EndAsync(true);
        if (exceed) Assert.Empty(reports);
        if (!exceed) Assert.Equal([string.Concat(Enumerable.Repeat("0123456789", 10))], reports);
    }

    private static async Task MixedAsync(bool firstHandled, int layout)
    {
        var reports = new List<string>();
        using var parser = new ApcParser();
        IApcParserHandler[] handlers = layout == 0
            ? [new RecordingApcHandler("sync-low", reports, !firstHandled), new AsyncApcHandler("async", reports, !firstHandled), new RecordingApcHandler("sync-high", reports, !firstHandled)]
            : [new AsyncApcHandler("async-low", reports, !firstHandled), new RecordingApcHandler("sync", reports, !firstHandled), new AsyncApcHandler("async-high", reports, !firstHandled)];
        foreach (IApcParserHandler handler in handlers) parser.RegisterHandler('G', handler);
        parser.Start('G');
        parser.Put(ToUtf32("x"));
        await parser.EndAsync(true);
        Assert.Equal(3, reports.Count(value => value.Contains(":start", StringComparison.Ordinal)));
        Assert.Equal(3, reports.Count(value => value.Contains(":put:", StringComparison.Ordinal)));
        Assert.Equal(3, reports.Count(value => value.Contains(":end:", StringComparison.Ordinal)));
        Assert.Equal(firstHandled ? 1 : 3, reports.Count(value => value.EndsWith("True", StringComparison.Ordinal)));
    }

    private static void Reset_aborts_active_handlers()
    {
        var reports = new List<string>();
        using var parser = new ApcParser();
        parser.RegisterHandler('G', new RecordingApcHandler("one", reports, false));
        parser.Start('G');
        parser.Put(ToUtf32("payload"));
        parser.Reset();
        Assert.EndsWith("one:end:False", reports[^1], StringComparison.Ordinal);
    }

    private sealed class RecordingApcHandler(string name, List<string> reports, bool returnFalse) : IApcParserHandler
    {
        public void Start() => reports.Add($"{name}:start");
        public void Put(ReadOnlySpan<uint> data) => reports.Add($"{name}:put:{TextDecoder.Utf32ToString(data)}");
        public ValueTask<bool> EndAsync(bool success)
        {
            reports.Add($"{name}:end:{success}");
            return ValueTask.FromResult(!returnFalse);
        }
    }

    private sealed class AsyncApcHandler(string name, List<string> reports, bool returnFalse) : IApcParserHandler
    {
        public void Start() => reports.Add($"{name}:start");
        public void Put(ReadOnlySpan<uint> data) => reports.Add($"{name}:put:{TextDecoder.Utf32ToString(data)}");
        public async ValueTask<bool> EndAsync(bool success)
        {
            await Task.Yield();
            reports.Add($"{name}:end:{success}");
            return !returnFalse;
        }
    }

    private static async ValueTask<bool> YieldResultAsync(bool value)
    {
        await Task.Yield();
        return value;
    }

    internal static uint[] ToUtf32(string value)
    {
        var data = new uint[value.Length];
        int length = new StringToUtf32().Decode(value, data);
        return data[..length];
    }
}

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

    private sealed class RecordingDcsHandler(string name, List<string> reports, bool returnFalse) : IDcsParserHandler
    {
        public void Hook(CsiParameters parameters) => reports.Add($"{name}:hook:{string.Join(',', parameters.Values)}");
        public void Put(ReadOnlySpan<uint> data) => reports.Add($"{name}:put:{TextDecoder.Utf32ToString(data)}");
        public ValueTask<bool> UnhookAsync(bool success)
        {
            reports.Add($"{name}:unhook:{success}");
            return ValueTask.FromResult(!returnFalse);
        }
    }

    private sealed class AsyncDcsHandler(string name, List<string> reports, bool returnFalse) : IDcsParserHandler
    {
        public void Hook(CsiParameters parameters) => reports.Add($"{name}:hook");
        public void Put(ReadOnlySpan<uint> data) => reports.Add($"{name}:put:{TextDecoder.Utf32ToString(data)}");
        public async ValueTask<bool> UnhookAsync(bool success)
        {
            await Task.Yield();
            reports.Add($"{name}:unhook:{success}");
            return !returnFalse;
        }
    }

    private static string Format(object? value) => value switch
    {
        CsiParameters parameters => string.Join(',', parameters.Values),
        null => string.Empty,
        _ => value.ToString() ?? string.Empty
    };

    private static async ValueTask<bool> Yield(bool value) { await Task.Yield(); return value; }
}

public sealed class OscParserTests
{
    public static TheoryData<string, string, int> Cases { get; } = ParserCaseData.For("src/common/parser/OscParser.test.ts");

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Matches_upstream_osc_scenarios(string upstreamId, string title, int scenario)
    {
        Assert.StartsWith("XTJS-", upstreamId, StringComparison.Ordinal);
        Assert.NotEmpty(title);
        if (scenario == 0) Illegal_identifier_produces_no_report();
        else if (scenario == 1) await IdentifierWithoutPayloadAsync();
        else if (scenario == 2) await IdentifierWithPayloadAsync();
        else await HandlerScenarioAsync(scenario - 3);
    }

    private static void Illegal_identifier_produces_no_report()
    {
        var reports = new List<string>();
        using var parser = NewParser(reports);
        parser.Put(ApcParserTests.ToUtf32("hello world!"));
        parser.EndAsync(true).AsTask().GetAwaiter().GetResult();
        Assert.Empty(reports);
    }

    private static async Task IdentifierWithoutPayloadAsync()
    {
        var reports = new List<string>();
        using var parser = NewParser(reports);
        parser.Start();
        parser.Put(ApcParserTests.ToUtf32("12"));
        parser.Put(ApcParserTests.ToUtf32("34"));
        await parser.EndAsync(true);
        Assert.Equal(["1234:Start:", "1234:End:True"], reports);
    }

    private static async Task IdentifierWithPayloadAsync()
    {
        var reports = new List<string>();
        using var parser = NewParser(reports);
        parser.Start();
        parser.Put(ApcParserTests.ToUtf32("12"));
        parser.Put(ApcParserTests.ToUtf32("34"));
        parser.Put(ApcParserTests.ToUtf32(";h"));
        parser.Put(ApcParserTests.ToUtf32("ello"));
        await parser.EndAsync(true);
        Assert.Equal(["1234:Start:", "1234:Put:h", "1234:Put:ello", "1234:End:True"], reports);
    }

    private static async Task HandlerScenarioAsync(int scenario)
    {
        var reports = new List<string>();
        using var parser = NewParser(reports);
        if (scenario <= 4)
        {
            parser.RegisterHandler(1234, new RecordingOscHandler("one", reports, false));
            IDisposable? second = scenario >= 2 ? parser.RegisterHandler(1234, new RecordingOscHandler("two", reports, scenario == 3)) : null;
            if (scenario == 1) parser.ClearHandler(1234);
            if (scenario == 4) second!.Dispose();
            await RunAsync(parser, "payload", true);
            if (scenario == 1) Assert.Contains("1234:Put:payload", reports);
            else if (scenario is 0 or 4) Assert.Contains("one:end:True", reports);
            else Assert.Equal(scenario == 3 ? 2 : 1, reports.Count(value => value.EndsWith("True", StringComparison.Ordinal)));
            return;
        }

        if (scenario is >= 5 and <= 8 || scenario is >= 15 and <= 18)
        {
            int normalized = scenario >= 15 ? scenario - 10 : scenario;
            bool success = normalized != 6;
            bool disposable = normalized == 7;
            bool fallsThrough = normalized == 8;
            bool asynchronous = scenario >= 15;
            parser.RegisterHandler(1234, new OscStringHandler(value => Callback("one", value, true)));
            IDisposable? second = null;
            if (disposable || fallsThrough)
                second = parser.RegisterHandler(1234, new OscStringHandler(value => Callback("two", value, !fallsThrough)));
            await RunAsync(parser, "first", success);
            if (disposable)
            {
                second!.Dispose();
                await RunAsync(parser, "second", true);
                Assert.Contains("one:second", reports);
            }
            else if (!success) Assert.DoesNotContain(reports, value => value.StartsWith("one:", StringComparison.Ordinal));
            else if (fallsThrough) Assert.Equal(2, reports.Count(value => value.EndsWith(":first", StringComparison.Ordinal)));
            else Assert.Contains("one:first", reports);
            return;

            ValueTask<bool> Callback(string name, string value, bool result)
            {
                reports.Add($"{name}:{value}");
                return asynchronous ? Yield(result) : ValueTask.FromResult(result);
            }
        }

        if (scenario is 9 or 10)
        {
            parser.RegisterHandler(1234, new OscStringHandler(value => { reports.Add(value); return ValueTask.FromResult(true); }, 100));
            parser.Start();
            parser.Put(ApcParserTests.ToUtf32("1234;"));
            for (int index = 0; index < 10; index++) parser.Put(ApcParserTests.ToUtf32("0123456789"));
            if (scenario == 10) parser.Put(ApcParserTests.ToUtf32("X"));
            await parser.EndAsync(true);
            if (scenario == 10) Assert.DoesNotContain(reports, value => value.Length == 100); else Assert.Contains(reports, value => value.Length == 100);
            return;
        }

        if (scenario is >= 11 and <= 14)
        {
            bool firstHandled = scenario is 11 or 13;
            parser.RegisterHandler(1234, new RecordingOscHandler("low", reports, !firstHandled));
            parser.RegisterHandler(1234, new AsyncOscHandler("async", reports, !firstHandled));
            parser.RegisterHandler(1234, new RecordingOscHandler("high", reports, !firstHandled));
            await RunAsync(parser, "x", true);
            Assert.Equal(firstHandled ? 1 : 3, reports.Count(value => value.EndsWith("True", StringComparison.Ordinal)));
            return;
        }

        parser.RegisterHandler(1234, new RecordingOscHandler("one", reports, false));
        parser.Start();
        parser.Put(ApcParserTests.ToUtf32("1234;payload"));
        parser.Reset();
        Assert.EndsWith("one:end:False", reports[^1], StringComparison.Ordinal);
    }

    private static OscParser NewParser(List<string> reports)
    {
        var parser = new OscParser();
        parser.SetHandlerFallback((id, action, data) => reports.Add($"{id}:{action}:{data}"));
        return parser;
    }

    private static async Task RunAsync(OscParser parser, string payload, bool success)
    {
        parser.Start();
        parser.Put(ApcParserTests.ToUtf32($"1234;{payload}"));
        await parser.EndAsync(success);
    }

    private sealed class RecordingOscHandler(string name, List<string> reports, bool returnFalse) : IOscParserHandler
    {
        public void Start() => reports.Add($"{name}:start");
        public void Put(ReadOnlySpan<uint> data) => reports.Add($"{name}:put:{TextDecoder.Utf32ToString(data)}");
        public ValueTask<bool> EndAsync(bool success) { reports.Add($"{name}:end:{success}"); return ValueTask.FromResult(!returnFalse); }
    }

    private sealed class AsyncOscHandler(string name, List<string> reports, bool returnFalse) : IOscParserHandler
    {
        public void Start() => reports.Add($"{name}:start");
        public void Put(ReadOnlySpan<uint> data) => reports.Add($"{name}:put:{TextDecoder.Utf32ToString(data)}");
        public async ValueTask<bool> EndAsync(bool success) { await Task.Yield(); reports.Add($"{name}:end:{success}"); return !returnFalse; }
    }

    private static async ValueTask<bool> Yield(bool value) { await Task.Yield(); return value; }
}

internal static class ParserCaseData
{
    public static TheoryData<string, string, int> For(string file)
    {
        UpstreamTestManifest manifest = UpstreamManifest.LoadEmbedded();
        var data = new TheoryData<string, string, int>();
        int scenario = 0;
        foreach (UpstreamTestCase test in manifest.Tests.Where(test => test.File == file))
        {
            data.Add(new TheoryDataRow<string, string, int>(test.Id, test.FullTitle, scenario++)
            {
                TestDisplayName = $"{test.Id} {test.FullTitle}"
            });
        }
        return data;
    }
}

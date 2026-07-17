using System.Collections.Immutable;
using XtermSharp.Internal;
using XtermSharp.Internal.Parser;
using XtermSharp.TestSupport;

namespace XtermSharp.Tests.Parser.StringSequences;

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

    private static async ValueTask<bool> Yield(bool value) { await Task.Yield(); return value; }
}

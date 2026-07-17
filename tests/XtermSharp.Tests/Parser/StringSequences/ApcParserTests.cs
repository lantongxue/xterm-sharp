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

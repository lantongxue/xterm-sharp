using System.Text;
using XtermSharp.Internal;

namespace XtermSharp.Tests.Input;

public sealed class WriteBufferTests
{
    [UpstreamFact("XTJS-0772", "WriteBuffer write input string")]
    public Task Writes_strings_in_order() => OrderedWritesAsync(bytes: false, mixed: false);

    [UpstreamFact("XTJS-0773", "WriteBuffer write input bytes")]
    public Task Writes_bytes_in_order() => OrderedWritesAsync(bytes: true, mixed: false);

    [UpstreamFact("XTJS-0774", "WriteBuffer write input string/bytes mixed")]
    public Task Writes_mixed_chunks_in_order() => OrderedWritesAsync(bytes: false, mixed: true);

    [UpstreamFact("XTJS-0775", "WriteBuffer write input write callback works for empty chunks")]
    public async Task Empty_chunks_are_processed_and_callbacks_run()
    {
        var chunks = new List<string>();
        var callbacks = new List<string>();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var buffer = NewBuffer(chunks);
        buffer.Write("a", () => callbacks.Add("a"));
        buffer.Write("", () => callbacks.Add("b"));
        buffer.Write(Encoding.UTF8.GetBytes("c"), () => callbacks.Add("c"));
        buffer.Write(ReadOnlyMemory<byte>.Empty, () => callbacks.Add("d"));
        buffer.Write("e", () => done.SetResult());
        await done.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(["a", "", "c", "", "e"], chunks);
        Assert.Equal(["a", "b", "c", "d"], callbacks);
    }

    [UpstreamFact("XTJS-0776", "WriteBuffer write input writeSync")]
    public async Task WriteSync_flushes_queued_data_before_its_own_chunk()
    {
        var chunks = new List<string>();
        var callbacks = new List<string>();
        using var buffer = NewBuffer(chunks);
        buffer.Write("a", () => callbacks.Add("a"));
        buffer.Write("b", () => callbacks.Add("b"));
        buffer.Write("c", () => callbacks.Add("c"));
        buffer.WriteSync("d");
        Assert.Equal(["a", "b", "c", "d"], chunks);
        Assert.Equal(["a", "b", "c"], callbacks);
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        buffer.Write("x", () => callbacks.Add("x"));
        buffer.Write("", () => done.SetResult());
        await done.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(["a", "b", "c", "d", "x", ""], chunks);
    }

    [UpstreamFact("XTJS-0777", "WriteBuffer write input writeSync called from action does not overflow callstack - issue #3265")]
    public void Recursive_WriteSync_uses_a_loop()
    {
        WriteBuffer? buffer = null;
        buffer = new WriteBuffer((chunk, _) =>
        {
            int value = int.Parse(chunk.Text!, System.Globalization.CultureInfo.InvariantCulture);
            if (value < 10_000) buffer!.WriteSync((value + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
            return ValueTask.FromResult(true);
        });
        using (buffer) buffer.WriteSync("1");
    }

    [UpstreamFact("XTJS-0778", "WriteBuffer write input writeSync maxSubsequentCalls argument")]
    public void Recursive_WriteSync_honors_the_limit()
    {
        string last = string.Empty;
        WriteBuffer? buffer = null;
        buffer = new WriteBuffer((chunk, _) =>
        {
            last = chunk.Text!;
            int value = int.Parse(last, System.Globalization.CultureInfo.InvariantCulture);
            if (value < 1_000_000) buffer!.WriteSync((value + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), 10);
            return ValueTask.FromResult(true);
        });
        using (buffer) buffer.WriteSync("1", 10);
        Assert.Equal("11", last);
    }

    [UpstreamFact("XTJS-0779", "WriteBuffer write input flushSync processes all pending writes")]
    public void FlushSync_processes_all_pending_writes()
    {
        var chunks = new List<string>();
        var callbacks = new List<string>();
        using var buffer = NewBuffer(chunks);
        buffer.Write("a", () => callbacks.Add("a"));
        buffer.Write("b", () => callbacks.Add("b"));
        buffer.Write("c", () => callbacks.Add("c"));
        buffer.FlushSync();
        Assert.Equal(["a", "b", "c"], chunks);
        Assert.Equal(["a", "b", "c"], callbacks);
    }

    [UpstreamFact("XTJS-0780", "WriteBuffer write input flushSync with no pending writes is a no-op")]
    public void Empty_FlushSync_is_a_no_op()
    {
        var chunks = new List<string>();
        using var buffer = NewBuffer(chunks);
        buffer.FlushSync();
        Assert.Empty(chunks);
    }

    [UpstreamFact("XTJS-0781", "WriteBuffer write input flushSync fires onWriteParsed")]
    public void FlushSync_fires_WriteParsed_once()
    {
        using var buffer = NewBuffer([]);
        int parsed = 0;
        buffer.WriteParsed += () => parsed++;
        buffer.Write("a");
        buffer.Write("b");
        Assert.Equal(0, parsed);
        buffer.FlushSync();
        Assert.Equal(1, parsed);
    }

    [UpstreamFact("XTJS-0782", "WriteBuffer write input flushSync with no pending writes does not fire onWriteParsed")]
    public void Empty_FlushSync_does_not_fire_WriteParsed()
    {
        using var buffer = NewBuffer([]);
        int parsed = 0;
        buffer.WriteParsed += () => parsed++;
        buffer.FlushSync();
        Assert.Equal(0, parsed);
    }

    [UpstreamFact("XTJS-0783", "WriteBuffer write input dispose cancels scheduled innerWrite")]
    public async Task Dispose_cancels_scheduled_processing()
    {
        var chunks = new List<string>();
        var buffer = NewBuffer(chunks);
        buffer.Write("a");
        buffer.Dispose();
        await Task.Delay(20);
        Assert.Empty(chunks);
    }

    [UpstreamFact("XTJS-0784", "WriteBuffer write input dispose does not fire onWriteParsed for pending writes")]
    public async Task Dispose_does_not_fire_WriteParsed()
    {
        var buffer = NewBuffer([]);
        int parsed = 0;
        buffer.WriteParsed += () => parsed++;
        buffer.Write("a");
        buffer.Dispose();
        await Task.Delay(20);
        Assert.Equal(0, parsed);
    }

    [UpstreamFact("XTJS-0785", "WriteBuffer write input write after dispose is a no-op")]
    public async Task Write_after_dispose_is_a_no_op()
    {
        var chunks = new List<string>();
        var buffer = NewBuffer(chunks);
        buffer.Dispose();
        buffer.Write("a");
        await Task.Delay(20);
        Assert.Empty(chunks);
    }

    [UpstreamFact("XTJS-0786", "WriteBuffer write input dispose is idempotent")]
    public async Task Dispose_is_idempotent()
    {
        var chunks = new List<string>();
        var buffer = NewBuffer(chunks);
        buffer.Write("a");
        buffer.Dispose();
        buffer.Dispose();
        await Task.Delay(20);
        Assert.Empty(chunks);
    }

    [UpstreamFact("XTJS-0787", "WriteBuffer write input async handler continuation is skipped after dispose")]
    public async Task Async_continuation_is_skipped_after_dispose()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var chunks = new List<string>();
        var buffer = new WriteBuffer(async (chunk, _) =>
        {
            entered.SetResult();
            bool result = await release.Task;
            chunks.Add(chunk.Text!);
            return result;
        });
        buffer.Write("a");
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        buffer.Dispose();
        release.SetResult(true);
        await Task.Delay(20);
        Assert.Single(chunks);
    }

    [UpstreamFact("XTJS-0788", "WriteBuffer write input handleUserInput still processes first chunk synchronously")]
    public void User_input_causes_the_next_chunk_to_process_synchronously()
    {
        var chunks = new List<string>();
        using var buffer = NewBuffer(chunks);
        buffer.HandleUserInput();
        buffer.Write("a");
        Assert.Equal(["a"], chunks);
    }

    [UpstreamFact("XTJS-0789", "WriteBuffer write input flushSync after dispose is a no-op")]
    public void FlushSync_after_dispose_is_a_no_op()
    {
        var chunks = new List<string>();
        var buffer = NewBuffer(chunks);
        buffer.Write("a");
        buffer.Dispose();
        buffer.FlushSync();
        Assert.Empty(chunks);
    }

    [UpstreamFact("XTJS-0790", "WriteBuffer write input writeSync after dispose is a no-op")]
    public void WriteSync_after_dispose_is_a_no_op()
    {
        var chunks = new List<string>();
        var buffer = NewBuffer(chunks);
        buffer.Dispose();
        buffer.WriteSync("a");
        Assert.Empty(chunks);
    }

    private static async Task OrderedWritesAsync(bool bytes, bool mixed)
    {
        var chunks = new List<string>();
        var callbacks = new List<string>();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var buffer = NewBuffer(chunks);
        Write("a._");
        Write("b.x", () => callbacks.Add("b"));
        if (mixed) buffer.Write(Encoding.UTF8.GetBytes("c._")); else Write("c._");
        if (mixed) buffer.Write(Encoding.UTF8.GetBytes("d.x"), () => callbacks.Add("d")); else Write("d.x", () => callbacks.Add("d"));
        if (mixed || bytes) buffer.Write(Encoding.UTF8.GetBytes("e"), () => done.SetResult()); else buffer.Write("e", () => done.SetResult());
        await done.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(["a._", "b.x", "c._", "d.x", "e"], chunks);
        Assert.Equal(["b", "d"], callbacks);

        void Write(string value, Action? callback = null)
        {
            if (bytes) buffer.Write(Encoding.UTF8.GetBytes(value), callback); else buffer.Write(value, callback);
        }
    }

    private static WriteBuffer NewBuffer(List<string> chunks) => new((chunk, _) =>
    {
        chunks.Add(chunk.IsBytes ? Encoding.UTF8.GetString(chunk.Bytes.Span) : chunk.Text!);
        return ValueTask.FromResult(true);
    });
}

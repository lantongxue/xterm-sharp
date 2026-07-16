using System.Diagnostics;
using System.Text;
using XtermSharp;

const int iterations = 20;
string ascii = string.Concat(Enumerable.Repeat("0123456789abcdef ", 4096));
string colored = string.Concat(Enumerable.Repeat("\x1b[38;2;10;20;30mcolor\x1b[0m ", 2048));
string cjk = string.Concat(Enumerable.Repeat("终端仿真性能测试 ", 4096));

await RunAsync("ASCII", ascii);
await RunAsync("SGR", colored);
await RunAsync("CJK", cjk);

static async Task RunAsync(string name, string payload)
{
    await using var terminal = new Terminal(new TerminalOptions { Columns = 120, Rows = 40, Scrollback = 5000 });
    await terminal.WriteAsync(payload);
    await terminal.ResetAsync();

    long allocatedBefore = GC.GetTotalAllocatedBytes(true);
    var stopwatch = Stopwatch.StartNew();
    for (int i = 0; i < iterations; i++)
    {
        await terminal.WriteAsync(payload);
        await terminal.ResetAsync();
    }
    stopwatch.Stop();
    long allocated = GC.GetTotalAllocatedBytes(true) - allocatedBefore;
    double megabytes = Encoding.UTF8.GetByteCount(payload) * (double)iterations / 1_000_000;
    Console.WriteLine($"{name,-6} {megabytes / stopwatch.Elapsed.TotalSeconds,8:F2} MB/s  {allocated / (1024d * 1024d),8:F2} MiB allocated");
}


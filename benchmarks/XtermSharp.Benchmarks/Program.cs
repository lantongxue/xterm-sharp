using System.Diagnostics;
using System.Text;
using SkiaSharp;
using XtermSharp;
using XtermSharp.Rendering;
using XtermSharp.Rendering.Skia;

const int iterations = 20;
string ascii = string.Concat(Enumerable.Repeat("0123456789abcdef ", 4096));
string colored = string.Concat(Enumerable.Repeat("\x1b[38;2;10;20;30mcolor\x1b[0m ", 2048));
string cjk = string.Concat(Enumerable.Repeat("终端仿真性能测试 ", 4096));

await RunAsync("ASCII", ascii);
await RunAsync("SGR", colored);
await RunAsync("CJK", cjk);
await RunRenderingAsync();

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

static async Task RunRenderingAsync()
{
    const int columns = 120;
    const int rows = 40;
    const int renderIterations = 120;
    await using var terminal = new Terminal(new TerminalOptions
    {
        Columns = columns,
        Rows = rows,
        Scrollback = 1000
    });
    using var backend = new SkiaTerminalRenderBackend();
    using var controller = new TerminalRenderController(terminal, backend);
    var viewport = new TerminalViewport(1200, 800);
    using var bitmap = new SKBitmap(1200, 800);
    using var canvas = new SKCanvas(bitmap);

    await RenderDashboardFrameAsync(0);
    long allocatedBefore = GC.GetTotalAllocatedBytes(true);
    var stopwatch = Stopwatch.StartNew();
    int displayCommands = 0;
    for (int iteration = 1; iteration <= renderIterations; iteration++)
    {
        displayCommands = await RenderDashboardFrameAsync(iteration);
    }
    stopwatch.Stop();
    long allocated = GC.GetTotalAllocatedBytes(true) - allocatedBefore;
    Console.WriteLine(
        $"Render {renderIterations / stopwatch.Elapsed.TotalSeconds,8:F2} FPS  " +
        $"{displayCommands,5} commands/frame  {allocated / (1024d * 1024d),8:F2} MiB allocated");

    async Task<int> RenderDashboardFrameAsync(int frameNumber)
    {
        var output = new StringBuilder("\x1b[?25l");
        for (int row = 0; row < rows; row++)
        {
            output.Append($"\x1b[{row + 1};1H");
            for (int segment = 0; segment < 6; segment++)
            {
                int foreground = 16 + (row * 7 + segment * 19 + frameNumber) % 216;
                int background = 16 + (row * 11 + segment * 13 + frameNumber / 3) % 216;
                output.Append($"\x1b[38;5;{foreground};48;5;{background}m");
                for (int column = 0; column < columns / 6; column++)
                {
                    output.Append((char)('!' + (row + segment + column + frameNumber) % 90));
                }
            }
        }
        output.Append("\x1b[0m");
        await terminal.WriteAsync(output.ToString());
        TerminalRenderFrame frame = await controller.PrepareFrameAsync(viewport);
        backend.Render(canvas, frame);
        canvas.Flush();
        return frame.DisplayList.Rows.Sum(row => row.Commands.Length);
    }
}

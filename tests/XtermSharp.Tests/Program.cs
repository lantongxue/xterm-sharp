using System.Diagnostics;
using System.Text;
using XtermSharp;

var tests = new (string Name, Func<Task> Run)[]
{
    ("plain text and snapshot", PlainTextAsync),
    ("cursor after right margin", CursorAfterMarginAsync),
    ("streamed UTF-8", StreamedUtf8Async),
    ("invalid UTF-8 compatibility", InvalidUtf8Async),
    ("wrapping and scrollback", WrappingAndScrollbackAsync),
    ("resize reflow", ResizeReflowAsync),
    ("cursor movement and erase", CursorAndEraseAsync),
    ("C1 controls and sequence cancellation", C1AndCancellationAsync),
    ("SGR attributes and colors", SgrAsync),
    ("alternate screen", AlternateScreenAsync),
    ("terminal modes", ModesAsync),
    ("reverse wrapped backspace", ReverseWrappedBackspaceAsync),
    ("OSC title event", TitleEventAsync),
    ("device status response", DeviceStatusAsync),
    ("custom async parser handlers", ParserExtensionsAsync),
    ("parser handler isolation", ParserHandlerIsolationAsync),
    ("resize ordering", ResizeOrderingAsync),
    ("Unicode providers", UnicodeProvidersAsync),
    ("addon lifecycle", AddonLifecycleAsync),
    ("event subscriber isolation", EventIsolationAsync),
    ("queue admission cancellation", AdmissionCancellationAsync),
    ("upstream escape fixtures", UpstreamFixturesAsync)
};

int failed = 0;
var total = Stopwatch.StartNew();
foreach ((string name, Func<Task> run) in tests)
{
    var stopwatch = Stopwatch.StartNew();
    try
    {
        await run();
        Console.WriteLine($"PASS {name} ({stopwatch.ElapsedMilliseconds} ms)");
    }
    catch (Exception exception)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {name}: {exception}");
    }
}

Console.WriteLine($"{tests.Length - failed}/{tests.Length} tests passed in {total.ElapsedMilliseconds} ms");
return failed == 0 ? 0 : 1;

static async Task PlainTextAsync()
{
    await using var terminal = new Terminal(new TerminalOptions { Columns = 10, Rows = 3 });
    await terminal.WriteAsync("hello");
    TerminalSnapshot snapshot = await terminal.GetSnapshotAsync();
    Equal("hello", snapshot.ActiveBuffer.Lines[0].TranslateToString(true));
    Equal(5, snapshot.ActiveBuffer.CursorX);
    Equal(1L, snapshot.Revision);
}

static async Task CursorAfterMarginAsync()
{
    await using var terminal = new Terminal(new TerminalOptions { Columns = 5, Rows = 2 });
    await terminal.WriteAsync("abcde");
    Equal(5, (await terminal.GetSnapshotAsync()).ActiveBuffer.CursorX);
    await terminal.WriteAsync("f");
    TerminalSnapshot snapshot = await terminal.GetSnapshotAsync();
    Equal(1, snapshot.ActiveBuffer.CursorX);
    Equal(1, snapshot.ActiveBuffer.CursorY);
    True(snapshot.ActiveBuffer.Lines[1].IsWrapped);
}

static async Task StreamedUtf8Async()
{
    await using var terminal = new Terminal(new TerminalOptions { Columns = 10, Rows = 2 });
    byte[] bytes = Encoding.UTF8.GetBytes("A文B");
    await terminal.WriteAsync(bytes.AsMemory(0, 2));
    await terminal.WriteAsync(bytes.AsMemory(2, 2));
    await terminal.WriteAsync(bytes.AsMemory(4));
    TerminalSnapshot snapshot = await terminal.GetSnapshotAsync();
    Equal("A文B", snapshot.ActiveBuffer.Lines[0].TranslateToString(true));
    Equal((byte)2, snapshot.ActiveBuffer.Lines[0].Cells[1].Width);
    Equal((byte)0, snapshot.ActiveBuffer.Lines[0].Cells[2].Width);
}

static async Task InvalidUtf8Async()
{
    await using var terminal = new Terminal(new TerminalOptions { Columns = 10, Rows = 2 });
    await terminal.WriteAsync(new byte[] { (byte)'A', 0xC0, 0xAF, (byte)'B', 0xE2 });
    await terminal.WriteAsync(new byte[] { 0x28, (byte)'C' });
    TerminalSnapshot snapshot = await terminal.GetSnapshotAsync();
    Equal("AB(C", snapshot.ActiveBuffer.Lines[0].TranslateToString(true));
}

static async Task WrappingAndScrollbackAsync()
{
    await using var terminal = new Terminal(new TerminalOptions { Columns = 4, Rows = 2, Scrollback = 10 });
    await terminal.WriteAsync("abcdefghij");
    TerminalSnapshot snapshot = await terminal.GetSnapshotAsync(SnapshotScope.ActiveBuffer);
    Equal(3, snapshot.ActiveBuffer.Lines.Length);
    Equal("abcd", snapshot.ActiveBuffer.Lines[0].TranslateToString());
    Equal("efgh", snapshot.ActiveBuffer.Lines[1].TranslateToString());
    Equal("ij", snapshot.ActiveBuffer.Lines[2].TranslateToString(true));
    True(snapshot.ActiveBuffer.Lines[1].IsWrapped);
    True(snapshot.ActiveBuffer.Lines[2].IsWrapped);
    Equal(1, snapshot.ActiveBuffer.BaseY);
}

static async Task ResizeReflowAsync()
{
    await using var terminal = new Terminal(new TerminalOptions { Columns = 8, Rows = 3, Scrollback = 10 });
    await terminal.WriteAsync("abcdef");
    await terminal.ResizeAsync(4, 3);
    TerminalSnapshot snapshot = await terminal.GetSnapshotAsync();
    Equal("abcd", snapshot.ActiveBuffer.Lines[0].TranslateToString());
    Equal("ef", snapshot.ActiveBuffer.Lines[1].TranslateToString(true));
    Equal(2, snapshot.ActiveBuffer.CursorX);

    await terminal.ResizeAsync(10, 3);
    snapshot = await terminal.GetSnapshotAsync();
    Equal("abcdef", snapshot.ActiveBuffer.Lines[0].TranslateToString(true));
    Equal(6, snapshot.ActiveBuffer.CursorX);
}

static async Task CursorAndEraseAsync()
{
    await using var terminal = new Terminal(new TerminalOptions { Columns = 6, Rows = 2 });
    await terminal.WriteAsync("abcdef\x1b[2D\x1b[K");
    TerminalSnapshot snapshot = await terminal.GetSnapshotAsync();
    Equal("abc", snapshot.ActiveBuffer.Lines[0].TranslateToString(true));
    await terminal.WriteAsync("Z\x1b[2J\x1b[Hok");
    snapshot = await terminal.GetSnapshotAsync();
    Equal("ok", snapshot.ActiveBuffer.Lines[0].TranslateToString(true));
}

static async Task C1AndCancellationAsync()
{
    await using var terminal = new Terminal(new TerminalOptions { Columns = 6, Rows = 3 });
    string? title = null;
    terminal.TitleChanged += (_, args) => title = args.Title;
    await terminal.WriteAsync("abc\u009b2D!\u0085next\u009d2;c1-title\u009c\x1b[2\u0018D");
    TerminalSnapshot snapshot = await terminal.GetSnapshotAsync();
    Equal("a!c", snapshot.ActiveBuffer.Lines[0].TranslateToString(true));
    Equal("nextD", snapshot.ActiveBuffer.Lines[1].TranslateToString(true));
    Equal("c1-title", title);
}

static async Task SgrAsync()
{
    await using var terminal = new Terminal();
    await terminal.WriteAsync("\x1b[1;38;2;1;2;3;48;5;42mX\x1b[0m");
    TerminalCellSnapshot cell = (await terminal.GetSnapshotAsync()).ActiveBuffer.Lines[0].Cells[0];
    True(cell.Attributes.HasFlag(CellAttributes.Bold));
    Equal(TerminalColorMode.Rgb, cell.Foreground.Mode);
    Equal(0x010203, cell.Foreground.Value);
    Equal(TerminalColorMode.Palette, cell.Background.Mode);
    Equal(42, cell.Background.Value);
}

static async Task AlternateScreenAsync()
{
    await using var terminal = new Terminal(new TerminalOptions { Columns = 5, Rows = 2 });
    await terminal.WriteAsync("norm\x1b[?1049h\ralt");
    TerminalSnapshot snapshot = await terminal.GetSnapshotAsync(SnapshotScope.AllBuffers);
    Equal(TerminalBufferKind.Alternate, snapshot.ActiveBufferKind);
    Equal("alt", snapshot.AlternateBuffer!.Lines[0].TranslateToString(true));
    Equal("norm", snapshot.NormalBuffer!.Lines[0].TranslateToString(true));
    await terminal.WriteAsync("\x1b[?1049l");
    snapshot = await terminal.GetSnapshotAsync();
    Equal(TerminalBufferKind.Normal, snapshot.ActiveBufferKind);
    Equal("norm", snapshot.ActiveBuffer.Lines[0].TranslateToString(true));
}

static async Task ModesAsync()
{
    await using var terminal = new Terminal();
    await terminal.WriteAsync("\x1b[?1h\x1b[4h\x1b[?25l\x1b[?2004h\x1b[?1002h");
    TerminalModes modes = (await terminal.GetSnapshotAsync()).Modes;
    True(modes.ApplicationCursorKeys);
    True(modes.Insert);
    False(modes.ShowCursor);
    True(modes.BracketedPaste);
    Equal(TerminalMouseTrackingMode.Drag, modes.MouseTracking);
}

static async Task ReverseWrappedBackspaceAsync()
{
    await using var terminal = new Terminal(new TerminalOptions { Columns = 4, Rows = 3 });
    await terminal.WriteAsync("\x1b[?45habcde\b\bX");
    TerminalSnapshot snapshot = await terminal.GetSnapshotAsync();
    Equal("abcX", snapshot.ActiveBuffer.Lines[0].TranslateToString());
    Equal("e", snapshot.ActiveBuffer.Lines[1].TranslateToString(true));
    False(snapshot.ActiveBuffer.Lines[1].IsWrapped);
}

static async Task TitleEventAsync()
{
    await using var terminal = new Terminal();
    string? title = null;
    terminal.TitleChanged += (_, args) => title = args.Title;
    await terminal.WriteAsync("\x1b]2;build shell\a");
    Equal("build shell", title);
}

static async Task DeviceStatusAsync()
{
    await using var terminal = new Terminal();
    string? response = null;
    terminal.Data += (_, args) => response = args.Data;
    await terminal.WriteAsync("\x1b[5n");
    Equal("\x1b[0n", response);
}

static async Task ParserExtensionsAsync()
{
    await using var terminal = new Terminal();
    int csiValue = 0;
    string? oscValue = null;
    string? dcsValue = null;
    using IDisposable csi = terminal.Parser.RegisterCsiHandler(
        new FunctionIdentifier('z'),
        async parameters =>
        {
            await Task.Yield();
            csiValue = parameters.GetOrDefault(0);
            return true;
        });
    using IDisposable osc = terminal.Parser.RegisterOscHandler(777, data =>
    {
        oscValue = data;
        return ValueTask.FromResult(true);
    });
    using IDisposable dcs = terminal.Parser.RegisterDcsHandler(new FunctionIdentifier('q'), (data, _) =>
    {
        dcsValue = data;
        return ValueTask.FromResult(true);
    });
    await terminal.WriteAsync("\x1b[9z\x1b]777;osc-value\x1b\\\x1bP1qpayload\x1b\\");
    Equal(9, csiValue);
    Equal("osc-value", oscValue);
    Equal("payload", dcsValue);
}

static async Task ParserHandlerIsolationAsync()
{
    var logger = new TestLogger();
    await using var terminal = new Terminal(new TerminalOptions { Logger = logger });
    bool fallbackRan = false;
    using IDisposable fallback = terminal.Parser.RegisterCsiHandler(new FunctionIdentifier('z'), _ =>
    {
        fallbackRan = true;
        return ValueTask.FromResult(true);
    });
    using IDisposable failing = terminal.Parser.RegisterCsiHandler(new FunctionIdentifier('z'), _ =>
        ValueTask.FromException<bool>(new InvalidOperationException("handler failure")));
    await terminal.WriteAsync("\x1b[z");
    True(fallbackRan);
    True(logger.ExceptionCount > 0);
}

static async Task ResizeOrderingAsync()
{
    await using var terminal = new Terminal(new TerminalOptions { Columns = 4, Rows = 2 });
    ValueTask first = terminal.WriteAsync("abcd");
    ValueTask resize = terminal.ResizeAsync(8, 3);
    ValueTask second = terminal.WriteAsync("ef");
    await first;
    await resize;
    await second;
    TerminalSnapshot snapshot = await terminal.GetSnapshotAsync();
    Equal(8, snapshot.Columns);
    Equal(3, snapshot.Rows);
    Equal("abcdef", snapshot.ActiveBuffer.Lines[0].TranslateToString(true));
}

static async Task UnicodeProvidersAsync()
{
    await using var terminal = new Terminal(new TerminalOptions { Columns = 10, Rows = 2 });
    True(terminal.Unicode.Versions.Contains("6"));
    True(terminal.Unicode.Versions.Contains("11"));
    terminal.Unicode.ActiveVersion = "11";
    await terminal.WriteAsync("😀");
    TerminalSnapshot snapshot = await terminal.GetSnapshotAsync();
    Equal((byte)2, snapshot.ActiveBuffer.Lines[0].Cells[0].Width);
}

static async Task AddonLifecycleAsync()
{
    await using var terminal = new Terminal();
    var addon = new TestAddon();
    terminal.LoadAddon(addon);
    True(addon.Activated);
    await terminal.DisposeAsync();
    True(addon.Disposed);
}

static async Task EventIsolationAsync()
{
    var logger = new TestLogger();
    await using var terminal = new Terminal(new TerminalOptions { Logger = logger });
    bool secondRan = false;
    terminal.Bell += (_, _) => throw new InvalidOperationException("subscriber failure");
    terminal.Bell += (_, _) => secondRan = true;
    await terminal.WriteAsync("\a");
    True(secondRan);
    True(logger.ExceptionCount > 0);
}

static async Task AdmissionCancellationAsync()
{
    await using var terminal = new Terminal(new TerminalOptions { MaxPendingInputBytes = 2 });
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    using IDisposable registration = terminal.Parser.RegisterCsiHandler(new FunctionIdentifier('z'), async _ =>
    {
        await release.Task;
        return true;
    });

    ValueTask blockedParser = terminal.WriteAsync("\x1b[z");
    using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
    await ThrowsAsync<OperationCanceledException>(async () => await terminal.WriteAsync("later", cancellation.Token));
    release.SetResult();
    await blockedParser;
}

static async Task UpstreamFixturesAsync()
{
    string fixtureRoot = Path.Combine("xterm.js", "test", "fixtures", "escape_sequence_files");
    string[] fixtures =
    [
        "t0002j-simple_string", "t0003-line_wrap", "t0004-LF", "t0004j-CR", "t0005-CR",
        "t0006-IND", "t0007-space_at_end", "t0008-BS", "t0009-NEL", "t0010-RI",
        "t0011-RI_scroll", "t0012-VT", "t0013-FF", "t0014-CAN", "t0015-SUB",
        "t0016-SU", "t0017-SD", "t0020-CUF", "t0021-CUB", "t0022-CUU",
        "t0023-CUU_scroll", "t0024-CUD", "t0025-CUP", "t0026-CNL", "t0027-CPL",
        "t0030-HPR", "t0032-VPB", "t0033-VPB_scroll", "t0034-VPR", "t0035-HVP",
        "t0040-REP", "t0050-ICH", "t0051-IL", "t0052-DL", "t0053-DCH",
        "t0054-ECH", "t0055-EL", "t0056-ED", "t0057-ED3", "t0060-DECSC",
        "t0061-CSI_s", "t0070-DECSTBM_LF", "t0071-DECSTBM_IND", "t0072-DECSTBM_NEL",
        "t0073-DECSTBM_RI", "t0074-DECSTBM_SU_SD", "t0075-DECSTBM_CUU_CUD",
        "t0076-DECSTBM_IL_DL", "t0077-DECSTBM_quirks", "t0078-DECSTBM_CPL_CNL",
        "t0079-DECSTBM_VPR", "t0080-HT", "t0081-TBC", "t0082-HTS", "t0083-CHT",
        "t0084-CBT", "t0090-alt_screen", "t0091-alt_screen_ED3", "t0092-alt_screen_DECSC",
        "t0100-IRM", "t0101-NLM", "t0102-DECAWM"
    ];
    foreach (string fixture in fixtures)
    {
        await using var terminal = new Terminal(new TerminalOptions
        {
            Columns = 80,
            Rows = 25,
            Scrollback = 1000,
            ConvertEol = true
        });
        await terminal.WriteAsync(await File.ReadAllBytesAsync(Path.Combine(fixtureRoot, fixture + ".in")));
        TerminalSnapshot snapshot = await terminal.GetSnapshotAsync();
        string actual = string.Join('\n', snapshot.ActiveBuffer.Lines.Select(TranslateFixtureLine)) + "\n";
        string expected = await File.ReadAllTextAsync(Path.Combine(fixtureRoot, fixture + ".text"));
        actual = NormalizeFixture(actual);
        expected = NormalizeFixture(expected);
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Fixture {fixture} did not match.\nExpected:\n{expected}\nActual:\n{actual}");
        }
    }
}

static string NormalizeFixture(string value) =>
    string.Join('\n', value.Split('\n').Select(static line => line.TrimEnd(' ')));

static string TranslateFixtureLine(TerminalLineSnapshot line)
{
    int last = -1;
    for (int i = 0; i < line.Cells.Length; i++)
    {
        if (line.Cells[i].Width != 0 && line.Cells[i].CodePoint != 0)
        {
            last = i;
        }
    }
    if (last < 0)
    {
        return string.Empty;
    }
    var builder = new StringBuilder();
    for (int i = 0; i <= last; i++)
    {
        TerminalCellSnapshot cell = line.Cells[i];
        if (cell.Width != 0)
        {
            builder.Append(cell.Text.Length == 0 ? " " : cell.Text);
        }
    }
    return builder.ToString();
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }
}

static void True(bool value)
{
    if (!value)
    {
        throw new InvalidOperationException("Expected true, got false.");
    }
}

static void False(bool value) => True(!value);

static async Task ThrowsAsync<TException>(Func<Task> action) where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException)
    {
        return;
    }
    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}

sealed class TestAddon : ITerminalAddon
{
    public bool Activated { get; private set; }
    public bool Disposed { get; private set; }
    public void Activate(Terminal terminal) => Activated = true;
    public void Dispose() => Disposed = true;
}

sealed class TestLogger : ITerminalLogger
{
    public int ExceptionCount { get; private set; }
    public void Log(TerminalLogLevel level, string message, Exception? exception = null)
    {
        if (exception is not null)
        {
            ExceptionCount++;
        }
    }
}

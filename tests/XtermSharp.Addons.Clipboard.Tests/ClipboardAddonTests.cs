using XtermSharp.Addons.Clipboard.Tests.Support;

namespace XtermSharp.Addons.Clipboard.Tests;

public sealed class ClipboardAddonTests
{
    private const string HelloEncoded = "aGVsbG8gd29ybGQ=";
    private const string HelloDecoded = "hello world";

    [Fact]
    public async Task WritesSimpleString()
    {
        await using var terminal = CreateTerminal();
        var provider = new RecordingClipboardProvider();
        using ClipboardAddon addon = LoadAddon(terminal, provider);

        await WriteAsync(terminal, $"\x1b]52;c;{HelloEncoded}\x07");

        Assert.Equal(HelloDecoded, provider.Text);
        Assert.Equal([("c", HelloDecoded)], provider.Writes);
    }

    [Fact]
    public async Task WritesPrimarySelection()
    {
        await using var terminal = CreateTerminal();
        var provider = new RecordingClipboardProvider();
        using ClipboardAddon addon = LoadAddon(terminal, provider);

        await WriteAsync(terminal, $"\x1b]52;p;{HelloEncoded}\x07");

        Assert.Equal([("p", HelloDecoded)], provider.Writes);
    }

    [Fact]
    public async Task WritesDefaultEmptySelection()
    {
        await using var terminal = CreateTerminal();
        var provider = new RecordingClipboardProvider();
        using ClipboardAddon addon = LoadAddon(terminal, provider);

        await WriteAsync(terminal, $"\x1b]52;;{HelloEncoded}\x07");

        Assert.Equal([("", HelloDecoded)], provider.Writes);
    }

    [Fact]
    public async Task InvalidBase64IsRejectedWithoutChangingClipboard()
    {
        await using var terminal = CreateTerminal();
        var provider = new RecordingClipboardProvider { Text = "existing" };
        using ClipboardAddon addon = LoadAddon(terminal, provider);

        await WriteAsync(terminal, $"\x1b]52;c;{HelloEncoded}invalid\x07");

        Assert.Equal("existing", provider.Text);
        Assert.Empty(provider.Writes);
    }

    [Fact]
    public async Task EmptyPayloadClearsClipboard()
    {
        await using var terminal = CreateTerminal();
        var provider = new RecordingClipboardProvider { Text = HelloDecoded };
        using ClipboardAddon addon = LoadAddon(terminal, provider);

        await WriteAsync(terminal, "\x1b]52;c;\x07");

        Assert.Equal(string.Empty, provider.Text);
    }

    [Fact]
    public async Task ReadsSimpleString()
    {
        await using var terminal = CreateTerminal();
        var provider = new RecordingClipboardProvider { Text = HelloDecoded };
        using ClipboardAddon addon = LoadAddon(terminal, provider);
        List<string> responses = CaptureData(terminal);

        await WriteAsync(terminal, "\x1b]52;c;?\x07");

        Assert.Equal(["c"], provider.Reads);
        Assert.Equal([$"\x1b]52;c;{HelloEncoded}\x07"], responses);
    }

    [Fact]
    public async Task ReadsPrimarySelection()
    {
        await using var terminal = CreateTerminal();
        var provider = new RecordingClipboardProvider { Text = HelloDecoded };
        using ClipboardAddon addon = LoadAddon(terminal, provider);
        List<string> responses = CaptureData(terminal);

        await WriteAsync(terminal, "\x1b]52;p;?\x07");

        Assert.Equal(["p"], provider.Reads);
        Assert.Equal([$"\x1b]52;p;{HelloEncoded}\x07"], responses);
    }

    [Fact]
    public async Task ExclamationPayloadClearsClipboard()
    {
        await using var terminal = CreateTerminal();
        var provider = new RecordingClipboardProvider { Text = HelloDecoded };
        using ClipboardAddon addon = LoadAddon(terminal, provider);

        await WriteAsync(terminal, "\x1b]52;c;!\x07");
        await WriteAsync(terminal, "\x1b]52;c;?\x07");

        Assert.Equal(string.Empty, provider.Text);
    }

    [Fact]
    public async Task WritesNonAsciiUtf8Text()
    {
        const string encoded = "4oKsbWzDpMO8dMOf";
        const string decoded = "€mläütß";
        await using var terminal = CreateTerminal();
        var provider = new RecordingClipboardProvider();
        using ClipboardAddon addon = LoadAddon(terminal, provider);

        await WriteAsync(terminal, $"\x1b]52;c;{encoded}\x07");

        Assert.Equal(decoded, provider.Text);
    }

    [Fact]
    public async Task ReadsNonAsciiUtf8Text()
    {
        const string encoded = "4oKsbWzDpMO8dMOf";
        const string decoded = "€mläütß";
        await using var terminal = CreateTerminal();
        var provider = new RecordingClipboardProvider { Text = decoded };
        using ClipboardAddon addon = LoadAddon(terminal, provider);
        List<string> responses = CaptureData(terminal);

        await WriteAsync(terminal, "\x1b]52;c;?\x07");

        Assert.Equal([$"\x1b]52;c;{encoded}\x07"], responses);
    }

    [Fact]
    public async Task DefaultPolicyDeniesReadsAndWrites()
    {
        await using var terminal = CreateTerminal();
        var provider = new RecordingClipboardProvider { Text = "existing" };
        using var addon = new ClipboardAddon(provider);
        terminal.LoadAddon(addon);
        List<string> responses = CaptureData(terminal);

        await WriteAsync(terminal, $"\x1b]52;c;{HelloEncoded}\x07");
        await WriteAsync(terminal, "\x1b]52;c;?\x07");

        Assert.Equal("existing", provider.Text);
        Assert.Empty(provider.Reads);
        Assert.Empty(provider.Writes);
        Assert.Empty(responses);
    }

    [Fact]
    public async Task ReadAndWritePermissionsAreIndependent()
    {
        await using var writeTerminal = CreateTerminal();
        var writeProvider = new RecordingClipboardProvider();
        using ClipboardAddon writeAddon = LoadAddon(
            writeTerminal,
            writeProvider,
            allowRead: false,
            allowWrite: true);
        List<string> writeResponses = CaptureData(writeTerminal);
        await WriteAsync(writeTerminal, $"\x1b]52;c;{HelloEncoded}\x07");
        await WriteAsync(writeTerminal, "\x1b]52;c;?\x07");

        await using var readTerminal = CreateTerminal();
        var readProvider = new RecordingClipboardProvider { Text = HelloDecoded };
        using ClipboardAddon readAddon = LoadAddon(
            readTerminal,
            readProvider,
            allowRead: true,
            allowWrite: false);
        List<string> readResponses = CaptureData(readTerminal);
        await WriteAsync(readTerminal, "\x1b]52;c;\x07");
        await WriteAsync(readTerminal, "\x1b]52;c;?\x07");

        Assert.Equal(HelloDecoded, writeProvider.Text);
        Assert.Empty(writeProvider.Reads);
        Assert.Empty(writeResponses);
        Assert.Empty(readProvider.Writes);
        Assert.Equal([$"\x1b]52;c;{HelloEncoded}\x07"], readResponses);
    }

    [Fact]
    public async Task PayloadLimitRejectsOversizedWritesAndReads()
    {
        await using var terminal = CreateTerminal();
        var provider = new RecordingClipboardProvider();
        using ClipboardAddon addon = LoadAddon(terminal, provider, maxPayloadBytes: 5);
        List<string> responses = CaptureData(terminal);

        await WriteAsync(terminal, "\x1b]52;c;aGVsbG8=\x07");
        await WriteAsync(terminal, "\x1b]52;c;aGVsbG8h\x07");
        provider.Text = "123456";
        await WriteAsync(terminal, "\x1b]52;c;?\x07");

        Assert.Equal([("c", "hello")], provider.Writes);
        Assert.Empty(responses);
    }

    [Fact]
    public async Task InvalidUtf8IsRejectedAndFollowingSequenceStillParses()
    {
        await using var terminal = CreateTerminal();
        var provider = new RecordingClipboardProvider { Text = "existing" };
        using ClipboardAddon addon = LoadAddon(terminal, provider);

        await WriteAsync(terminal, "\x1b]52;c;/w==\x07");
        await WriteAsync(terminal, $"\x1b]52;c;{HelloEncoded}\x07");

        Assert.Equal(HelloDecoded, provider.Text);
        Assert.Equal([("c", HelloDecoded)], provider.Writes);
    }

    [Fact]
    public async Task InvalidSelectionIsRejected()
    {
        await using var terminal = CreateTerminal();
        var provider = new RecordingClipboardProvider { Text = "existing" };
        using ClipboardAddon addon = LoadAddon(terminal, provider);
        List<string> responses = CaptureData(terminal);

        await WriteAsync(terminal, $"\x1b]52;x;{HelloEncoded}\x07");
        await WriteAsync(terminal, "\x1b]52;x;?\x07");

        Assert.Empty(provider.Reads);
        Assert.Empty(provider.Writes);
        Assert.Empty(responses);
    }

    [Fact]
    public async Task AsyncProviderBlocksWriteUntilResponseIsReady()
    {
        await using var terminal = CreateTerminal();
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new RecordingClipboardProvider
        {
            ReadOverride = (_, cancellationToken) =>
                new ValueTask<string>(completion.Task.WaitAsync(cancellationToken))
        };
        using ClipboardAddon addon = LoadAddon(terminal, provider);
        List<string> responses = CaptureData(terminal);

        Task write = terminal.WriteAsync("\x1b]52;c;?\x07", TestContext.Current.CancellationToken).AsTask();
        await Task.Yield();
        Assert.False(write.IsCompleted);
        completion.SetResult(HelloDecoded);
        await write;

        Assert.Equal([$"\x1b]52;c;{HelloEncoded}\x07"], responses);
    }

    [Fact]
    public async Task DisposingAddonUnregistersOscHandler()
    {
        await using var terminal = CreateTerminal();
        var fallbackPayloads = new List<string>();
        using IDisposable fallback = terminal.Parser.RegisterOscHandler(52, data =>
        {
            fallbackPayloads.Add(data);
            return ValueTask.FromResult(true);
        });
        var provider = new RecordingClipboardProvider();
        ClipboardAddon addon = LoadAddon(terminal, provider);

        await WriteAsync(terminal, $"\x1b]52;c;{HelloEncoded}\x07");
        addon.Dispose();
        await WriteAsync(terminal, $"\x1b]52;c;{HelloEncoded}\x07");

        Assert.Equal([$"c;{HelloEncoded}"], fallbackPayloads);
    }

    [Fact]
    public async Task DisposingAddonSuppressesAQueryResponseWhenProviderIgnoresCancellation()
    {
        await using var terminal = CreateTerminal();
        var readStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new RecordingClipboardProvider
        {
            ReadOverride = (_, _) =>
            {
                readStarted.SetResult();
                return new ValueTask<string>(completion.Task);
            }
        };
        ClipboardAddon addon = LoadAddon(terminal, provider);
        List<string> responses = CaptureData(terminal);

        Task write = terminal.WriteAsync("\x1b]52;c;?\x07", TestContext.Current.CancellationToken).AsTask();
        await readStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        addon.Dispose();
        completion.SetResult(HelloDecoded);
        await write;

        Assert.Empty(responses);
    }

    [Fact]
    public async Task DisableStdinSuppressesClipboardQueryResponses()
    {
        await using var terminal = new Terminal(new TerminalOptions
        {
            Columns = 10,
            Rows = 2,
            DisableStdin = true
        });
        var provider = new RecordingClipboardProvider { Text = HelloDecoded };
        using ClipboardAddon addon = LoadAddon(terminal, provider);
        List<string> responses = CaptureData(terminal);

        await WriteAsync(terminal, "\x1b]52;c;?\x07");

        Assert.Equal(["c"], provider.Reads);
        Assert.Empty(responses);
    }

    private static Terminal CreateTerminal() =>
        new(new TerminalOptions { Columns = 10, Rows = 2 });

    private static ClipboardAddon LoadAddon(
        Terminal terminal,
        IClipboardProvider provider,
        bool allowRead = true,
        bool allowWrite = true,
        int maxPayloadBytes = ClipboardAddonOptions.DefaultMaxPayloadBytes)
    {
        var addon = new ClipboardAddon(provider, new ClipboardAddonOptions
        {
            AllowRead = allowRead,
            AllowWrite = allowWrite,
            MaxPayloadBytes = maxPayloadBytes
        });
        terminal.LoadAddon(addon);
        return addon;
    }

    private static List<string> CaptureData(Terminal terminal)
    {
        var responses = new List<string>();
        terminal.Data += (_, args) => responses.Add(args.Data);
        return responses;
    }

    private static ValueTask WriteAsync(Terminal terminal, string data) =>
        terminal.WriteAsync(data, TestContext.Current.CancellationToken);
}

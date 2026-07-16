using XtermSharp.Internal;
using XtermSharp.Internal.Parser;

namespace XtermSharp.Tests.InputHandler;

public sealed class KittyKeyboardModeTests
{
    [UpstreamFact("XTJS-0984", "InputHandler InputHandler - kitty keyboard stack limit should evict oldest entry when stack exceeds 16 entries")]
    public async Task Stack_evicts_the_oldest_entry_after_sixteen_pushes()
    {
        TerminalEngine engine = CreateEngine();
        for (int flags = 1; flags <= 20; flags++)
        {
            await engine.WriteAsync($"\x1b[>{flags}u");
        }
        Assert.Equal(16, engine.KittyKeyboardStack.Count);
        Assert.Equal((TerminalKittyKeyboardFlags)4, engine.KittyKeyboardStack[0]);
    }

    [UpstreamFact("XTJS-0985", "InputHandler InputHandler - kitty keyboard buffer switch should maintain separate flags for main and alt screens")]
    public async Task Main_and_alternate_buffers_keep_separate_flags()
    {
        await using var terminal = new Terminal();
        await terminal.WriteAsync("\x1b[>5u");
        Assert.Equal((TerminalKittyKeyboardFlags)5, (await terminal.GetSnapshotAsync()).Modes.KittyKeyboardFlags);
        await terminal.WriteAsync("\x1b[?1049h");
        Assert.Equal(TerminalKittyKeyboardFlags.None, (await terminal.GetSnapshotAsync()).Modes.KittyKeyboardFlags);
        await terminal.WriteAsync("\x1b[>7u\x1b[?1049l");
        Assert.Equal((TerminalKittyKeyboardFlags)5, (await terminal.GetSnapshotAsync()).Modes.KittyKeyboardFlags);
        await terminal.WriteAsync("\x1b[?1049h");
        Assert.Equal((TerminalKittyKeyboardFlags)7, (await terminal.GetSnapshotAsync()).Modes.KittyKeyboardFlags);
    }

    [UpstreamFact("XTJS-0986", "InputHandler InputHandler - kitty keyboard pop reset should reset flags to 0 when stack is emptied")]
    public async Task Popping_an_empty_stack_resets_flags()
    {
        await using var terminal = new Terminal();
        await terminal.WriteAsync("\x1b[>5u\x1b[<10u");
        Assert.Equal(TerminalKittyKeyboardFlags.None, (await terminal.GetSnapshotAsync()).Modes.KittyKeyboardFlags);
    }

    private static TerminalEngine CreateEngine()
    {
        TerminalOptions options = new TerminalOptions().ValidateAndClone();
        var unicode = new UnicodeRegistry(options.UnicodeVersion);
        return new TerminalEngine(options, unicode, new EscapeSequenceParser());
    }
}

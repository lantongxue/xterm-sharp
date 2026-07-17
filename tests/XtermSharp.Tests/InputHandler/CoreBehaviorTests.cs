using System.Text;
using XtermSharp.Internal;
using XtermSharp.Internal.Parser;

namespace XtermSharp.Tests.InputHandler;

public sealed class CoreBehaviorTests
{
    [UpstreamFact("XTJS-0798", "InputHandler save and restore cursor")]
    public async Task SaveAndRestoreCursor_RestoresPositionAndAttributes()
    {
        await using var terminal = CreateTerminal(20, 5);
        await terminal.WriteAsync("\x1b[3;2H\x1b[31m\u001b7\x1b[5;10H\x1b[32m\u001b8X");
        TerminalBufferSnapshot buffer = terminal.Buffer.Active;
        Assert.Equal((2, 2), (buffer.CursorX, buffer.CursorY));
        TerminalCellSnapshot cell = buffer.GetLine(2)!.GetCell(1)!.Value;
        Assert.Equal("X", cell.Text);
        Assert.Equal(TerminalColor.Palette(1), cell.Foreground);
    }

    [UpstreamFact("XTJS-0799", "InputHandler should parse big chunks in smaller subchunks")]
    public async Task LargeWrites_AreParsedThroughBoundedStreamingState()
    {
        await using var terminal = CreateTerminal(100, 5, 10);
        var provider = new CountingUnicodeProvider();
        using IDisposable registration = terminal.Unicode.Register(provider);
        terminal.Unicode.ActiveVersion = provider.Version;

        // xterm.js splits these writes at 131072 code points. XtermSharp has no parse buffer:
        // it streams each Rune directly, so the equivalent invariant is that all four upstream
        // sizes are processed once, in order, without truncation or a large intermediate array.
        int processed = 0;
        foreach (int length in new[] { 5, 10_000, 200_000, 300_000 })
        {
            await terminal.WriteAsync(new string('a', length));
            processed += length;
            Assert.Equal(processed, provider.WidthCalls);
        }

        TerminalBufferSnapshot buffer = terminal.Buffer.Active;
        Assert.Equal(15, buffer.Length);
        Assert.Equal(10, buffer.BaseY);
        Assert.All(buffer.Lines[..^1], line => Assert.Equal(new string('a', 100), line.TranslateToString()));
        Assert.Equal("aaaaa", buffer.Lines[^1].TranslateToString(trimRight: true));
        Assert.Equal(5, buffer.CursorX);
    }

    [UpstreamFact("XTJS-0800", "InputHandler SL/SR/DECIC/DECDC SL (scrollLeft)")]
    public async Task ScrollLeft_ShiftsEveryLineWithinMargins()
    {
        await using var terminal = await FilledFiveColumnBuffer();
        await terminal.WriteAsync("\x1b[ @");
        Assert.Equal(["12345", "2345", "2345", "2345", "2345", "2345"], Lines(terminal, 6));
        await terminal.WriteAsync("\x1b[0 @");
        Assert.Equal(["12345", "345", "345", "345", "345", "345"], Lines(terminal, 6));
        await terminal.WriteAsync("\x1b[2 @");
        Assert.Equal(["12345", "5", "5", "5", "5", "5"], Lines(terminal, 6));
    }

    [UpstreamFact("XTJS-0801", "InputHandler SL/SR/DECIC/DECDC SR (scrollRight)")]
    public async Task ScrollRight_ShiftsEveryLineWithinMargins()
    {
        await using var terminal = await FilledFiveColumnBuffer();
        await terminal.WriteAsync("\x1b[ A");
        Assert.Equal(["12345", " 1234", " 1234", " 1234", " 1234", " 1234"], Lines(terminal, 6));
        await terminal.WriteAsync("\x1b[0 A");
        Assert.Equal(["12345", "  123", "  123", "  123", "  123", "  123"], Lines(terminal, 6));
        await terminal.WriteAsync("\x1b[2 A");
        Assert.Equal(["12345", "    1", "    1", "    1", "    1", "    1"], Lines(terminal, 6));
    }

    [UpstreamFact("XTJS-0802", "InputHandler SL/SR/DECIC/DECDC insertColumns (DECIC)")]
    public async Task InsertColumns_InsertsAtCursorAcrossMargins()
    {
        await using var terminal = await FilledFiveColumnBuffer();
        await terminal.WriteAsync("\x1b[3;3H\x1b['}");
        Assert.Equal(["12345", "12 34", "12 34", "12 34", "12 34", "12 34"], Lines(terminal, 6));

        await terminal.ResetAsync();
        await terminal.WriteAsync(string.Concat(Enumerable.Repeat("12345", 6)));
        await terminal.WriteAsync("\x1b[3;3H\x1b[1'}");
        Assert.Equal(["12345", "12 34", "12 34", "12 34", "12 34", "12 34"], Lines(terminal, 6));

        await terminal.ResetAsync();
        await terminal.WriteAsync(string.Concat(Enumerable.Repeat("12345", 6)));
        await terminal.WriteAsync("\x1b[3;3H\x1b[2'}");
        Assert.Equal(["12345", "12  3", "12  3", "12  3", "12  3", "12  3"], Lines(terminal, 6));
    }

    [UpstreamFact("XTJS-0803", "InputHandler SL/SR/DECIC/DECDC deleteColumns (DECDC)")]
    public async Task DeleteColumns_DeletesAtCursorAcrossMargins()
    {
        await using var terminal = await FilledFiveColumnBuffer();
        await terminal.WriteAsync("\x1b[3;3H\x1b['~");
        Assert.Equal(["12345", "1245", "1245", "1245", "1245", "1245"], Lines(terminal, 6));

        await terminal.ResetAsync();
        await terminal.WriteAsync(string.Concat(Enumerable.Repeat("12345", 6)));
        await terminal.WriteAsync("\x1b[3;3H\x1b[1'~");
        Assert.Equal(["12345", "1245", "1245", "1245", "1245", "1245"], Lines(terminal, 6));

        await terminal.ResetAsync();
        await terminal.WriteAsync(string.Concat(Enumerable.Repeat("12345", 6)));
        await terminal.WriteAsync("\x1b[3;3H\x1b[2'~");
        Assert.Equal(["12345", "125", "125", "125", "125", "125"], Lines(terminal, 6));
    }

    [UpstreamFact("XTJS-0804", "InputHandler BS with reverseWraparound set/unset reverseWraparound set should not reverse outside of scroll margins")]
    public async Task ReverseWraparound_DoesNotCrossOutsideVerticalMargins()
    {
        await using var terminal = CreateTerminal(5, 5, 1);
        await terminal.WriteAsync("#####abcdefghijklmnopqrstuvwxy");
        await terminal.WriteAsync("\x1b[?45h\x1b[2;4r");

        await terminal.WriteAsync("\x1b[5;1H\x08");
        Assert.Equal((0, 4), (terminal.Buffer.Active.CursorX, terminal.Buffer.Active.CursorY));

        await terminal.WriteAsync("\x1b[1;1H\x08");
        Assert.Equal((0, 0), (terminal.Buffer.Active.CursorX, terminal.Buffer.Active.CursorY));

        await terminal.WriteAsync("\x1b[4;1H\x08");
        Assert.Equal(2, terminal.Buffer.Active.CursorY);
        Assert.InRange(terminal.Buffer.Active.CursorX, 3, 4);
    }

    [UpstreamFact("XTJS-0805", "InputHandler DECSC/DECRC - save and restore cursor should save and restore origin mode")]
    public async Task SaveAndRestoreCursor_RestoresOriginMode()
    {
        await using var terminal = new Terminal();
        await terminal.WriteAsync("\x1b[?6h\u001b7\x1b[?6l");
        Assert.False(terminal.Modes.Origin);
        await terminal.WriteAsync("\u001b8");
        Assert.True(terminal.Modes.Origin);
    }

    [UpstreamFact("XTJS-0806", "InputHandler DECSC/DECRC - save and restore cursor should save and restore wraparound mode")]
    public async Task SaveAndRestoreCursor_RestoresWraparoundMode()
    {
        await using var terminal = new Terminal();
        await terminal.WriteAsync("\x1b[?7l\u001b7\x1b[?7h");
        Assert.True(terminal.Modes.Wraparound);
        await terminal.WriteAsync("\u001b8");
        Assert.False(terminal.Modes.Wraparound);
    }

    [UpstreamFact("XTJS-0807", "InputHandler setCursorStyle should call Terminal.setOption with correct params")]
    public async Task SetCursorStyle_MapsAllDecscusrParameters()
    {
        await using var terminal = new Terminal();
        await terminal.WriteAsync("\x1b[0 q");
        Assert.Null(terminal.Modes.CursorStyle);
        Assert.Null(terminal.Modes.CursorBlink);

        (int Value, TerminalCursorStyle Style, bool Blink)[] cases =
        [
            (1, TerminalCursorStyle.Block, true),
            (2, TerminalCursorStyle.Block, false),
            (3, TerminalCursorStyle.Underline, true),
            (4, TerminalCursorStyle.Underline, false),
            (5, TerminalCursorStyle.Bar, true),
            (6, TerminalCursorStyle.Bar, false)
        ];
        foreach ((int value, TerminalCursorStyle style, bool blink) in cases)
        {
            await terminal.WriteAsync($"\x1b[{value} q");
            Assert.Equal(style, terminal.Modes.CursorStyle);
            Assert.Equal(blink, terminal.Modes.CursorBlink);
        }
    }

    [UpstreamFact("XTJS-0808", "InputHandler setMode should toggle bracketedPasteMode")]
    public async Task SetMode_TogglesBracketedPaste()
    {
        await using var terminal = new Terminal();
        await terminal.WriteAsync("\x1b[?2004h");
        Assert.True(terminal.Modes.BracketedPaste);
        await terminal.WriteAsync("\x1b[?2004l");
        Assert.False(terminal.Modes.BracketedPaste);
    }

    [UpstreamFact("XTJS-0809", "InputHandler setMode should toggle colorSchemeUpdates (DECSET 2031)")]
    public async Task SetMode_TogglesColorSchemeUpdates()
    {
        await using var terminal = new Terminal();
        await terminal.WriteAsync("\x1b[?2031h");
        Assert.True(terminal.Modes.ColorSchemeUpdates);
        await terminal.WriteAsync("\x1b[?2031l");
        Assert.False(terminal.Modes.ColorSchemeUpdates);
    }

    [UpstreamFact("XTJS-0810", "InputHandler setMode should not toggle colorSchemeUpdates when colorSchemeQuery is disabled")]
    public async Task SetMode_IgnoresColorSchemeUpdatesWhenQueryDisabled()
    {
        await using var terminal = new Terminal(new TerminalOptions { ColorSchemeQuery = false });
        await terminal.WriteAsync("\x1b[?2031h");
        Assert.False(terminal.Modes.ColorSchemeUpdates);
    }

    [UpstreamFact("XTJS-0811", "InputHandler regression tests insertChars")]
    public async Task Regression_InsertCharsHandlesDefaultAndExplicitCounts()
    {
        await using var terminal = CreateTerminal(80, 30);
        await terminal.WriteAsync(new string('a', 70) + "1234567890");
        await terminal.WriteAsync("\x1b[1;71H\x1b[@");
        Assert.Equal(new string('a', 70) + " 123456789", FullLine(terminal, 0));
        await terminal.WriteAsync("\x1b[1;71H\x1b[1@");
        Assert.Equal(new string('a', 70) + "  12345678", FullLine(terminal, 0));
        await terminal.WriteAsync("\x1b[1;71H\x1b[2@");
        Assert.Equal(new string('a', 70) + "    123456", FullLine(terminal, 0));
        await terminal.WriteAsync("\x1b[1;71H\x1b[10@");
        Assert.Equal(new string('a', 70), TrimmedLine(terminal, 0));
    }

    [UpstreamFact("XTJS-0812", "InputHandler regression tests deleteChars")]
    public async Task Regression_DeleteCharsHandlesDefaultAndExplicitCounts()
    {
        await using var terminal = CreateTerminal(80, 30);
        await terminal.WriteAsync(new string('a', 70) + "1234567890");
        await terminal.WriteAsync("\x1b[1;71H\x1b[P");
        Assert.Equal(new string('a', 70) + "234567890 ", FullLine(terminal, 0));
        await terminal.WriteAsync("\x1b[1;71H\x1b[1P");
        Assert.Equal(new string('a', 70) + "34567890  ", FullLine(terminal, 0));
        await terminal.WriteAsync("\x1b[1;71H\x1b[2P");
        Assert.Equal(new string('a', 70) + "567890    ", FullLine(terminal, 0));
        await terminal.WriteAsync("\x1b[1;71H\x1b[10P");
        Assert.Equal(new string('a', 70), TrimmedLine(terminal, 0));
    }

    [UpstreamFact("XTJS-0813", "InputHandler regression tests eraseInLine")]
    public async Task Regression_EraseInLineHandlesAllModes()
    {
        await using var terminal = CreateTerminal(80, 30);
        await terminal.WriteAsync(new string('a', 240));
        await terminal.WriteAsync("\x1b[1;71H\x1b[0K");
        Assert.Equal(new string('a', 70) + new string(' ', 10), FullLine(terminal, 0));
        await terminal.WriteAsync("\x1b[2;71H\x1b[1K");
        Assert.Equal(new string(' ', 71) + new string('a', 9), FullLine(terminal, 1));
        await terminal.WriteAsync("\x1b[3;71H\x1b[2K");
        Assert.Equal(new string(' ', 80), FullLine(terminal, 2));
    }

    [UpstreamFact("XTJS-0814", "InputHandler regression tests eraseInLine reflow")]
    public async Task Regression_EraseInLineMaintainsOrBreaksWrapAsRequired()
    {
        await using Terminal first = await WrappedEraseTerminal();
        await first.WriteAsync("\x1b[3;41H\x1b[0K");
        Assert.True(first.Buffer.Active.GetLine(2)!.IsWrapped);
        await first.WriteAsync("\x1b[3;1H\x1b[0K");
        Assert.False(first.Buffer.Active.GetLine(2)!.IsWrapped);

        await using Terminal second = await WrappedEraseTerminal();
        await second.WriteAsync("\x1b[3;41H\x1b[1K");
        Assert.True(second.Buffer.Active.GetLine(2)!.IsWrapped);

        await using Terminal third = await WrappedEraseTerminal();
        await third.WriteAsync("\x1b[3;41H\x1b[2K");
        Assert.False(third.Buffer.Active.GetLine(2)!.IsWrapped);
    }

    [UpstreamFact("XTJS-0815", "InputHandler regression tests ED2 with scrollOnEraseInDisplay turned on")]
    public async Task Regression_Ed2CanPushContentIntoScrollback()
    {
        await using var terminal = new Terminal(new TerminalOptions
        {
            Columns = 10,
            Rows = 5,
            Scrollback = 100,
            ScrollOnEraseInDisplay = true
        });
        string line = new('a', 10);
        await terminal.WriteAsync(line + line + "\x1b[2J");
        Assert.Equal(7, terminal.Buffer.Active.Length);
        Assert.Equal(2, terminal.Buffer.Active.BaseY);
        Assert.Equal(line, FullLine(terminal, 0));
        Assert.Equal(line, FullLine(terminal, 1));

        await terminal.WriteAsync("\x1b[5;1H" + line + "\x1b[2J");
        Assert.Equal(12, terminal.Buffer.Active.Length);
    }

    [UpstreamFact("XTJS-0816", "InputHandler regression tests eraseInDisplay")]
    public async Task Regression_EraseInDisplayHandlesModesAndWrappedLines()
    {
        await using var terminal = CreateTerminal(10, 7);
        await terminal.WriteAsync(new string('a', 70));
        await terminal.WriteAsync("\x1b[6;6H\x1b[0J");
        Assert.Equal(new string('a', 10), FullLine(terminal, 4));
        Assert.Equal(new string('a', 5) + new string(' ', 5), FullLine(terminal, 5));
        Assert.Equal(new string(' ', 10), FullLine(terminal, 6));

        await terminal.ResetAsync();
        await terminal.WriteAsync(new string('a', 70) + "\x1b[6;6H\x1b[1J");
        Assert.All(Enumerable.Range(0, 5), row => Assert.Equal(string.Empty, TrimmedLine(terminal, row)));
        Assert.Equal(new string(' ', 6) + new string('a', 4), FullLine(terminal, 5));
        Assert.Equal(new string('a', 10), FullLine(terminal, 6));

        await terminal.ResetAsync();
        await terminal.WriteAsync(new string('a', 70) + "\x1b[6;6H\x1b[2J");
        Assert.All(Enumerable.Range(0, 7), row => Assert.Equal(string.Empty, TrimmedLine(terminal, row)));

        await terminal.ResetAsync();
        await terminal.WriteAsync(new string('a', 10) + new string('a', 19));
        Assert.True(terminal.Buffer.Active.GetLine(2)!.IsWrapped);
        await terminal.WriteAsync("\x1b[3;6H\x1b[1J");
        Assert.False(terminal.Buffer.Active.GetLine(2)!.IsWrapped);
    }

    [UpstreamFact("XTJS-0817", "InputHandler print should not cause an infinite loop (regression test)")]
    public async Task Print_ZeroWidthSpaceDoesNotLoopOrAddLines()
    {
        await using var terminal = new Terminal();
        int before = terminal.Buffer.Active.Length;
        await terminal.WriteAsync("\u200B");
        Assert.Equal(0, terminal.Buffer.Active.CursorY);
        Assert.Equal(before, terminal.Buffer.Active.Length);
    }

    [UpstreamFact("XTJS-0818", "InputHandler print should join combining characters in a single print")]
    public async Task Print_JoinsCombiningCharactersWithinOneWrite()
    {
        await using var terminal = new Terminal();
        await terminal.WriteAsync("e\u0301");
        Assert.Equal("e\u0301", TrimmedLine(terminal, 0));
        Assert.Equal(1, terminal.Buffer.Active.CursorX);
    }

    [UpstreamFact("XTJS-0819", "InputHandler print should join combining characters split across parse calls")]
    public async Task Print_JoinsCombiningCharactersAcrossWrites()
    {
        await using var terminal = new Terminal();
        await terminal.WriteAsync("e");
        await terminal.WriteAsync("\u0301");
        Assert.Equal("e\u0301", TrimmedLine(terminal, 0));
        Assert.Equal(1, terminal.Buffer.Active.CursorX);
    }

    [UpstreamFact("XTJS-0820", "InputHandler print should repeat preceding grapheme cluster via REP")]
    public async Task Print_RepRepeatsPrecedingGraphemeCluster()
    {
        await using var terminal = new Terminal();
        await terminal.WriteAsync("e\u0301\x1b[2b");
        Assert.Equal("e\u0301e\u0301e\u0301", TrimmedLine(terminal, 0));
        Assert.Equal(3, terminal.Buffer.Active.CursorX);
    }

    [UpstreamFact("XTJS-0821", "InputHandler print should not repeat when REP has no preceding join state")]
    public async Task Print_RepWithoutPrecedingStateDoesNothing()
    {
        await using var terminal = new Terminal();
        await terminal.WriteAsync("\x1b[2b");
        Assert.Equal(string.Empty, TrimmedLine(terminal, 0));
        Assert.Equal(0, terminal.Buffer.Active.CursorX);
    }

    [UpstreamFact("XTJS-0822", "InputHandler print should not repeat after an intervening escape sequence")]
    public async Task Print_RepDoesNotCrossInterveningEscapeSequence()
    {
        await using var terminal = new Terminal();
        await terminal.WriteAsync("a\x1b[0m\x1b[2b");
        Assert.Equal("a", TrimmedLine(terminal, 0));
        Assert.Equal(1, terminal.Buffer.Active.CursorX);
    }

    [UpstreamFact("XTJS-0823", "InputHandler print should clear cells to the right on early wrap-around")]
    public async Task Print_EarlyWideWrapClearsCellsToTheRight()
    {
        TerminalOptions options = new TerminalOptions { Columns = 5, Rows = 5, Scrollback = 1 }.ValidateAndClone();
        var unicode = new UnicodeRegistry(options.UnicodeVersion);
        var parser = new EscapeSequenceParser();
        using var engine = new TerminalEngine(options, unicode, parser);
        await engine.WriteAsync("12345");
        engine.ActiveBuffer.CursorX = 0;
        engine.ActiveBuffer.WrapPending = false;
        await engine.WriteAsync("￥￥￥");
        TerminalSnapshot snapshot = engine.CreateSnapshot(1, SnapshotScope.ActiveBuffer);
        Assert.Equal("￥￥", snapshot.ActiveBuffer.GetLine(0)!.TranslateToString(true));
        Assert.Equal("￥", snapshot.ActiveBuffer.GetLine(1)!.TranslateToString(true));
    }

    [UpstreamFact("XTJS-0824", "InputHandler print should strip soft hyphens (U+00AD)")]
    public async Task Print_StripsSoftHyphens()
    {
        await using var terminal = new Terminal();
        await terminal.WriteAsync("Soft\u00ADhy\u00ADphen");
        Assert.Equal("Softhyphen", TrimmedLine(terminal, 0));
        Assert.Equal(10, terminal.Buffer.Active.CursorX);
    }

    [UpstreamFact("XTJS-0825", "InputHandler ISO-2022 character sets should map G0 line drawing via ESC ( 0")]
    public async Task Iso2022_MapsG0LineDrawingCharset()
    {
        await using var terminal = new Terminal();
        await terminal.WriteAsync("\x1b(0q\x1b(Bq");
        Assert.Equal("─q", TrimmedLine(terminal, 0));
    }

    [UpstreamFact("XTJS-0826", "InputHandler ISO-2022 character sets should map G1 line drawing after ESC ) 0 and SO")]
    public async Task Iso2022_MapsG1AfterShiftOutAndReturnsWithShiftIn()
    {
        await using var terminal = new Terminal();
        await terminal.WriteAsync("\x1b)0\x0Eq\x0F\x1b(Bq");
        Assert.Equal("─q", TrimmedLine(terminal, 0));
    }

    [UpstreamFact("XTJS-0827", "InputHandler ISO-2022 character sets should restore charset and glevel on ESC 7 / ESC 8")]
    public async Task Iso2022_SaveAndRestoreCursorRestoresCharsetAndGLevel()
    {
        await using var terminal = new Terminal();
        await terminal.WriteAsync("\x1b)0\x0E\u001b7\x0F\x1b(B\u001b8q");
        Assert.Equal("─", TrimmedLine(terminal, 0));
    }

    [UpstreamFact("XTJS-0828", "InputHandler alt screen should handle DECSET/DECRST 47 (alt screen buffer)")]
    public async Task AltScreen_Mode47SwitchesBufferAndCarriesCursorAndAttributes()
    {
        await using var terminal = CreateTerminal(80, 30);
        await terminal.WriteAsync("\x1b[?47h\r\n\x1b[31mJUNK\x1b[?47lTEST");
        Assert.Equal(string.Empty, TrimmedLine(terminal, 0));
        Assert.Equal("    TEST", TrimmedLine(terminal, 1));
        Assert.Equal(TerminalColor.Palette(1), terminal.Buffer.Active.GetLine(1)!.GetCell(4)!.Value.Foreground);
    }

    [UpstreamFact("XTJS-0829", "InputHandler alt screen should handle DECSET/DECRST 1047 (alt screen buffer)")]
    public async Task AltScreen_Mode1047SwitchesBufferAndCarriesCursorAndAttributes()
    {
        await using var terminal = CreateTerminal(80, 30);
        await terminal.WriteAsync("\x1b[?1047h\r\n\x1b[31mJUNK\x1b[?1047lTEST");
        Assert.Equal(string.Empty, TrimmedLine(terminal, 0));
        Assert.Equal("    TEST", TrimmedLine(terminal, 1));
        Assert.Equal(TerminalColor.Palette(1), terminal.Buffer.Active.GetLine(1)!.GetCell(4)!.Value.Foreground);
    }

    [UpstreamFact("XTJS-0830", "InputHandler alt screen should handle DECSET/DECRST 1048 (alt screen cursor)")]
    public async Task AltScreen_Mode1048SavesAndRestoresCursorAndAttributesOnly()
    {
        await using var terminal = CreateTerminal(80, 30);
        await terminal.WriteAsync("\x1b[?1048h\r\n\x1b[31mJUNK\x1b[?1048lTEST");
        Assert.Equal("TEST", TrimmedLine(terminal, 0));
        Assert.Equal("JUNK", TrimmedLine(terminal, 1));
        Assert.Equal(TerminalColor.Default, terminal.Buffer.Active.GetLine(0)!.GetCell(0)!.Value.Foreground);
        Assert.Equal(TerminalColor.Palette(1), terminal.Buffer.Active.GetLine(1)!.GetCell(0)!.Value.Foreground);
    }

    [UpstreamFact("XTJS-0831", "InputHandler alt screen should handle DECSET/DECRST 1049 (alt screen buffer+cursor)")]
    public async Task AltScreen_Mode1049SwitchesBufferAndRestoresCursorAndAttributes()
    {
        await using var terminal = CreateTerminal(80, 30);
        await terminal.WriteAsync("\x1b[?1049h\r\n\x1b[31mJUNK\x1b[?1049lTEST");
        Assert.Equal("TEST", TrimmedLine(terminal, 0));
        Assert.Equal(string.Empty, TrimmedLine(terminal, 1));
        Assert.Equal(TerminalColor.Default, terminal.Buffer.Active.GetLine(0)!.GetCell(0)!.Value.Foreground);
    }

    [UpstreamFact("XTJS-0832", "InputHandler alt screen should handle DECSET/DECRST 1049 - maintains saved cursor for alt buffer")]
    public async Task AltScreen_Mode1049MaintainsIndependentAlternateSavedCursor()
    {
        await using var terminal = CreateTerminal(80, 30);
        await terminal.WriteAsync("\x1b[?1049h\r\n\x1b[31m\x1b[s\x1b[?1049lTEST");
        Assert.Equal("TEST", TrimmedLine(terminal, 0));
        Assert.Equal(TerminalColor.Default, terminal.Buffer.Active.GetLine(0)!.GetCell(0)!.Value.Foreground);
        await terminal.WriteAsync("\x1b[?1049h\x1b[uTEST");
        Assert.Equal("TEST", terminal.Buffer.Active.GetLine(1)!.TranslateToString(true));
        Assert.Equal(TerminalColor.Palette(1), terminal.Buffer.Active.GetLine(1)!.GetCell(0)!.Value.Foreground);
    }

    [UpstreamFact("XTJS-0833", "InputHandler alt screen should handle DECSET/DECRST 1049 - clears alt buffer with erase attributes")]
    public async Task AltScreen_Mode1049ClearsAlternateWithCurrentEraseAttributes()
    {
        await using var terminal = CreateTerminal(80, 30);
        await terminal.WriteAsync("\x1b[42m\x1b[?1049h");
        TerminalCellSnapshot cell = terminal.Buffer.Active.GetLine(20)!.GetCell(10)!.Value;
        Assert.Equal(TerminalColor.Palette(2), cell.Background);
    }

    private static Terminal CreateTerminal(int columns, int rows, int scrollback = 1000) =>
        new(new TerminalOptions { Columns = columns, Rows = rows, Scrollback = scrollback });

    private static async Task<Terminal> FilledFiveColumnBuffer()
    {
        Terminal terminal = CreateTerminal(5, 5, 1);
        await terminal.WriteAsync(string.Concat(Enumerable.Repeat("12345", 6)));
        return terminal;
    }

    private static string[] Lines(Terminal terminal, int count) =>
        terminal.Buffer.Active.Lines.Take(count).Select(line => line.TranslateToString(true)).ToArray();

    private static string FullLine(Terminal terminal, int row) =>
        terminal.Buffer.Active.GetLine(row)!.TranslateToString();

    private static string TrimmedLine(Terminal terminal, int row) =>
        terminal.Buffer.Active.GetLine(row)!.TranslateToString(true);

    private static async Task<Terminal> WrappedEraseTerminal()
    {
        Terminal terminal = CreateTerminal(80, 30);
        await terminal.WriteAsync(new string('a', 80));
        await terminal.WriteAsync(new string('a', 89));
        return terminal;
    }

}

namespace XtermSharp.Tests.InputHandler;

public sealed class ModeAndProtectionTests
{
    [UpstreamFact("XTJS-0972", "InputHandler DECSCA and DECSED/DECSEL default is unprotected")]
    public async Task Default_cells_are_unprotected()
    {
        await using var terminal = NewTerminal();
        await terminal.WriteAsync("some text\x1b[?2K");
        Assert.Equal(string.Empty, Line(await terminal.GetSnapshotAsync(), 0));
        await terminal.WriteAsync("some text\x1b[?2J");
        Assert.All((await terminal.GetSnapshotAsync()).ActiveBuffer.Lines, line =>
            Assert.Equal(string.Empty, line.TranslateToString(trimRight: true)));
    }

    [UpstreamFact("XTJS-0973", "InputHandler DECSCA and DECSED/DECSEL DECSCA 1 with DECSEL")]
    public async Task Selective_line_erase_preserves_protected_cells()
    {
        await using var terminal = NewTerminal();
        await terminal.WriteAsync("###\x1b[1\"qlineerase\x1b[0\"q***\x1b[?2K");
        Assert.Equal("   lineerase", Line(await terminal.GetSnapshotAsync(), 0));
        await terminal.WriteAsync("\x1b[2K");
        Assert.Equal(string.Empty, Line(await terminal.GetSnapshotAsync(), 0));
    }

    [UpstreamFact("XTJS-0974", "InputHandler DECSCA and DECSED/DECSEL DECSCA 1 with DECSED")]
    public async Task Selective_display_erase_preserves_protected_cells()
    {
        await using var terminal = NewTerminal();
        await terminal.WriteAsync("###\x1b[1\"qdisplayerase\x1b[0\"q***\x1b[?2J");
        Assert.Equal("   displayerase", Line(await terminal.GetSnapshotAsync(), 0));
        await terminal.WriteAsync("\x1b[2J");
        Assert.Equal(string.Empty, Line(await terminal.GetSnapshotAsync(), 0));
    }

    [UpstreamFact("XTJS-0975", "InputHandler DECSCA and DECSED/DECSEL DECRQSS reports correct DECSCA state")]
    public async Task Status_string_reports_current_protection_state()
    {
        await using var terminal = NewTerminal();
        var reports = new List<string>();
        terminal.Data += (_, args) => reports.Add(args.Data);
        await terminal.WriteAsync("\x1bP$q\"q\x1b\\");
        Assert.Equal("\x1bP1$r0\"q\x1b\\", reports[^1]);
        await terminal.WriteAsync("\x1b[1\"q\x1bP$q\"q\x1b\\");
        Assert.Equal("\x1bP1$r1\"q\x1b\\", reports[^1]);
        await terminal.WriteAsync("\x1b[2\"q\x1bP$q\"q\x1b\\");
        Assert.Equal("\x1bP1$r0\"q\x1b\\", reports[^1]);
    }

    [UpstreamFact("XTJS-0976", "InputHandler DECRQM ANSI 2 (keyboard action mode)")]
    public async Task Keyboard_action_mode_is_permanently_reset()
    {
        Assert.Equal("\x1b[2;4$y", await QueryAsync("\x1b[2$p"));
    }

    [UpstreamFact("XTJS-0977", "InputHandler DECRQM ANSI 4 (insert mode)")]
    public async Task Insert_mode_query_tracks_set_and_reset()
    {
        await using var terminal = NewTerminal();
        var reports = CaptureReports(terminal);
        await terminal.WriteAsync("\x1b[4$p\x1b[4h\x1b[4$p\x1b[4l\x1b[4$p");
        Assert.Equal(["\x1b[4;2$y", "\x1b[4;1$y", "\x1b[4;2$y"], reports);
    }

    [UpstreamFact("XTJS-0978", "InputHandler DECRQM ANSI 12 (send/receive)")]
    public async Task Send_receive_mode_is_permanently_set()
    {
        Assert.Equal("\x1b[12;3$y", await QueryAsync("\x1b[12$p"));
    }

    [UpstreamFact("XTJS-0979", "InputHandler DECRQM ANSI 20 (newline mode)")]
    public async Task Newline_mode_query_tracks_set_and_reset()
    {
        await using var terminal = NewTerminal();
        var reports = CaptureReports(terminal);
        await terminal.WriteAsync("\x1b[20$p\x1b[20h\x1b[20$p\x1b[20l\x1b[20$p");
        Assert.Equal(["\x1b[20;2$y", "\x1b[20;1$y", "\x1b[20;2$y"], reports);
    }

    [UpstreamFact("XTJS-0980", "InputHandler DECRQM ANSI unknown")]
    public async Task Unknown_ansi_mode_is_not_recognized()
    {
        Assert.Equal("\x1b[1234;0$y", await QueryAsync("\x1b[1234$p"));
    }

    [UpstreamFact("XTJS-0981", "InputHandler DECRQM DEC privates with set/reset semantic")]
    public async Task Private_modes_report_their_live_set_reset_state()
    {
        int[] modes = [1, 6, 9, 45, 66, 1000, 1002, 1003, 1004, 1006, 1016, 47, 1047, 1049, 2004, 2026];
        foreach (int mode in modes)
        {
            await using var terminal = NewTerminal();
            var reports = CaptureReports(terminal);
            await terminal.WriteAsync($"\x1b[?{mode}$p\x1b[?{mode}h\x1b[?{mode}$p\x1b[?{mode}l\x1b[?{mode}$p");
            Assert.Equal(
                [$"\x1b[?{mode};2$y", $"\x1b[?{mode};1$y", $"\x1b[?{mode};2$y"],
                reports);
        }

        foreach (int mode in new[] { 7, 25 })
        {
            await using var terminal = NewTerminal();
            var reports = CaptureReports(terminal);
            await terminal.WriteAsync($"\x1b[?{mode}$p\x1b[?{mode}l\x1b[?{mode}$p\x1b[?{mode}h\x1b[?{mode}$p");
            Assert.Equal(
                [$"\x1b[?{mode};1$y", $"\x1b[?{mode};2$y", $"\x1b[?{mode};1$y"],
                reports);
        }
    }

    [UpstreamFact("XTJS-0982", "InputHandler DECRQM DEC privates quirks")]
    public async Task Cursor_blink_mode_stays_reset_without_the_browser_quirk()
    {
        await using var terminal = NewTerminal();
        var reports = CaptureReports(terminal);
        await terminal.WriteAsync("\x1b[?12$p\x1b[?12h\x1b[?12$p");
        Assert.Equal(["\x1b[?12;2$y", "\x1b[?12;2$y"], reports);
    }

    [UpstreamFact("XTJS-0983", "InputHandler DECRQM DEC privates perma modes")]
    public async Task Permanent_private_modes_report_locked_states()
    {
        (int Mode, int State)[] values = [(3, 0), (8, 3), (67, 4), (1005, 4), (1015, 4), (1048, 1)];
        foreach ((int mode, int state) in values)
        {
            Assert.Equal($"\x1b[?{mode};{state}$y", await QueryAsync($"\x1b[?{mode}$p"));
        }
    }

    private static Terminal NewTerminal() => new(new TerminalOptions { Columns = 20, Rows = 2 });

    private static string Line(TerminalSnapshot snapshot, int row) =>
        snapshot.ActiveBuffer.Lines[row].TranslateToString(trimRight: true);

    private static List<string> CaptureReports(Terminal terminal)
    {
        var reports = new List<string>();
        terminal.Data += (_, args) => reports.Add(args.Data);
        return reports;
    }

    private static async Task<string> QueryAsync(string query)
    {
        await using var terminal = NewTerminal();
        string? report = null;
        terminal.Data += (_, args) => report = args.Data;
        await terminal.WriteAsync(query);
        return Assert.IsType<string>(report);
    }
}

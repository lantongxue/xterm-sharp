using System.Text;

namespace XtermSharp.Tests.Fixtures;

public sealed class EscapeSequenceFixtureTests
{
    // These historical .text files encode pre-6.0 behavior. The expected viewport below was
    // captured from the pinned b1aee19a headless build and is also checked by the differential tool.
    private static readonly IReadOnlyDictionary<string, string[]> PinnedOracleOverrides =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["t0012-VT"] =
            [
                "7", "8", "9", "0", "a", "b", "c", "d", "e", "f", "g", "h",
                "i", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t"
            ],
            ["t0013-FF"] =
            [
                "8", "9", "0", "a", "b", "c", "d", "e", "f", "g", "h", "i",
                "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t"
            ],
            ["t0055-EL"] =
            [
                "abcdefghijklmnopqrstuvwxyz0123456789ABC>",
                "abcdefghijklmnopqrstuvwxyz0123456789ABC>",
                "                                         FGHIJKLMNOPQRSTUVWXYZ0123456789abcdefgh",
                "",
                "abcdefghijklmnopqrstuvwxyz0123456789ABC>!",
                "                                        !FGHIJKLMNOPQRSTUVWXYZ0123456789abcdefgh",
                "                                        !",
                "abcdefghijklmnopqrstuvwxyz0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefgh",
                "!"
            ],
            ["t0084-CBT"] =
            [
                "a       b       c       d       e       f       g       h       i       j      k",
                "a                                       f               h       i   <",
                "a",
                "",
                "default tab stops:",
                "near end:                                                               !bcdefg",
                "at end:                                                                 abcdefgh",
                "!",
                "at end (2):                                                             abcdefgh",
                "!",
                "at end with clipping:                                                   abcdefg!",
                "at end with clipping (2):                                               abcdefg!",
                "",
                "set tab stop at column 80:                                                     v",
                "at end:                                                                 abcdefgh",
                "!",
                "at end (2):                                                             abcdefgh",
                "!",
                "at end with clipping:                                                   abcdefg!",
                "at end with clipping (2):                                               abcdefg!"
            ],
            ["t0101-NLM"] =
            [
                "a", "b", "c", "d", "e", "f", "g", "h", "k", " l", "  m", "   n",
                "    o", "     p"
            ],
            ["t0103-reverse_wrap"] = ["the endreally!", "A", "", "-B", "", "C!"],
            ["t0504-vim"] =
            [
                "t0007-space_at_end.text   t0026-CNL.text         t0060-DECSC.in",
                "t0008-BS.in               t0027-CPL.in           t0060-DECSC.text",
                "t0008-BS.text             t0027-CPL.text         t0061-CSI_s.in",
                "t0009-NEL.in              t0030-HPR.in           t0061-CSI_s.text",
                "t0009-NEL.text            t0030-HPR.text         t008x-alt_screen_ED.in",
                "t0010-RI.in               t0031-HPB.in           t008x-IRM.in",
                "t0010-RI.text             t0031-HPB.text         t008x-NLM.in",
                "t0011-RI_scroll.in        t0032-VPB.in           t008x-save_cursor_mode.in",
                "t0011-RI_scroll.text      t0032-VPB.text         t0500-bash_long_line.in",
                "t0012-VT.in               t0033-VPB_scroll.in    t0500-bash_long_line.text",
                "t0012-VT.text             t0033-VPB_scroll.text  t0501-bash_ls.in",
                "t0013-FF.in               t0034-VPR.in           t0501-bash_ls.text",
                "t0013-FF.text             t0034-VPR.text         t0502-bash_ls_color.in",
                "t0014-CAN.in              t0035-HVP.in           t0502-bash_ls_color.text",
                "t0014-CAN.text            t0035-HVP.text         t0503-zsh_ls_color.in",
                "t0015-SUB.in              t0040-REP.in           t0503-zsh_ls_color.text",
                "t0015-SUB.text            t0040-REP.text         typescript",
                "t0016-SU.in               t0050-ICH.in",
                "$ vim                                                      ~/vt100-to-html/test",
                "$ echo Yes!                                                ~/vt100-to-html/test",
                "Yes!",
                "$                                                          ~/vt100-to-html/test",
                "",
                "Script done on Sun 15 Aug 2010 11:54:14 PM EDT",
                ""
            ]
        };

    private static readonly string FixtureRoot = Path.Combine(
        FindRepositoryRoot(AppContext.BaseDirectory),
        "xterm.js",
        "test",
        "fixtures",
        "escape_sequence_files");

    public static TheoryData<string> Cases { get; } = CreateCases();

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Matches_upstream_escape_sequence_fixture(string fixture)
    {
        await using var terminal = new Terminal(new TerminalOptions
        {
            Columns = 80,
            Rows = 25,
            Scrollback = 1000,
            ConvertEol = true
        });

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        byte[] input = await File.ReadAllBytesAsync(Path.Combine(FixtureRoot, fixture + ".in"), cancellationToken);
        await terminal.WriteAsync(input, cancellationToken);
        TerminalSnapshot snapshot = await terminal.GetSnapshotAsync(cancellationToken: cancellationToken);
        string actual = string.Join('\n', snapshot.ActiveBuffer.Lines.Select(TranslateLine)) + "\n";
        string expected = PinnedOracleOverrides.TryGetValue(fixture, out string[]? oracleLines)
            ? ViewportText(oracleLines)
            : await File.ReadAllTextAsync(Path.Combine(FixtureRoot, fixture + ".text"), cancellationToken);

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    private static TheoryData<string> CreateCases()
    {
        var data = new TheoryData<string>();
        foreach (string inputPath in Directory.EnumerateFiles(FixtureRoot, "*.in").Order(StringComparer.Ordinal))
        {
            string fixture = Path.GetFileNameWithoutExtension(inputPath);
            Assert.True(File.Exists(Path.Combine(FixtureRoot, fixture + ".text")), $"Missing expected output for {fixture}.");
            data.Add(new TheoryDataRow<string>(fixture) { TestDisplayName = $"fixture {fixture}" });
        }
        Assert.Equal(76, data.Count);
        return data;
    }

    private static string Normalize(string value) =>
        string.Join('\n', value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(static line => line.TrimEnd(' ')));

    private static string ViewportText(IReadOnlyList<string> lines)
    {
        var viewport = new string[25];
        for (int index = 0; index < lines.Count; index++)
        {
            viewport[index] = lines[index];
        }
        Array.Fill(viewport, string.Empty, lines.Count, viewport.Length - lines.Count);
        return string.Join('\n', viewport) + "\n";
    }

    private static string TranslateLine(TerminalLineSnapshot line)
    {
        int last = -1;
        for (int index = 0; index < line.Cells.Length; index++)
        {
            if (line.Cells[index].Width != 0 && line.Cells[index].CodePoint != 0)
            {
                last = index;
            }
        }
        if (last < 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (int index = 0; index <= last; index++)
        {
            TerminalCellSnapshot cell = line.Cells[index];
            if (cell.Width != 0)
            {
                builder.Append(cell.Text.Length == 0 ? ' ' : cell.Text);
            }
        }
        return builder.ToString();
    }

    private static string FindRepositoryRoot(string start)
    {
        DirectoryInfo? directory = new(start);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "XtermSharp.sln")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the XtermSharp repository root.");
    }
}

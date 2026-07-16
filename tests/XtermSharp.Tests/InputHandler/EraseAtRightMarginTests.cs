namespace XtermSharp.Tests.InputHandler;

public sealed class EraseAtRightMarginTests
{
    private const string FullLine = "##########";

    [UpstreamFact("XTJS-0964", "InputHandler EL/ED cursor at buffer.cols cursor should stay at cols / does not overflow EL0")]
    public async Task El0_PreservesLineAndLogicalCursorAtRightMargin()
    {
        TerminalSnapshot snapshot = await WriteAndSnapshotAsync("\x1b[0K");

        Assert.Equal(10, snapshot.ActiveBuffer.CursorX);
        Assert.Equal([FullLine, "", "", "", ""], Lines(snapshot));
    }

    [UpstreamFact("XTJS-0965", "InputHandler EL/ED cursor at buffer.cols cursor should stay at cols / does not overflow EL1")]
    public async Task El1_ErasesThroughRightMarginWithoutMovingCursor()
    {
        TerminalSnapshot snapshot = await WriteAndSnapshotAsync("\x1b[1K");

        Assert.Equal(10, snapshot.ActiveBuffer.CursorX);
        Assert.Equal(["", "", "", "", ""], Lines(snapshot));
    }

    [UpstreamFact("XTJS-0966", "InputHandler EL/ED cursor at buffer.cols cursor should stay at cols / does not overflow EL2")]
    public async Task El2_ErasesWholeLineWithoutMovingCursor()
    {
        TerminalSnapshot snapshot = await WriteAndSnapshotAsync("\x1b[2K");

        Assert.Equal(10, snapshot.ActiveBuffer.CursorX);
        Assert.Equal(["", "", "", "", ""], Lines(snapshot));
    }

    [UpstreamFact("XTJS-0967", "InputHandler EL/ED cursor at buffer.cols cursor should stay at cols / does not overflow ED0")]
    public async Task Ed0_PreservesCurrentLineAtRightMargin()
    {
        TerminalSnapshot snapshot = await WriteAndSnapshotAsync("\x1b[0J");

        Assert.Equal(10, snapshot.ActiveBuffer.CursorX);
        Assert.Equal([FullLine, "", "", "", ""], Lines(snapshot));
    }

    [UpstreamFact("XTJS-0968", "InputHandler EL/ED cursor at buffer.cols cursor should stay at cols / does not overflow ED1")]
    public async Task Ed1_ErasesThroughCurrentRightMarginWithoutMovingCursor()
    {
        TerminalSnapshot snapshot = await WriteAndSnapshotAsync("\x1b[1J");

        Assert.Equal(10, snapshot.ActiveBuffer.CursorX);
        Assert.Equal(["", "", "", "", ""], Lines(snapshot));
    }

    [UpstreamFact("XTJS-0969", "InputHandler EL/ED cursor at buffer.cols cursor should stay at cols / does not overflow ED2")]
    public async Task Ed2_ErasesViewportWithoutMovingCursor()
    {
        TerminalSnapshot snapshot = await WriteAndSnapshotAsync("\x1b[2J");

        Assert.Equal(10, snapshot.ActiveBuffer.CursorX);
        Assert.Equal(["", "", "", "", ""], Lines(snapshot));
    }

    [UpstreamFact("XTJS-0970", "InputHandler EL/ED cursor at buffer.cols cursor should stay at cols / does not overflow ED3")]
    public async Task Ed3_ClearsOnlyScrollbackAndDoesNotMoveCursor()
    {
        TerminalSnapshot snapshot = await WriteAndSnapshotAsync("\x1b[3J");

        Assert.Equal(10, snapshot.ActiveBuffer.CursorX);
        Assert.Equal([FullLine, "", "", "", ""], Lines(snapshot));
    }

    [UpstreamFact("XTJS-0971", "InputHandler EL/ED cursor at buffer.cols following sequence keeps working cursor never advances beyond cols")]
    public async Task FollowingControlSequences_NeverAdvanceCursorBeyondColumns()
    {
        string[] sequences =
        [
            "\x1b[10@",
            "\x1b[10 @",
            "\x1b[10A",
            "\x1b[10 A",
            "\x1b[10B",
            "\x1b[10C",
            "\x1b[10D",
            "\x1b[10E",
            "\x1b[10F",
            "\x1b[10G",
            "\x1b[10;10H",
            "\x1b[10I",
            "\x1b[10L",
            "\x1b[10M",
            "\x1b[10P",
            "\x1b[10S",
            "\x1b[10T",
            "\x1b[10X",
            "\x1b[10Z",
            "\x1b[10`",
            "\x1b[10a",
            "\x1b[10b",
            "\x1b[10d",
            "\x1b[10e",
            "\x1b[10;10f",
            "\x1b[0g",
            "\x1b[s",
            "\x1b[10'}",
            "\x1b[10'~"
        ];

        foreach (string sequence in sequences)
        {
            await using var terminal = NewTerminal();
            await terminal.WriteAsync(FullLine + "\x1b[2J" + sequence);
            TerminalSnapshot snapshot = await terminal.GetSnapshotAsync();
            Assert.InRange(snapshot.ActiveBuffer.CursorX, 0, snapshot.Columns);
        }
    }

    private static async Task<TerminalSnapshot> WriteAndSnapshotAsync(string sequence)
    {
        await using var terminal = NewTerminal();
        await terminal.WriteAsync(FullLine + sequence);
        return await terminal.GetSnapshotAsync();
    }

    private static Terminal NewTerminal() => new(new TerminalOptions { Columns = 10, Rows = 5 });

    private static string[] Lines(TerminalSnapshot snapshot) =>
        snapshot.ActiveBuffer.Lines
            .Select(line => line.TranslateToString(trimRight: true))
            .ToArray();
}

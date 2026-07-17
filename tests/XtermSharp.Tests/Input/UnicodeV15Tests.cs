using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace XtermSharp.Tests.Input;

public sealed class UnicodeV15Tests
{
    private const string Family = "👩‍👩‍👦";

    [Fact]
    public void PropertyDataMatchesPinnedSourcesForEveryUnicodeScalar()
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> info = stackalloc byte[1];
        int scalarCount = 0;

        for (int codePoint = 0; codePoint <= 0x10FFFF; codePoint++)
        {
            if (!Rune.IsValid(codePoint))
            {
                continue;
            }

            info[0] = UnicodeV15Provider.GetInfo(codePoint);
            hash.AppendData(info);
            scalarCount++;
        }

        Assert.Equal(UnicodeV15Data.ScalarCount, scalarCount);
        Assert.Equal(UnicodeV15Data.ScalarPropertySha256, Convert.ToHexString(hash.GetHashAndReset()));
    }

    [Fact]
    public void OfficialUnicode15GraphemeBreakTestsPass()
    {
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Unicode15",
            "GraphemeBreakTest.txt");
        string[] lines = File.ReadAllLines(fixturePath);
        var provider = new UnicodeV15Provider();
        int testCount = 0;

        foreach (string rawLine in lines)
        {
            string body = rawLine.Split('#', 2)[0].Trim();
            if (body.Length == 0)
            {
                continue;
            }

            string[] tokens = body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            UnicodeCharacterProperties preceding = default;
            Rune? precedingRune = null;
            for (int index = 1; index < tokens.Length; index += 2)
            {
                int codePoint = int.Parse(tokens[index], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                var rune = new Rune(codePoint);
                UnicodeCharacterProperties properties = provider.GetProperties(rune, preceding, precedingRune);
                bool expectedJoin = tokens[index - 1] == "×";
                Assert.True(
                    properties.JoinPrevious == expectedJoin,
                    $"Grapheme test {testCount + 1} before U+{codePoint:X}: expected " +
                    $"{(expectedJoin ? "join" : "break")} in {body}.");
                preceding = properties;
                precedingRune = rune;
            }

            Assert.Equal("÷", tokens[^1]);
            testCount++;
        }

        Assert.Equal(UnicodeV15Data.GraphemeBreakTestCount, testCount);
    }

    [Fact]
    public void UpstreamAddonCasesHaveExpectedWidths()
    {
        var registry = new UnicodeRegistry(UnicodeV15Provider.GraphemeVersionName);
        (string Text, int Width)[] cases =
        [
            ("🤣🤣🤣🤣🤣🤣🤣🤣🤣🤣", 20),
            ("👶🏿👶", 4),
            (Family, 2),
            ("=🏋️=🏋🏾‍♀=", 7),
            ("👩👩‍🎓👨🏿‍🎓", 6),
            ("🇳🇴/", 3),
            ("🇳/🇴", 3),
            ("á", 1),
            ("{각가}", 6),
            ("가=횅=", 6),
            ("(⚰︎)", 3),
            ("(⚰️)", 4),
            ("<É️g️a️l️i️️t️é️>", 16)
        ];

        Assert.Contains(UnicodeV15Provider.VersionName, registry.Versions);
        Assert.Contains(UnicodeV15Provider.GraphemeVersionName, registry.Versions);
        Assert.Equal(2, registry.GetStringCellWidth("👶"));
        foreach ((string text, int expectedWidth) in cases)
        {
            Assert.Equal(expectedWidth, registry.GetStringCellWidth(text));
        }

        var widthOnlyRegistry = new UnicodeRegistry(UnicodeV15Provider.VersionName);
        Assert.Equal(8, widthOnlyRegistry.GetStringCellWidth(Family));
        Assert.Equal(2, widthOnlyRegistry.GetStringCellWidth("á"));

        // The pinned upstream test accidentally uses private-use U+F3CB for its second lifter.
        // Full UAX #29 correctly breaks before the female sign because that base is not ExtPict.
        Assert.Equal(8, registry.GetStringCellWidth("=🏋️=\uF3CB🏾‍♀="));
    }

    [Fact]
    public async Task GraphemeClustersRemainSingleCellsAcrossStringAndUtf8Writes()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using var stringTerminal = CreateTerminal(20, 2);
        foreach (char codeUnit in Family)
        {
            await stringTerminal.WriteAsync(codeUnit.ToString(), cancellationToken);
        }
        AssertFamilyCell(stringTerminal.GetCurrentSnapshot());

        await using var utf8Terminal = CreateTerminal(20, 2);
        foreach (byte value in Encoding.UTF8.GetBytes(Family))
        {
            await utf8Terminal.WriteAsync(new byte[] { value }, cancellationToken);
        }
        AssertFamilyCell(utf8Terminal.GetCurrentSnapshot());
    }

    [Fact]
    public async Task EmojiPresentationCanWidenWrapAndBeErasedByCellWidth()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using var terminal = CreateTerminal(2, 2);

        await terminal.WriteAsync("x⚰", cancellationToken);
        await terminal.WriteAsync("️", cancellationToken);
        TerminalSnapshot snapshot = await terminal.GetSnapshotAsync(cancellationToken: cancellationToken);

        Assert.Equal("x", snapshot.ActiveBuffer.Lines[0].TranslateToString(trimRight: true));
        Assert.True(snapshot.ActiveBuffer.Lines[1].IsWrapped);
        Assert.Equal("⚰️", snapshot.ActiveBuffer.Lines[1].Cells[0].Text);
        Assert.Equal(2, snapshot.ActiveBuffer.Lines[1].Cells[0].Width);
        Assert.Equal(0, snapshot.ActiveBuffer.Lines[1].Cells[1].Width);
        Assert.Equal((2, 1), (snapshot.ActiveBuffer.CursorX, snapshot.ActiveBuffer.CursorY));

        await using var eraseTerminal = CreateTerminal(10, 2);
        await eraseTerminal.WriteAsync("$ 🙂", cancellationToken);
        await eraseTerminal.WriteAsync("\b\b  \b\b", cancellationToken);
        snapshot = await eraseTerminal.GetSnapshotAsync(cancellationToken: cancellationToken);
        Assert.DoesNotContain("🙂", snapshot.ActiveBuffer.Lines[0].TranslateToString(trimRight: true));
        Assert.Equal(2, snapshot.ActiveBuffer.CursorX);
        Assert.Equal(" ", snapshot.ActiveBuffer.Lines[0].Cells[2].Text);
        Assert.Equal(" ", snapshot.ActiveBuffer.Lines[0].Cells[3].Text);
        Assert.Equal(1, snapshot.ActiveBuffer.Lines[0].Cells[2].Width);
        Assert.Equal(1, snapshot.ActiveBuffer.Lines[0].Cells[3].Width);
    }

    [Fact]
    public async Task RepAndReflowPreserveCompleteGraphemeClusters()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using var terminal = CreateTerminal(6, 4);
        await terminal.WriteAsync(Family + "\x1b[2b", cancellationToken);

        Assert.Equal(Family + Family + Family, terminal.Buffer.Active.Lines[0].TranslateToString(true));
        Assert.Equal(6, terminal.Buffer.Active.CursorX);
        await terminal.WriteAsync("\x1b[4;1H", cancellationToken);

        await terminal.ResizeAsync(2, 4, cancellationToken);
        TerminalSnapshot snapshot = await terminal.GetSnapshotAsync(
            SnapshotScope.AllBuffers,
            cancellationToken);
        Assert.Equal(Family, snapshot.ActiveBuffer.Lines[0].TranslateToString(true));
        Assert.Equal(Family, snapshot.ActiveBuffer.Lines[1].TranslateToString(true));
        Assert.Equal(Family, snapshot.ActiveBuffer.Lines[2].TranslateToString(true));
        Assert.True(snapshot.ActiveBuffer.Lines[1].IsWrapped);
        Assert.True(snapshot.ActiveBuffer.Lines[2].IsWrapped);

        await terminal.ResizeAsync(6, 4, cancellationToken);
        snapshot = await terminal.GetSnapshotAsync(SnapshotScope.AllBuffers, cancellationToken);
        Assert.Equal(Family + Family + Family, snapshot.ActiveBuffer.Lines[0].TranslateToString(true));
        Assert.False(snapshot.ActiveBuffer.Lines[0].IsWrapped);
    }

    private static Terminal CreateTerminal(int columns, int rows) => new(new TerminalOptions
    {
        Columns = columns,
        Rows = rows,
        UnicodeVersion = UnicodeV15Provider.GraphemeVersionName
    });

    private static void AssertFamilyCell(TerminalSnapshot snapshot)
    {
        Assert.Equal(Family, snapshot.ActiveBuffer.Lines[0].Cells[0].Text);
        Assert.Equal(2, snapshot.ActiveBuffer.Lines[0].Cells[0].Width);
        Assert.Equal(0, snapshot.ActiveBuffer.Lines[0].Cells[1].Width);
        Assert.Equal(2, snapshot.ActiveBuffer.CursorX);
    }
}

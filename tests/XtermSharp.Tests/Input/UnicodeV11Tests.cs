using System.Security.Cryptography;
using System.Text;

namespace XtermSharp.Tests.Input;

public sealed class UnicodeV11Tests
{
    [Fact]
    public void WidthMatchesPinnedUpstreamForEveryUnicodeScalar()
    {
        var provider = new UnicodeV11Provider();
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> width = stackalloc byte[1];
        int scalarCount = 0;

        for (int codePoint = 0; codePoint <= 0x10FFFF; codePoint++)
        {
            if (!Rune.IsValid(codePoint))
            {
                continue;
            }

            width[0] = checked((byte)provider.GetWidth(codePoint));
            hash.AppendData(width);
            scalarCount++;
        }

        Assert.Equal(UnicodeV11Data.ScalarCount, scalarCount);
        Assert.Equal(UnicodeV11Data.ScalarWidthSha256, Convert.ToHexString(hash.GetHashAndReset()));
    }

    [Fact]
    public void WidthPreservesUnicode11RangeBoundariesAndGaps()
    {
        var provider = new UnicodeV11Provider();
        (int CodePoint, int Width)[] cases =
        [
            (0x001F, 0),
            (0x0020, 1),
            (0x009F, 0),
            (0x00A0, 1),
            (0x0300, 0),
            (0x036F, 0),
            (0x0370, 1),
            (0x23E9, 2),
            (0x1F320, 2),
            (0x1F321, 1),
            (0x1F32D, 2),
            (0x1F971, 2),
            (0x1F972, 1),
            (0x1F973, 2),
            (0x1FA73, 2),
            (0x1FA74, 1),
            (0x1FA78, 2),
            (0xE0100, 0),
            (0xE01EF, 0),
            (0xE01F0, 1)
        ];

        foreach ((int codePoint, int expectedWidth) in cases)
        {
            Assert.Equal(expectedWidth, provider.GetWidth(codePoint));
        }
    }

    [Fact]
    public void ProviderRegistersVersionAndTreatsUnicode11EmojiAsDoubleWidth()
    {
        var registry = new UnicodeRegistry(UnicodeV11Provider.VersionName);

        Assert.Contains(UnicodeV11Provider.VersionName, registry.Versions);
        Assert.Equal(UnicodeV11Provider.VersionName, registry.ActiveVersion);
        Assert.Equal(20, registry.GetStringCellWidth("🤣🤣🤣🤣🤣🤣🤣🤣🤣🤣"));
    }
}

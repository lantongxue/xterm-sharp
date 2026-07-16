using System.Globalization;

namespace XtermSharp.Internal;

internal readonly record struct RgbColor(byte Red, byte Green, byte Blue);

internal static class XParseColor
{
    private static readonly int[] ValidComponentWidths = [1, 2, 3, 4];

    public static RgbColor? ParseColor(string? data)
    {
        if (string.IsNullOrEmpty(data))
        {
            return null;
        }

        if (data.StartsWith("rgb:", StringComparison.OrdinalIgnoreCase))
        {
            string[] components = data[4..].Split('/');
            if (components.Length != 3 ||
                components[0].Length != components[1].Length ||
                components[0].Length != components[2].Length ||
                !ValidComponentWidths.Contains(components[0].Length))
            {
                return null;
            }

            int width = components[0].Length;
            int maximum = width switch
            {
                1 => 0xF,
                2 => 0xFF,
                3 => 0xFFF,
                _ => 0xFFFF
            };
            if (!TryParseHex(components[0], out int red) ||
                !TryParseHex(components[1], out int green) ||
                !TryParseHex(components[2], out int blue))
            {
                return null;
            }

            return new RgbColor(Scale(red, maximum), Scale(green, maximum), Scale(blue, maximum));
        }

        if (data[0] != '#')
        {
            return null;
        }

        string hashValue = data[1..];
        if (hashValue.Length is not (3 or 6 or 9 or 12))
        {
            return null;
        }

        int advance = hashValue.Length / 3;
        if (!TryParseHex(hashValue.AsSpan(0, advance), out int hashRed) ||
            !TryParseHex(hashValue.AsSpan(advance, advance), out int hashGreen) ||
            !TryParseHex(hashValue.AsSpan(advance * 2, advance), out int hashBlue))
        {
            return null;
        }

        return new RgbColor(
            Reduce(hashRed, advance),
            Reduce(hashGreen, advance),
            Reduce(hashBlue, advance));
    }

    public static string ToRgbString(RgbColor color, int bits = 16) =>
        $"rgb:{Pad(color.Red, bits)}/{Pad(color.Green, bits)}/{Pad(color.Blue, bits)}";

    private static bool TryParseHex(ReadOnlySpan<char> value, out int result) =>
        int.TryParse(value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out result);

    private static byte Scale(int value, int maximum) =>
        (byte)Math.Floor((double)value / maximum * 255 + 0.5);

    private static byte Reduce(int value, int width) => width switch
    {
        1 => (byte)(value << 4),
        2 => (byte)value,
        3 => (byte)(value >> 4),
        _ => (byte)(value >> 8)
    };

    private static string Pad(byte value, int bits)
    {
        string unpadded = value.ToString("x", CultureInfo.InvariantCulture);
        string padded = unpadded.Length < 2 ? "0" + unpadded : unpadded;
        return bits switch
        {
            4 => unpadded[..1],
            8 => padded,
            12 => (padded + padded)[..3],
            _ => padded + padded
        };
    }
}

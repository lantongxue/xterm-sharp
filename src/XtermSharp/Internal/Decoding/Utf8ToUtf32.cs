using System.Text;

namespace XtermSharp.Internal;

/// <summary>
/// Streaming UTF-8 to UTF-32 decoder with xterm.js-compatible malformed-input handling.
/// </summary>
internal sealed class Utf8ToUtf32
{
    private readonly byte[] _interim = new byte[3];

    internal ReadOnlySpan<byte> Interim => _interim;

    public void Clear() => Array.Clear(_interim);

    public int Decode(ReadOnlySpan<byte> input, Span<uint> target)
    {
        if (input.Length == 0)
        {
            return 0;
        }

        int size = 0;
        int startPosition = 0;

        if (_interim[0] != 0)
        {
            bool discardInterim = false;
            int codePoint = _interim[0];
            codePoint &= (codePoint & 0xE0) == 0xC0
                ? 0x1F
                : (codePoint & 0xF0) == 0xE0 ? 0x0F : 0x07;

            int position = 0;
            while (position < _interim.Length)
            {
                position++;
                byte value = position < _interim.Length ? _interim[position] : (byte)0;
                if (value == 0)
                {
                    break;
                }
                codePoint = (codePoint << 6) | (value & 0x3F);
            }

            int type = (_interim[0] & 0xE0) == 0xC0
                ? 2
                : (_interim[0] & 0xF0) == 0xE0 ? 3 : 4;
            int missing = type - position;
            while (startPosition < missing)
            {
                if (startPosition >= input.Length)
                {
                    return 0;
                }

                byte value = input[startPosition++];
                if ((value & 0xC0) != 0x80)
                {
                    startPosition--;
                    discardInterim = true;
                    break;
                }

                if (position < _interim.Length)
                {
                    _interim[position] = value;
                }
                position++;
                codePoint = (codePoint << 6) | (value & 0x3F);
            }

            if (!discardInterim)
            {
                if (type == 2)
                {
                    if (codePoint < 0x80)
                    {
                        startPosition--;
                    }
                    else
                    {
                        target[size++] = (uint)codePoint;
                    }
                }
                else if (type == 3)
                {
                    if (codePoint >= 0x0800 &&
                        codePoint is not (>= 0xD800 and <= 0xDFFF) &&
                        codePoint != 0xFEFF)
                    {
                        target[size++] = (uint)codePoint;
                    }
                }
                else if (codePoint is >= 0x010000 and <= 0x10FFFF)
                {
                    target[size++] = (uint)codePoint;
                }
            }

            Array.Clear(_interim);
        }

        int index = startPosition;
        while (index < input.Length)
        {
            int byte1 = input[index++];
            if (byte1 < 0x80)
            {
                target[size++] = (uint)byte1;
            }
            else if ((byte1 & 0xE0) == 0xC0)
            {
                if (index >= input.Length)
                {
                    _interim[0] = (byte)byte1;
                    return size;
                }

                int byte2 = input[index++];
                if ((byte2 & 0xC0) != 0x80)
                {
                    index--;
                    continue;
                }

                int codePoint = ((byte1 & 0x1F) << 6) | (byte2 & 0x3F);
                if (codePoint < 0x80)
                {
                    index--;
                    continue;
                }
                target[size++] = (uint)codePoint;
            }
            else if ((byte1 & 0xF0) == 0xE0)
            {
                if (index >= input.Length)
                {
                    _interim[0] = (byte)byte1;
                    return size;
                }

                int byte2 = input[index++];
                if ((byte2 & 0xC0) != 0x80)
                {
                    index--;
                    continue;
                }
                if (index >= input.Length)
                {
                    _interim[0] = (byte)byte1;
                    _interim[1] = (byte)byte2;
                    return size;
                }

                int byte3 = input[index++];
                if ((byte3 & 0xC0) != 0x80)
                {
                    index--;
                    continue;
                }

                int codePoint = ((byte1 & 0x0F) << 12) | ((byte2 & 0x3F) << 6) | (byte3 & 0x3F);
                if (codePoint < 0x0800 ||
                    codePoint is >= 0xD800 and <= 0xDFFF ||
                    codePoint == 0xFEFF)
                {
                    continue;
                }
                target[size++] = (uint)codePoint;
            }
            else if ((byte1 & 0xF8) == 0xF0)
            {
                if (index >= input.Length)
                {
                    _interim[0] = (byte)byte1;
                    return size;
                }

                int byte2 = input[index++];
                if ((byte2 & 0xC0) != 0x80)
                {
                    index--;
                    continue;
                }
                if (index >= input.Length)
                {
                    _interim[0] = (byte)byte1;
                    _interim[1] = (byte)byte2;
                    return size;
                }

                int byte3 = input[index++];
                if ((byte3 & 0xC0) != 0x80)
                {
                    index--;
                    continue;
                }
                if (index >= input.Length)
                {
                    _interim[0] = (byte)byte1;
                    _interim[1] = (byte)byte2;
                    _interim[2] = (byte)byte3;
                    return size;
                }

                int byte4 = input[index++];
                if ((byte4 & 0xC0) != 0x80)
                {
                    index--;
                    continue;
                }

                int codePoint = ((byte1 & 0x07) << 18) |
                    ((byte2 & 0x3F) << 12) |
                    ((byte3 & 0x3F) << 6) |
                    (byte4 & 0x3F);
                if (codePoint is < 0x010000 or > 0x10FFFF)
                {
                    continue;
                }
                target[size++] = (uint)codePoint;
            }
        }

        return size;
    }
}

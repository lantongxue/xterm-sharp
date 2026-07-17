using System.Text;

namespace XtermSharp.Internal;

/// <summary>
/// Streaming UTF-16 to UTF-32 decoder. Lone surrogates retain xterm.js' UCS-2 behavior.
/// </summary>
internal sealed class StringToUtf32
{
    private uint _interim;

    public void Clear() => _interim = 0;

    public int Decode(string input, Span<uint> target)
    {
        if (input.Length == 0)
        {
            return 0;
        }

        int size = 0;
        int startPosition = 0;

        if (_interim != 0)
        {
            uint second = input[startPosition++];
            if (second is >= 0xDC00 and <= 0xDFFF)
            {
                target[size++] = (_interim - 0xD800) * 0x400 + second - 0xDC00 + 0x10000;
            }
            else
            {
                target[size++] = _interim;
                target[size++] = second;
            }

            _interim = 0;
        }

        for (int i = startPosition; i < input.Length; i++)
        {
            uint code = input[i];
            if (code is >= 0xD800 and <= 0xDBFF)
            {
                if (++i >= input.Length)
                {
                    _interim = code;
                    return size;
                }

                uint second = input[i];
                if (second is >= 0xDC00 and <= 0xDFFF)
                {
                    target[size++] = (code - 0xD800) * 0x400 + second - 0xDC00 + 0x10000;
                }
                else
                {
                    target[size++] = code;
                    target[size++] = second;
                }
                continue;
            }

            if (code == 0xFEFF)
            {
                continue;
            }

            target[size++] = code;
        }

        return size;
    }
}

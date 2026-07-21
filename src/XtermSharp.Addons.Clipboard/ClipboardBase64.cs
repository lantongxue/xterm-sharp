using System.Text;

namespace XtermSharp.Addons.Clipboard;

/// <summary>Strict UTF-8 Base64 codec used by <see cref="ClipboardAddon"/>.</summary>
public sealed class ClipboardBase64 : IClipboardBase64
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public string EncodeText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Convert.ToBase64String(StrictUtf8.GetBytes(text));
    }

    public string DecodeText(string data)
    {
        ArgumentNullException.ThrowIfNull(data);
        ValidateBase64(data);
        return StrictUtf8.GetString(Convert.FromBase64String(data));
    }

    private static void ValidateBase64(string data)
    {
        if (data.Length % 4 != 0)
        {
            throw new FormatException("Base64 data must contain complete four-character groups.");
        }

        int padding = data.EndsWith("==", StringComparison.Ordinal)
            ? 2
            : data.EndsWith('=') ? 1 : 0;
        int contentLength = data.Length - padding;
        for (int index = 0; index < contentLength; index++)
        {
            char value = data[index];
            bool valid = value is >= 'A' and <= 'Z' or
                >= 'a' and <= 'z' or
                >= '0' and <= '9' or '+' or '/';
            if (!valid)
            {
                throw new FormatException("Base64 data contains an invalid character.");
            }
        }
        for (int index = contentLength; index < data.Length; index++)
        {
            if (data[index] != '=')
            {
                throw new FormatException("Base64 padding is invalid.");
            }
        }
    }
}

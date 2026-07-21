namespace XtermSharp.Addons.Clipboard;

/// <summary>Encodes and decodes UTF-8 clipboard text using Base64.</summary>
public interface IClipboardBase64
{
    string EncodeText(string text);
    string DecodeText(string data);
}

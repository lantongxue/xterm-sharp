using System.Collections.ObjectModel;
using System.Text;

namespace XtermSharp.Internal.Services.Charsets;

internal sealed class CharsetService
{
    private readonly List<IReadOnlyDictionary<int, string>?> _charsets = [];

    public IReadOnlyDictionary<int, string>? Charset { get; private set; }

    public int GLevel { get; private set; }

    public IReadOnlyList<IReadOnlyDictionary<int, string>?> Charsets => _charsets;

    public void Reset()
    {
        Charset = null;
        _charsets.Clear();
        GLevel = 0;
    }

    public void SetGLevel(int level)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(level);
        GLevel = level;
        Charset = level < _charsets.Count ? _charsets[level] : null;
    }

    public void SetGCharset(int level, IReadOnlyDictionary<int, string>? charset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(level);
        while (_charsets.Count <= level)
        {
            _charsets.Add(null);
        }
        _charsets[level] = charset;
        if (GLevel == level)
        {
            Charset = charset;
        }
    }

    public string Translate(Rune rune)
    {
        if (rune.Value < 0x7F && Charset?.TryGetValue(rune.Value, out string? replacement) == true)
        {
            return replacement;
        }
        return rune.ToString();
    }

    public CharsetState CaptureState() => new(GLevel, _charsets.ToArray());

    public void RestoreState(CharsetState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _charsets.Clear();
        _charsets.AddRange(state.Charsets);
        SetGLevel(state.GLevel);
    }
}

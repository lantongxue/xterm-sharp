using System.Collections.ObjectModel;
using System.Text;

namespace XtermSharp.Internal;

internal sealed record CharsetState(
    int GLevel,
    IReadOnlyList<IReadOnlyDictionary<int, string>?> Charsets);

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

internal static class CharsetMaps
{
    public static IReadOnlyDictionary<int, string> DecSpecialGraphics { get; } = Create(
        ('`', "◆"), ('a', "▒"), ('b', "␉"), ('c', "␌"), ('d', "␍"), ('e', "␊"),
        ('f', "°"), ('g', "±"), ('h', "␤"), ('i', "␋"), ('j', "┘"), ('k', "┐"),
        ('l', "┌"), ('m', "└"), ('n', "┼"), ('o', "⎺"), ('p', "⎻"), ('q', "─"),
        ('r', "⎼"), ('s', "⎽"), ('t', "├"), ('u', "┤"), ('v', "┴"), ('w', "┬"),
        ('x', "│"), ('y', "≤"), ('z', "≥"), ('{', "π"), ('|', "≠"), ('}', "£"),
        ('~', "·"));

    public static IReadOnlyDictionary<int, string> British { get; } = Create(('#', "£"));

    public static IReadOnlyDictionary<int, string> Dutch { get; } = Create(
        ('#', "£"), ('@', "¾"), ('[', "ij"), ('\\', "½"), (']', "|"), ('{', "¨"),
        ('|', "f"), ('}', "¼"), ('~', "´"));

    public static IReadOnlyDictionary<int, string> Finnish { get; } = Create(
        ('[', "Ä"), ('\\', "Ö"), (']', "Å"), ('^', "Ü"), ('`', "é"), ('{', "ä"),
        ('|', "ö"), ('}', "å"), ('~', "ü"));

    public static IReadOnlyDictionary<int, string> French { get; } = Create(
        ('#', "£"), ('@', "à"), ('[', "°"), ('\\', "ç"), (']', "§"), ('{', "é"),
        ('|', "ù"), ('}', "è"), ('~', "¨"));

    public static IReadOnlyDictionary<int, string> FrenchCanadian { get; } = Create(
        ('@', "à"), ('[', "â"), ('\\', "ç"), (']', "ê"), ('^', "î"), ('`', "ô"),
        ('{', "é"), ('|', "ù"), ('}', "è"), ('~', "û"));

    public static IReadOnlyDictionary<int, string> German { get; } = Create(
        ('@', "§"), ('[', "Ä"), ('\\', "Ö"), (']', "Ü"), ('{', "ä"), ('|', "ö"),
        ('}', "ü"), ('~', "ß"));

    public static IReadOnlyDictionary<int, string> Italian { get; } = Create(
        ('#', "£"), ('@', "§"), ('[', "°"), ('\\', "ç"), (']', "é"), ('`', "ù"),
        ('{', "à"), ('|', "ò"), ('}', "è"), ('~', "ì"));

    public static IReadOnlyDictionary<int, string> NorwegianDanish { get; } = Create(
        ('@', "Ä"), ('[', "Æ"), ('\\', "Ø"), (']', "Å"), ('^', "Ü"), ('`', "ä"),
        ('{', "æ"), ('|', "ø"), ('}', "å"), ('~', "ü"));

    public static IReadOnlyDictionary<int, string> Spanish { get; } = Create(
        ('#', "£"), ('@', "§"), ('[', "¡"), ('\\', "Ñ"), (']', "¿"), ('{', "°"),
        ('|', "ñ"), ('}', "ç"));

    public static IReadOnlyDictionary<int, string> Swedish { get; } = Create(
        ('@', "É"), ('[', "Ä"), ('\\', "Ö"), (']', "Å"), ('^', "Ü"), ('`', "é"),
        ('{', "ä"), ('|', "ö"), ('}', "å"), ('~', "ü"));

    public static IReadOnlyDictionary<int, string> Swiss { get; } = Create(
        ('#', "ù"), ('@', "à"), ('[', "é"), ('\\', "ç"), (']', "ê"), ('^', "î"),
        ('_', "è"), ('`', "ô"), ('{', "ä"), ('|', "ö"), ('}', "ü"), ('~', "û"));

    public static IReadOnlyDictionary<int, string>? Resolve(char designator) => designator switch
    {
        '0' => DecSpecialGraphics,
        'A' => British,
        'B' => null,
        '4' => Dutch,
        'C' or '5' => Finnish,
        'R' => French,
        'Q' => FrenchCanadian,
        'K' => German,
        'Y' => Italian,
        'E' or '6' => NorwegianDanish,
        'Z' => Spanish,
        'H' or '7' => Swedish,
        '=' => Swiss,
        _ => null
    };

    private static IReadOnlyDictionary<int, string> Create(params (char Source, string Target)[] entries)
    {
        var result = new Dictionary<int, string>(entries.Length);
        foreach ((char source, string target) in entries)
        {
            result.Add(source, target);
        }
        return new ReadOnlyDictionary<int, string>(result);
    }
}

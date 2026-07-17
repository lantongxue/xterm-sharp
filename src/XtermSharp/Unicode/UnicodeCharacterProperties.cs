namespace XtermSharp.Unicode;

public readonly record struct UnicodeCharacterProperties(int Width, bool JoinPrevious, int State = 0)
{
    private const int StateMask = 0xFFFFFF;

    internal int Encode() =>
        ((State & StateMask) << 3) |
        ((Width & 3) << 1) |
        (JoinPrevious ? 1 : 0);

    internal static UnicodeCharacterProperties Decode(int value) => new(
        (value >> 1) & 3,
        (value & 1) != 0,
        value >> 3);
}

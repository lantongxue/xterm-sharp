namespace XtermSharp.Addons.Clipboard;

/// <summary>Configures OSC 52 clipboard permissions and payload limits.</summary>
public sealed record ClipboardAddonOptions
{
    public const int DefaultMaxPayloadBytes = 1_048_576;

    /// <summary>Allows terminal applications to query clipboard text. Disabled by default.</summary>
    public bool AllowRead { get; init; }

    /// <summary>Allows terminal applications to write clipboard text. Disabled by default.</summary>
    public bool AllowWrite { get; init; }

    /// <summary>Maximum decoded UTF-8 clipboard payload size.</summary>
    public int MaxPayloadBytes { get; init; } = DefaultMaxPayloadBytes;

    internal ClipboardAddonOptions Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxPayloadBytes);
        return this with { };
    }
}

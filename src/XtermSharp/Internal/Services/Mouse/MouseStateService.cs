namespace XtermSharp.Internal;

internal sealed class MouseStateService
{
    private readonly Dictionary<string, MouseProtocol> _protocols = new(StringComparer.Ordinal)
    {
        ["NONE"] = new(TerminalMouseEventTypes.None, value => (false, value)),
        ["X10"] = new(TerminalMouseEventTypes.Down, value =>
        {
            if (value.Button == TerminalMouseButton.Wheel || value.Action != TerminalMouseAction.Down)
            {
                return (false, value);
            }
            return (true, value with { Modifiers = TerminalModifiers.None });
        }),
        ["VT200"] = new(
            TerminalMouseEventTypes.Down | TerminalMouseEventTypes.Up | TerminalMouseEventTypes.Wheel,
            value => (value.Action != TerminalMouseAction.Move, value)),
        ["DRAG"] = new(
            TerminalMouseEventTypes.Down | TerminalMouseEventTypes.Up | TerminalMouseEventTypes.Wheel | TerminalMouseEventTypes.Drag,
            value => (value.Action != TerminalMouseAction.Move || value.Button != TerminalMouseButton.None, value)),
        ["ANY"] = new(
            TerminalMouseEventTypes.Down | TerminalMouseEventTypes.Up | TerminalMouseEventTypes.Wheel |
            TerminalMouseEventTypes.Drag | TerminalMouseEventTypes.Move,
            value => (true, value))
    };

    private readonly Dictionary<string, Func<TerminalMouseEvent, string>> _encodings = new(StringComparer.Ordinal)
    {
        ["DEFAULT"] = EncodeDefault,
        ["SGR"] = value => EncodeSgr(value, pixels: false),
        ["SGR_PIXELS"] = value => EncodeSgr(value, pixels: true)
    };

    private string _activeProtocol = "NONE";
    private string _activeEncoding = "DEFAULT";

    public event Action<TerminalMouseEventTypes>? ProtocolChanged;

    public string ActiveProtocol
    {
        get => _activeProtocol;
        set
        {
            if (!_protocols.TryGetValue(value, out MouseProtocol? protocol))
            {
                throw new ArgumentException($"Unknown mouse protocol '{value}'.", nameof(value));
            }
            _activeProtocol = value;
            ProtocolChanged?.Invoke(protocol.Events);
        }
    }

    public string ActiveEncoding
    {
        get => _activeEncoding;
        set
        {
            if (!_encodings.ContainsKey(value))
            {
                throw new ArgumentException($"Unknown mouse encoding '{value}'.", nameof(value));
            }
            _activeEncoding = value;
        }
    }

    public bool AreMouseEventsActive => _protocols[_activeProtocol].Events != TerminalMouseEventTypes.None;
    public bool IsDefaultEncoding => _activeEncoding == "DEFAULT";
    public bool IsPixelEncoding => _activeEncoding == "SGR_PIXELS";
    public IReadOnlyCollection<string> ProtocolNames => _protocols.Keys;
    public IReadOnlyCollection<string> EncodingNames => _encodings.Keys;

    public void Reset()
    {
        ActiveProtocol = "NONE";
        ActiveEncoding = "DEFAULT";
    }

    public void AddProtocol(
        string name,
        TerminalMouseEventTypes events,
        Func<TerminalMouseEvent, (bool Allowed, TerminalMouseEvent Event)> restrict)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(restrict);
        _protocols[name] = new MouseProtocol(events, restrict);
    }

    public void AddEncoding(string name, Func<TerminalMouseEvent, string> encode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(encode);
        _encodings[name] = encode;
    }

    public bool TryEncode(TerminalMouseEvent value, out string sequence)
    {
        Validate(value);
        (bool allowed, TerminalMouseEvent normalized) = _protocols[_activeProtocol].Restrict(value);
        if (!allowed)
        {
            sequence = string.Empty;
            return false;
        }
        sequence = _encodings[_activeEncoding](normalized);
        return sequence.Length != 0;
    }

    private static string EncodeDefault(TerminalMouseEvent value)
    {
        int code = GetEventCode(value, sgr: false) + 32;
        int column = value.Column + 32;
        int row = value.Row + 32;
        if (code > byte.MaxValue || column > byte.MaxValue || row > byte.MaxValue)
        {
            return string.Empty;
        }
        return string.Concat("\x1b[M", (char)code, (char)column, (char)row);
    }

    private static string EncodeSgr(TerminalMouseEvent value, bool pixels)
    {
        char final = value.Action == TerminalMouseAction.Up && value.Button != TerminalMouseButton.Wheel ? 'm' : 'M';
        int first = pixels ? value.PixelX : value.Column;
        int second = pixels ? value.PixelY : value.Row;
        return $"\x1b[<{GetEventCode(value, sgr: true)};{first};{second}{final}";
    }

    private static int GetEventCode(TerminalMouseEvent value, bool sgr)
    {
        int code = 0;
        if (value.Modifiers.HasFlag(TerminalModifiers.Control)) code |= 16;
        if (value.Modifiers.HasFlag(TerminalModifiers.Shift)) code |= 4;
        if (value.Modifiers.HasFlag(TerminalModifiers.Alt)) code |= 8;

        if (value.Button == TerminalMouseButton.Wheel)
        {
            return code | 64 | (int)value.Action;
        }

        code |= (int)value.Button & 3;
        if (((int)value.Button & 4) != 0) code |= 64;
        if (((int)value.Button & 8) != 0) code |= 128;
        if (value.Action == TerminalMouseAction.Move)
        {
            code |= (int)TerminalMouseAction.Move;
        }
        else if (value.Action == TerminalMouseAction.Up && !sgr)
        {
            code |= (int)TerminalMouseButton.None;
        }
        return code;
    }

    private static void Validate(TerminalMouseEvent value)
    {
        if (value.Column <= 0) throw new ArgumentOutOfRangeException(nameof(value), "Column must be positive.");
        if (value.Row <= 0) throw new ArgumentOutOfRangeException(nameof(value), "Row must be positive.");
        if (value.PixelX < 0 || value.PixelY < 0) throw new ArgumentOutOfRangeException(nameof(value), "Pixel coordinates cannot be negative.");
    }
}

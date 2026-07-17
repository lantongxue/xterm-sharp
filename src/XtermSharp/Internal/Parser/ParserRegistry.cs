using XtermSharp.Internal.Parser;

namespace XtermSharp.Internal;

internal sealed class ParserRegistry(EscapeSequenceParser parser) : ITerminalParser
{
    public IDisposable RegisterCsiHandler(
        FunctionIdentifier identifier,
        Func<CsiParameters, ValueTask<bool>> handler)
    {
        Validate(identifier, csiOrDcs: true);
        ArgumentNullException.ThrowIfNull(handler);
        return parser.RegisterCsiHandler(identifier, handler);
    }

    public IDisposable RegisterEscHandler(FunctionIdentifier identifier, Func<ValueTask<bool>> handler)
    {
        Validate(identifier, csiOrDcs: false);
        if (identifier.Prefix is not null)
        {
            throw new ArgumentException("ESC handlers cannot use a prefix.", nameof(identifier));
        }
        ArgumentNullException.ThrowIfNull(handler);
        return parser.RegisterEscHandler(identifier, handler);
    }

    public IDisposable RegisterOscHandler(int identifier, Func<string, ValueTask<bool>> handler)
    {
        if (identifier < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(identifier));
        }
        ArgumentNullException.ThrowIfNull(handler);
        return parser.RegisterOscHandler(identifier, handler);
    }

    public IDisposable RegisterDcsHandler(
        FunctionIdentifier identifier,
        Func<string, CsiParameters, ValueTask<bool>> handler)
    {
        Validate(identifier, csiOrDcs: true);
        ArgumentNullException.ThrowIfNull(handler);
        return parser.RegisterDcsHandler(identifier, handler);
    }

    public IDisposable RegisterApcHandler(FunctionIdentifier identifier, Func<string, ValueTask<bool>> handler)
    {
        Validate(identifier, csiOrDcs: false);
        if (identifier.Prefix is not null)
        {
            throw new ArgumentException("APC handlers cannot use a prefix.", nameof(identifier));
        }
        ArgumentNullException.ThrowIfNull(handler);
        return parser.RegisterApcHandler(identifier, handler);
    }

    private static void Validate(FunctionIdentifier identifier, bool csiOrDcs)
    {
        char minimumFinal = csiOrDcs ? '\x40' : '\x30';
        if (identifier.Final < minimumFinal || identifier.Final > '\x7E')
        {
            throw new ArgumentException("The final byte is outside the permitted range.", nameof(identifier));
        }
        if (identifier.Prefix is char prefix && prefix is < '\x3C' or > '\x3F')
        {
            throw new ArgumentException("The prefix byte must be in the range 0x3C-0x3F.", nameof(identifier));
        }
        if (identifier.Intermediates.Length > 2 || identifier.Intermediates.Any(static value => value is < '\x20' or > '\x2F'))
        {
            throw new ArgumentException("Intermediate bytes must be at most two bytes in the range 0x20-0x2F.", nameof(identifier));
        }
    }
}

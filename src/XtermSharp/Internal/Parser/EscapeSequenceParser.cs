using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;

namespace XtermSharp.Internal.Parser;

internal sealed class EscapeSequenceParser : IDisposable
{
    private const int NonAsciiPrintable = 0xA0;
    private const int ActionShift = 8;
    private const int StateMask = 0xFF;

    internal static EscapeTransitionTable Vt500TransitionTable { get; } = CreateVt500TransitionTable();

    private readonly object _handlerGate = new();
    private readonly object _parseGate = new();
    private readonly EscapeTransitionTable _transitions;
    private readonly int _payloadLimit;
    private readonly ParserParameters _parameters = new();
    private readonly Dictionary<int, List<Func<CsiParameters, ValueTask<bool>>>> _csiHandlers = [];
    private readonly Dictionary<int, List<Func<ValueTask<bool>>>> _escHandlers = [];
    private readonly Dictionary<int, Func<int, bool>> _executeHandlers = [];
    private readonly OscParser _oscParser = new();
    private readonly DcsParser _dcsParser = new();
    private readonly ApcParser _apcParser = new();
    private readonly List<ParserPauseRecord> _pauseRecords = [];
    private ParserPrintHandler _printHandler = static _ => { };
    private Action<int> _executeFallback = static _ => { };
    private Action<int, CsiParameters> _csiFallback = static (_, _) => { };
    private Action<int> _escFallback = static _ => { };
    private Func<EscapeParserErrorState, EscapeParserErrorState> _errorHandler = static state => state;
    private long _operationSequence;
    private long _activeOperation;
    private int _resetGeneration;
    private bool _continuationFaulted;
    private int _collect;
    private bool _disposed;

    internal EscapeSequenceParser(
        EscapeTransitionTable? transitions = null,
        int payloadLimit = StringPayloadHandler.DefaultPayloadLimit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(payloadLimit);
        _transitions = transitions ?? Vt500TransitionTable;
        _payloadLimit = payloadLimit;
        InitialState = EscapeParserState.Ground;
        CurrentState = InitialState;
        _parameters.ResetZeroDefault();
        RegisterEscHandler(new FunctionIdentifier('\\'), static () => true);
    }

    internal EscapeParserState InitialState { get; }
    internal EscapeParserState CurrentState { get; set; }
    internal int PrecedingJoinState { get; set; }
    internal EscapeTransitionTable Transitions => _transitions;
    internal ParserParameters Parameters => _parameters;
    internal int CollectValue => _collect;
    internal string Collected => IdentifierToString(_collect);
    internal bool IsParsing
    {
        get
        {
            lock (_parseGate)
            {
                return _activeOperation != 0;
            }
        }
    }
    internal IReadOnlyList<ParserPauseRecord> PauseRecords => _pauseRecords;

    internal void SetCollected(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _collect = 0;
        foreach (char character in value)
        {
            _collect = (_collect << 8) | character;
        }
    }

    internal static int Identifier(FunctionIdentifier identifier, int minimumFinal = 0x40)
    {
        if (identifier.Prefix is char prefix && prefix is < '\x3C' or > '\x3F')
        {
            throw new ArgumentException("Prefix must be in the range 0x3C-0x3F.", nameof(identifier));
        }
        if (identifier.Intermediates.Length > 2)
        {
            throw new ArgumentException("At most two intermediate bytes are supported.", nameof(identifier));
        }

        int result = identifier.Prefix ?? 0;
        foreach (char intermediate in identifier.Intermediates)
        {
            if (intermediate is < '\x20' or > '\x2F')
            {
                throw new ArgumentException("Intermediate bytes must be in the range 0x20-0x2F.", nameof(identifier));
            }
            result = (result << 8) | intermediate;
        }
        if (identifier.Final < minimumFinal || identifier.Final > '\x7E')
        {
            throw new ArgumentException($"Final byte must be in the range {minimumFinal}-126.", nameof(identifier));
        }
        return (result << 8) | identifier.Final;
    }

    internal static string IdentifierToString(int identifier)
    {
        Span<char> characters = stackalloc char[4];
        int position = characters.Length;
        while (identifier != 0)
        {
            characters[--position] = (char)(identifier & 0xFF);
            identifier >>= 8;
        }
        return new string(characters[position..]);
    }

    internal void SetPrintHandler(ParserPrintHandler handler) =>
        Volatile.Write(ref _printHandler, handler ?? throw new ArgumentNullException(nameof(handler)));

    internal void ClearPrintHandler() => Volatile.Write(ref _printHandler, static _ => { });

    internal IDisposable RegisterEscHandler(FunctionIdentifier identifier, Func<bool> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return RegisterEscHandler(identifier, () => new ValueTask<bool>(handler()));
    }

    internal IDisposable RegisterEscHandler(FunctionIdentifier identifier, Func<ValueTask<bool>> handler) =>
        Register(_escHandlers, Identifier(identifier, 0x30), handler);

    internal void ClearEscHandler(FunctionIdentifier identifier) =>
        Clear(_escHandlers, Identifier(identifier, 0x30));

    internal void SetEscHandlerFallback(Action<int> handler) =>
        Volatile.Write(ref _escFallback, handler ?? throw new ArgumentNullException(nameof(handler)));

    internal IDisposable RegisterCsiHandler(FunctionIdentifier identifier, Func<CsiParameters, bool> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return RegisterCsiHandler(identifier, parameters => new ValueTask<bool>(handler(parameters)));
    }

    internal IDisposable RegisterCsiHandler(FunctionIdentifier identifier, Func<CsiParameters, ValueTask<bool>> handler) =>
        Register(_csiHandlers, Identifier(identifier), handler);

    internal void ClearCsiHandler(FunctionIdentifier identifier) =>
        Clear(_csiHandlers, Identifier(identifier));

    internal void SetCsiHandlerFallback(Action<int, CsiParameters> handler) =>
        Volatile.Write(ref _csiFallback, handler ?? throw new ArgumentNullException(nameof(handler)));

    internal void SetExecuteHandler(char flag, Func<bool> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        SetExecuteHandler(flag, _ => handler());
    }

    internal void SetExecuteHandler(char flag, Func<int, bool> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_handlerGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _executeHandlers[flag] = handler;
        }
    }

    internal void ClearExecuteHandler(char flag)
    {
        lock (_handlerGate)
        {
            _executeHandlers.Remove(flag);
        }
    }

    internal void SetExecuteHandlerFallback(Action<int> handler) =>
        Volatile.Write(ref _executeFallback, handler ?? throw new ArgumentNullException(nameof(handler)));

    internal IDisposable RegisterOscHandler(int identifier, IOscParserHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_handlerGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _oscParser.RegisterHandler(identifier, handler);
        }
    }

    internal IDisposable RegisterOscHandler(int identifier, Func<string, bool> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return RegisterOscHandler(
            identifier,
            new OscStringHandler(value => new ValueTask<bool>(handler(value)), _payloadLimit));
    }

    internal IDisposable RegisterOscHandler(int identifier, Func<string, ValueTask<bool>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return RegisterOscHandler(identifier, new OscStringHandler(handler, _payloadLimit));
    }

    internal void ClearOscHandler(int identifier) => _oscParser.ClearHandler(identifier);
    internal void SetOscHandlerFallback(Action<int, StringParserAction, object?> handler) =>
        _oscParser.SetHandlerFallback(handler);

    internal IDisposable RegisterDcsHandler(FunctionIdentifier identifier, IDcsParserHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        int normalized = Identifier(identifier);
        lock (_handlerGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _dcsParser.RegisterHandler(normalized, handler);
        }
    }

    internal IDisposable RegisterDcsHandler(FunctionIdentifier identifier, Func<string, CsiParameters, bool> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return RegisterDcsHandler(
            identifier,
            new DcsStringHandler(
                (value, parameters) => new ValueTask<bool>(handler(value, parameters)),
                _payloadLimit));
    }

    internal IDisposable RegisterDcsHandler(
        FunctionIdentifier identifier,
        Func<string, CsiParameters, ValueTask<bool>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return RegisterDcsHandler(identifier, new DcsStringHandler(handler, _payloadLimit));
    }

    internal void ClearDcsHandler(FunctionIdentifier identifier) => _dcsParser.ClearHandler(Identifier(identifier));
    internal void SetDcsHandlerFallback(Action<int, StringParserAction, object?> handler) =>
        _dcsParser.SetHandlerFallback(handler);

    internal IDisposable RegisterApcHandler(FunctionIdentifier identifier, IApcParserHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        FunctionIdentifier normalized = identifier with { Prefix = null };
        int value = Identifier(normalized, 0x30);
        lock (_handlerGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _apcParser.RegisterHandler(value, handler);
        }
    }

    internal IDisposable RegisterApcHandler(FunctionIdentifier identifier, Func<string, bool> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return RegisterApcHandler(
            identifier,
            new ApcStringHandler(value => new ValueTask<bool>(handler(value)), _payloadLimit));
    }

    internal IDisposable RegisterApcHandler(FunctionIdentifier identifier, Func<string, ValueTask<bool>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return RegisterApcHandler(identifier, new ApcStringHandler(handler, _payloadLimit));
    }

    internal void ClearApcHandler(FunctionIdentifier identifier) =>
        _apcParser.ClearHandler(Identifier(identifier with { Prefix = null }, 0x30));

    internal void SetApcHandlerFallback(Action<int, StringParserAction, object?> handler) =>
        _apcParser.SetHandlerFallback(handler);

    internal void SetErrorHandler(Func<EscapeParserErrorState, EscapeParserErrorState> handler) =>
        Volatile.Write(ref _errorHandler, handler ?? throw new ArgumentNullException(nameof(handler)));

    internal void ClearErrorHandler() => Volatile.Write(ref _errorHandler, static state => state);

    internal void Reset()
    {
        CurrentState = InitialState;
        Exception? resetException = null;
        ResetParser(_oscParser.Reset);
        ResetParser(_dcsParser.Reset);
        ResetParser(_apcParser.Reset);
        _parameters.ResetZeroDefault();
        _collect = 0;
        PrecedingJoinState = 0;
        _continuationFaulted = false;
        _pauseRecords.Clear();
        Interlocked.Increment(ref _resetGeneration);

        if (resetException is not null)
        {
            ExceptionDispatchInfo.Capture(resetException).Throw();
        }

        void ResetParser(Action reset)
        {
            try
            {
                reset();
            }
            catch (Exception exception)
            {
                resetException ??= exception;
            }
        }
    }

    internal ValueTask ParseAsync(ReadOnlyMemory<uint> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        long operation;
        lock (_parseGate)
        {
            if (_continuationFaulted)
            {
                throw new InvalidOperationException("Improper continuation due to a previous asynchronous handler.");
            }
            if (_activeOperation != 0)
            {
                _continuationFaulted = true;
                throw new InvalidOperationException("Improper continuation due to a previous asynchronous handler.");
            }
            _pauseRecords.Clear();
            operation = ++_operationSequence;
            _activeOperation = operation;
        }
        return ParseOperationAsync(data, operation);
    }

    internal void ParseSynchronously(ReadOnlySpan<uint> data)
    {
        ValueTask parse = ParseAsync(data.ToArray());
        if (!parse.IsCompletedSuccessfully)
        {
            throw new InvalidOperationException("An asynchronous parser handler cannot be used by synchronous parsing.");
        }
        parse.GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        lock (_handlerGate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _csiHandlers.Clear();
            _escHandlers.Clear();
            _executeHandlers.Clear();
            _pauseRecords.Clear();
        }
        _oscParser.Dispose();
        _dcsParser.Dispose();
        _apcParser.Dispose();
    }

    private async ValueTask ParseOperationAsync(ReadOnlyMemory<uint> data, long operation)
    {
        try
        {
            await ParseCoreAsync(data).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            try
            {
                Reset();
            }
            catch (Exception resetException)
            {
                exception.Data["XtermSharp.ParserResetException"] = resetException;
            }
            throw;
        }
        finally
        {
            lock (_parseGate)
            {
                if (_activeOperation == operation)
                {
                    _activeOperation = 0;
                }
            }
        }
    }

    private async ValueTask ParseCoreAsync(ReadOnlyMemory<uint> dataMemory)
    {
        uint[]? copy = null;
        ReadOnlyMemory<uint> stableMemory = dataMemory;
        if (!MemoryMarshal.TryGetArray(dataMemory, out ArraySegment<uint> _))
        {
            copy = dataMemory.ToArray();
            stableMemory = copy;
        }

        for (int index = 0; index < stableMemory.Length; index++)
        {
            ReadOnlySpan<uint> data = stableMemory.Span;
            uint code = data[index];
            int lookupCode = code < NonAsciiPrintable ? (int)code : NonAsciiPrintable;
            ushort transition = _transitions.Table[((int)CurrentState << 8) | lookupCode];
            EscapeParserAction action = (EscapeParserAction)(transition >> ActionShift);
            EscapeParserState nextState = (EscapeParserState)(transition & StateMask);
            int generation = _resetGeneration;

            switch (action)
            {
                case EscapeParserAction.Print:
                {
                    int end = index + 1;
                    while (end < data.Length && IsPrintable(data[end]))
                    {
                        end++;
                    }
                    Volatile.Read(ref _printHandler)(data[index..end]);
                    index = end - 1;
                    break;
                }
                case EscapeParserAction.Execute:
                    Func<int, bool>? execute;
                    lock (_handlerGate)
                    {
                        _executeHandlers.TryGetValue((int)code, out execute);
                    }
                    if (execute is not null)
                    {
                        execute((int)code);
                    }
                    else
                    {
                        Volatile.Read(ref _executeFallback)((int)code);
                    }
                    PrecedingJoinState = 0;
                    break;
                case EscapeParserAction.Ignore:
                    break;
                case EscapeParserAction.Error:
                {
                    EscapeParserErrorState state = Volatile.Read(ref _errorHandler)(new EscapeParserErrorState(
                        index,
                        code,
                        CurrentState,
                        _collect,
                        new CsiParameters(_parameters)));
                    if (state.Abort)
                    {
                        return;
                    }
                    break;
                }
                case EscapeParserAction.CsiDispatch:
                    await DispatchCsiAsync((_collect << 8) | (int)code, index).ConfigureAwait(false);
                    PrecedingJoinState = 0;
                    break;
                case EscapeParserAction.Parameter:
                    ApplyParameter(code);
                    break;
                case EscapeParserAction.Collect:
                    _collect = (_collect << 8) | (int)code;
                    break;
                case EscapeParserAction.EscDispatch:
                    await DispatchEscAsync((_collect << 8) | (int)code, index).ConfigureAwait(false);
                    PrecedingJoinState = 0;
                    break;
                case EscapeParserAction.Clear:
                    _parameters.ResetZeroDefault();
                    _collect = 0;
                    break;
                case EscapeParserAction.DcsHook:
                    _dcsParser.Hook((_collect << 8) | (int)code, new CsiParameters(_parameters));
                    break;
                case EscapeParserAction.DcsPut:
                {
                    int end = index + 1;
                    while (end < data.Length && IsDcsPayload(data[end]))
                    {
                        end++;
                    }
                    _dcsParser.Put(data[index..end]);
                    index = end - 1;
                    break;
                }
                case EscapeParserAction.DcsUnhook:
                {
                    ValueTask end = _dcsParser.UnhookAsync(code is not 0x18 and not 0x1A);
                    await AwaitPayloadAsync(end, ParserPauseKind.Dcs, index).ConfigureAwait(false);
                    if (code == 0x1B)
                    {
                        nextState = EscapeParserState.Escape;
                    }
                    _parameters.ResetZeroDefault();
                    _collect = 0;
                    PrecedingJoinState = 0;
                    break;
                }
                case EscapeParserAction.OscStart:
                    _oscParser.Start();
                    break;
                case EscapeParserAction.OscPut:
                {
                    int end = index + 1;
                    while (end < data.Length && IsOscPayload(data[end]))
                    {
                        end++;
                    }
                    _oscParser.Put(data[index..end]);
                    index = end - 1;
                    break;
                }
                case EscapeParserAction.OscEnd:
                {
                    ValueTask end = _oscParser.EndAsync(code is not 0x18 and not 0x1A);
                    await AwaitPayloadAsync(end, ParserPauseKind.Osc, index).ConfigureAwait(false);
                    if (code == 0x1B)
                    {
                        nextState = EscapeParserState.Escape;
                    }
                    _parameters.ResetZeroDefault();
                    _collect = 0;
                    PrecedingJoinState = 0;
                    break;
                }
                case EscapeParserAction.ApcStart:
                    _apcParser.Start((_collect << 8) | (int)code);
                    break;
                case EscapeParserAction.ApcPut:
                {
                    int end = index + 1;
                    while (end < data.Length && IsApcPayload(data[end]))
                    {
                        end++;
                    }
                    _apcParser.Put(data[index..end]);
                    index = end - 1;
                    break;
                }
                case EscapeParserAction.ApcEnd:
                {
                    ValueTask end = _apcParser.EndAsync(code is not 0x18 and not 0x1A);
                    await AwaitPayloadAsync(end, ParserPauseKind.Apc, index).ConfigureAwait(false);
                    if (code == 0x1B)
                    {
                        nextState = EscapeParserState.Escape;
                    }
                    _parameters.ResetZeroDefault();
                    _collect = 0;
                    PrecedingJoinState = 0;
                    break;
                }
            }

            if (generation != _resetGeneration)
            {
                continue;
            }
            CurrentState = nextState;
        }
    }

    private async ValueTask DispatchCsiAsync(int identifier, int position)
    {
        CsiParameters parameters = new(_parameters);
        Func<CsiParameters, ValueTask<bool>>[] handlers = Snapshot(_csiHandlers, identifier);
        for (int index = handlers.Length - 1; index >= 0; index--)
        {
            ValueTask<bool> result = handlers[index](parameters);
            if (!result.IsCompletedSuccessfully)
            {
                _pauseRecords.Add(new ParserPauseRecord(position, ParserPauseKind.Csi, index));
            }
            if (await result.ConfigureAwait(false))
            {
                return;
            }
        }
        Volatile.Read(ref _csiFallback)(identifier, parameters);
    }

    private async ValueTask DispatchEscAsync(int identifier, int position)
    {
        Func<ValueTask<bool>>[] handlers = Snapshot(_escHandlers, identifier);
        for (int index = handlers.Length - 1; index >= 0; index--)
        {
            ValueTask<bool> result = handlers[index]();
            if (!result.IsCompletedSuccessfully)
            {
                _pauseRecords.Add(new ParserPauseRecord(position, ParserPauseKind.Esc, index));
            }
            if (await result.ConfigureAwait(false))
            {
                return;
            }
        }
        Volatile.Read(ref _escFallback)(identifier);
    }

    private async ValueTask AwaitPayloadAsync(ValueTask task, ParserPauseKind kind, int position)
    {
        if (!task.IsCompletedSuccessfully)
        {
            _pauseRecords.Add(new ParserPauseRecord(position, kind, 0));
        }
        await task.ConfigureAwait(false);
    }

    private void ApplyParameter(uint code)
    {
        switch (code)
        {
            case (uint)';':
                _parameters.AddParameter(0);
                break;
            case (uint)':':
                _parameters.AddSubParameter(-1);
                break;
            default:
                _parameters.AddDigit((int)code - '0');
                break;
        }
    }

    private IDisposable Register<T>(Dictionary<int, List<T>> handlers, int identifier, T handler)
        where T : Delegate
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_handlerGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!handlers.TryGetValue(identifier, out List<T>? list))
            {
                list = [];
                handlers.Add(identifier, list);
            }
            list.Add(handler);
        }
        return new DelegateDisposable(() =>
        {
            lock (_handlerGate)
            {
                if (!handlers.TryGetValue(identifier, out List<T>? list))
                {
                    return;
                }
                list.Remove(handler);
                if (list.Count == 0)
                {
                    handlers.Remove(identifier);
                }
            }
        });
    }

    private void Clear<T>(Dictionary<int, List<T>> handlers, int identifier)
        where T : Delegate
    {
        lock (_handlerGate)
        {
            handlers.Remove(identifier);
        }
    }

    private T[] Snapshot<T>(Dictionary<int, List<T>> handlers, int identifier)
        where T : Delegate
    {
        lock (_handlerGate)
        {
            return handlers.TryGetValue(identifier, out List<T>? list)
                ? list.ToArray()
                : [];
        }
    }

    private static bool IsPrintable(uint code) =>
        code >= 0x20 && (code <= 0x7E || code >= NonAsciiPrintable);

    private static bool IsDcsPayload(uint code) =>
        code is not 0x18 and not 0x1A and not 0x1B && (code <= 0x7F || code >= NonAsciiPrintable);

    private static bool IsOscPayload(uint code) =>
        code >= 0x20 && (code <= 0x7F || code >= NonAsciiPrintable);

    private static bool IsApcPayload(uint code) =>
        code is >= 0x20 and < 0x7F || code is >= 0x08 and < 0x0E || code >= NonAsciiPrintable;

    private static EscapeTransitionTable CreateVt500TransitionTable()
    {
        var table = new EscapeTransitionTable(4257);
        int[] printables = Range(0x20, 0x7F);
        int[] executables = [.. Range(0x00, 0x18), 0x19, .. Range(0x1C, 0x20)];
        EscapeParserState[] states = Enum.GetValues<EscapeParserState>()
            .Where(state => state != EscapeParserState.StateLength)
            .ToArray();

        table.SetDefault(EscapeParserAction.Error, EscapeParserState.Ground);
        table.AddMany(printables, EscapeParserState.Ground, EscapeParserAction.Print, EscapeParserState.Ground);
        foreach (EscapeParserState state in states)
        {
            table.AddMany([0x18, 0x1A, 0x99, 0x9A], state, EscapeParserAction.Execute, EscapeParserState.Ground);
            table.AddMany(Range(0x80, 0x90), state, EscapeParserAction.Execute, EscapeParserState.Ground);
            table.AddMany(Range(0x90, 0x98), state, EscapeParserAction.Execute, EscapeParserState.Ground);
            table.Add(0x9C, state, EscapeParserAction.Ignore, EscapeParserState.Ground);
            table.Add(0x1B, state, EscapeParserAction.Clear, EscapeParserState.Escape);
            table.Add(0x9D, state, EscapeParserAction.OscStart, EscapeParserState.OscString);
            table.AddMany([0x98, 0x9E], state, EscapeParserAction.Ignore, EscapeParserState.SosPmString);
            table.Add(0x9F, state, EscapeParserAction.Clear, EscapeParserState.ApcEntry);
            table.Add(0x9B, state, EscapeParserAction.Clear, EscapeParserState.CsiEntry);
            table.Add(0x90, state, EscapeParserAction.Clear, EscapeParserState.DcsEntry);
        }

        table.AddMany(executables, EscapeParserState.Ground, EscapeParserAction.Execute, EscapeParserState.Ground);
        table.AddMany(executables, EscapeParserState.Escape, EscapeParserAction.Execute, EscapeParserState.Escape);
        table.Add(0x7F, EscapeParserState.Escape, EscapeParserAction.Ignore, EscapeParserState.Escape);
        table.AddMany(executables, EscapeParserState.OscString, EscapeParserAction.Ignore, EscapeParserState.OscString);
        table.AddMany(executables, EscapeParserState.CsiEntry, EscapeParserAction.Execute, EscapeParserState.CsiEntry);
        table.Add(0x7F, EscapeParserState.CsiEntry, EscapeParserAction.Ignore, EscapeParserState.CsiEntry);
        table.AddMany(executables, EscapeParserState.CsiParameter, EscapeParserAction.Execute, EscapeParserState.CsiParameter);
        table.Add(0x7F, EscapeParserState.CsiParameter, EscapeParserAction.Ignore, EscapeParserState.CsiParameter);
        table.AddMany(executables, EscapeParserState.CsiIgnore, EscapeParserAction.Execute, EscapeParserState.CsiIgnore);
        table.AddMany(executables, EscapeParserState.CsiIntermediate, EscapeParserAction.Execute, EscapeParserState.CsiIntermediate);
        table.Add(0x7F, EscapeParserState.CsiIntermediate, EscapeParserAction.Ignore, EscapeParserState.CsiIntermediate);
        table.AddMany(executables, EscapeParserState.EscapeIntermediate, EscapeParserAction.Execute, EscapeParserState.EscapeIntermediate);
        table.Add(0x7F, EscapeParserState.EscapeIntermediate, EscapeParserAction.Ignore, EscapeParserState.EscapeIntermediate);

        table.Add(0x5D, EscapeParserState.Escape, EscapeParserAction.OscStart, EscapeParserState.OscString);
        table.AddMany(printables, EscapeParserState.OscString, EscapeParserAction.OscPut, EscapeParserState.OscString);
        table.Add(0x7F, EscapeParserState.OscString, EscapeParserAction.OscPut, EscapeParserState.OscString);
        table.AddMany([0x9C, 0x1B, 0x18, 0x1A, 0x07], EscapeParserState.OscString, EscapeParserAction.OscEnd, EscapeParserState.Ground);
        table.AddMany(Range(0x1C, 0x20), EscapeParserState.OscString, EscapeParserAction.Ignore, EscapeParserState.OscString);

        table.AddMany([0x58, 0x5E], EscapeParserState.Escape, EscapeParserAction.Ignore, EscapeParserState.SosPmString);
        table.AddMany(printables, EscapeParserState.SosPmString, EscapeParserAction.Ignore, EscapeParserState.SosPmString);
        table.AddMany(executables, EscapeParserState.SosPmString, EscapeParserAction.Ignore, EscapeParserState.SosPmString);
        table.Add(0x9C, EscapeParserState.SosPmString, EscapeParserAction.Ignore, EscapeParserState.Ground);
        table.Add(0x7F, EscapeParserState.SosPmString, EscapeParserAction.Ignore, EscapeParserState.SosPmString);

        table.Add(0x5F, EscapeParserState.Escape, EscapeParserAction.Clear, EscapeParserState.ApcEntry);
        table.AddMany(executables, EscapeParserState.ApcEntry, EscapeParserAction.Ignore, EscapeParserState.ApcEntry);
        table.Add(0x7F, EscapeParserState.ApcEntry, EscapeParserAction.Ignore, EscapeParserState.ApcEntry);
        table.AddMany(Range(0x20, 0x30), EscapeParserState.ApcEntry, EscapeParserAction.Collect, EscapeParserState.ApcIntermediate);
        table.AddMany(Range(0x30, 0x7F), EscapeParserState.ApcEntry, EscapeParserAction.ApcStart, EscapeParserState.ApcPassthrough);
        table.AddMany(Range(0x30, 0x7F), EscapeParserState.ApcIntermediate, EscapeParserAction.ApcStart, EscapeParserState.ApcPassthrough);
        table.AddMany(executables, EscapeParserState.ApcIntermediate, EscapeParserAction.Ignore, EscapeParserState.ApcIntermediate);
        table.AddMany(Range(0x20, 0x30), EscapeParserState.ApcIntermediate, EscapeParserAction.Collect, EscapeParserState.ApcIntermediate);
        table.Add(0x7F, EscapeParserState.ApcIntermediate, EscapeParserAction.Ignore, EscapeParserState.ApcIntermediate);
        table.AddMany(printables, EscapeParserState.ApcPassthrough, EscapeParserAction.ApcPut, EscapeParserState.ApcPassthrough);
        table.AddMany(executables, EscapeParserState.ApcPassthrough, EscapeParserAction.Ignore, EscapeParserState.ApcPassthrough);
        table.AddMany(Range(0x08, 0x0E), EscapeParserState.ApcPassthrough, EscapeParserAction.ApcPut, EscapeParserState.ApcPassthrough);
        table.Add(0x7F, EscapeParserState.ApcPassthrough, EscapeParserAction.Ignore, EscapeParserState.ApcPassthrough);
        table.AddMany([0x1B, 0x9C, 0x18, 0x1A], EscapeParserState.ApcPassthrough, EscapeParserAction.ApcEnd, EscapeParserState.Ground);

        table.Add(0x5B, EscapeParserState.Escape, EscapeParserAction.Clear, EscapeParserState.CsiEntry);
        table.AddMany(Range(0x40, 0x7F), EscapeParserState.CsiEntry, EscapeParserAction.CsiDispatch, EscapeParserState.Ground);
        table.AddMany(Range(0x30, 0x3C), EscapeParserState.CsiEntry, EscapeParserAction.Parameter, EscapeParserState.CsiParameter);
        table.AddMany([0x3C, 0x3D, 0x3E, 0x3F], EscapeParserState.CsiEntry, EscapeParserAction.Collect, EscapeParserState.CsiParameter);
        table.AddMany(Range(0x30, 0x3C), EscapeParserState.CsiParameter, EscapeParserAction.Parameter, EscapeParserState.CsiParameter);
        table.AddMany(Range(0x40, 0x7F), EscapeParserState.CsiParameter, EscapeParserAction.CsiDispatch, EscapeParserState.Ground);
        table.AddMany([0x3C, 0x3D, 0x3E, 0x3F], EscapeParserState.CsiParameter, EscapeParserAction.Ignore, EscapeParserState.CsiIgnore);
        table.AddMany(Range(0x20, 0x40), EscapeParserState.CsiIgnore, EscapeParserAction.Ignore, EscapeParserState.CsiIgnore);
        table.Add(0x7F, EscapeParserState.CsiIgnore, EscapeParserAction.Ignore, EscapeParserState.CsiIgnore);
        table.AddMany(Range(0x40, 0x7F), EscapeParserState.CsiIgnore, EscapeParserAction.Ignore, EscapeParserState.Ground);
        table.AddMany(Range(0x20, 0x30), EscapeParserState.CsiEntry, EscapeParserAction.Collect, EscapeParserState.CsiIntermediate);
        table.AddMany(Range(0x20, 0x30), EscapeParserState.CsiIntermediate, EscapeParserAction.Collect, EscapeParserState.CsiIntermediate);
        table.AddMany(Range(0x30, 0x40), EscapeParserState.CsiIntermediate, EscapeParserAction.Ignore, EscapeParserState.CsiIgnore);
        table.AddMany(Range(0x40, 0x7F), EscapeParserState.CsiIntermediate, EscapeParserAction.CsiDispatch, EscapeParserState.Ground);
        table.AddMany(Range(0x20, 0x30), EscapeParserState.CsiParameter, EscapeParserAction.Collect, EscapeParserState.CsiIntermediate);

        table.AddMany(Range(0x20, 0x30), EscapeParserState.Escape, EscapeParserAction.Collect, EscapeParserState.EscapeIntermediate);
        table.AddMany(Range(0x20, 0x30), EscapeParserState.EscapeIntermediate, EscapeParserAction.Collect, EscapeParserState.EscapeIntermediate);
        table.AddMany(Range(0x30, 0x7F), EscapeParserState.EscapeIntermediate, EscapeParserAction.EscDispatch, EscapeParserState.Ground);
        table.AddMany(Range(0x30, 0x50), EscapeParserState.Escape, EscapeParserAction.EscDispatch, EscapeParserState.Ground);
        table.AddMany(Range(0x51, 0x58), EscapeParserState.Escape, EscapeParserAction.EscDispatch, EscapeParserState.Ground);
        table.AddMany([0x59, 0x5A, 0x5C], EscapeParserState.Escape, EscapeParserAction.EscDispatch, EscapeParserState.Ground);
        table.AddMany(Range(0x60, 0x7F), EscapeParserState.Escape, EscapeParserAction.EscDispatch, EscapeParserState.Ground);

        table.Add(0x50, EscapeParserState.Escape, EscapeParserAction.Clear, EscapeParserState.DcsEntry);
        table.AddMany(executables, EscapeParserState.DcsEntry, EscapeParserAction.Ignore, EscapeParserState.DcsEntry);
        table.Add(0x7F, EscapeParserState.DcsEntry, EscapeParserAction.Ignore, EscapeParserState.DcsEntry);
        table.AddMany(Range(0x20, 0x30), EscapeParserState.DcsEntry, EscapeParserAction.Collect, EscapeParserState.DcsIntermediate);
        table.AddMany(Range(0x30, 0x3C), EscapeParserState.DcsEntry, EscapeParserAction.Parameter, EscapeParserState.DcsParameter);
        table.AddMany([0x3C, 0x3D, 0x3E, 0x3F], EscapeParserState.DcsEntry, EscapeParserAction.Collect, EscapeParserState.DcsParameter);
        table.AddMany(executables, EscapeParserState.DcsIgnore, EscapeParserAction.Ignore, EscapeParserState.DcsIgnore);
        table.AddMany(Range(0x20, 0x80), EscapeParserState.DcsIgnore, EscapeParserAction.Ignore, EscapeParserState.DcsIgnore);
        table.AddMany(executables, EscapeParserState.DcsParameter, EscapeParserAction.Ignore, EscapeParserState.DcsParameter);
        table.Add(0x7F, EscapeParserState.DcsParameter, EscapeParserAction.Ignore, EscapeParserState.DcsParameter);
        table.AddMany(Range(0x30, 0x3C), EscapeParserState.DcsParameter, EscapeParserAction.Parameter, EscapeParserState.DcsParameter);
        table.AddMany([0x3C, 0x3D, 0x3E, 0x3F], EscapeParserState.DcsParameter, EscapeParserAction.Ignore, EscapeParserState.DcsIgnore);
        table.AddMany(Range(0x20, 0x30), EscapeParserState.DcsParameter, EscapeParserAction.Collect, EscapeParserState.DcsIntermediate);
        table.AddMany(executables, EscapeParserState.DcsIntermediate, EscapeParserAction.Ignore, EscapeParserState.DcsIntermediate);
        table.Add(0x7F, EscapeParserState.DcsIntermediate, EscapeParserAction.Ignore, EscapeParserState.DcsIntermediate);
        table.AddMany(Range(0x20, 0x30), EscapeParserState.DcsIntermediate, EscapeParserAction.Collect, EscapeParserState.DcsIntermediate);
        table.AddMany(Range(0x30, 0x40), EscapeParserState.DcsIntermediate, EscapeParserAction.Ignore, EscapeParserState.DcsIgnore);
        table.AddMany(Range(0x40, 0x7F), EscapeParserState.DcsIntermediate, EscapeParserAction.DcsHook, EscapeParserState.DcsPassthrough);
        table.AddMany(Range(0x40, 0x7F), EscapeParserState.DcsParameter, EscapeParserAction.DcsHook, EscapeParserState.DcsPassthrough);
        table.AddMany(Range(0x40, 0x7F), EscapeParserState.DcsEntry, EscapeParserAction.DcsHook, EscapeParserState.DcsPassthrough);
        table.AddMany(executables, EscapeParserState.DcsPassthrough, EscapeParserAction.DcsPut, EscapeParserState.DcsPassthrough);
        table.AddMany(printables, EscapeParserState.DcsPassthrough, EscapeParserAction.DcsPut, EscapeParserState.DcsPassthrough);
        table.Add(0x7F, EscapeParserState.DcsPassthrough, EscapeParserAction.Ignore, EscapeParserState.DcsPassthrough);
        table.AddMany([0x1B, 0x9C, 0x18, 0x1A], EscapeParserState.DcsPassthrough, EscapeParserAction.DcsUnhook, EscapeParserState.Ground);

        table.Add(NonAsciiPrintable, EscapeParserState.Ground, EscapeParserAction.Print, EscapeParserState.Ground);
        table.Add(NonAsciiPrintable, EscapeParserState.OscString, EscapeParserAction.OscPut, EscapeParserState.OscString);
        table.Add(NonAsciiPrintable, EscapeParserState.CsiIgnore, EscapeParserAction.Ignore, EscapeParserState.CsiIgnore);
        table.Add(NonAsciiPrintable, EscapeParserState.DcsIgnore, EscapeParserAction.Ignore, EscapeParserState.DcsIgnore);
        table.Add(NonAsciiPrintable, EscapeParserState.DcsPassthrough, EscapeParserAction.DcsPut, EscapeParserState.DcsPassthrough);
        table.Add(NonAsciiPrintable, EscapeParserState.ApcPassthrough, EscapeParserAction.ApcPut, EscapeParserState.ApcPassthrough);
        return table;
    }

    private static int[] Range(int start, int end) => Enumerable.Range(start, end - start).ToArray();
}

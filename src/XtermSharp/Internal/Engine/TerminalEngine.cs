using System.Collections.Immutable;
using System.Text;
using XtermSharp.Internal.Input;
using XtermSharp.Internal.Parser;

namespace XtermSharp.Internal.Engine;

internal sealed class TerminalEngine : IDisposable
{
    private const int TitleStackLimit = 10;
    private const string XtermVersion = "6.0.0";

    private TerminalOptions _options;
    private readonly UnicodeRegistry _unicode;
    private readonly EscapeSequenceParser _parser;
    private readonly uint[] _parserInput = new uint[1];
    private readonly XtermUtf8Decoder _utf8Decoder = new();
    private readonly MouseStateService _mouse = new();
    private readonly KittyKeyboard _kittyKeyboard = new();
    private readonly Win32InputMode _win32InputMode = new();
    private readonly CharsetService _charset = new();
    private readonly List<EngineEvent> _events = [];
    private readonly OscLinkService _oscLinks;
    private readonly List<string> _windowTitleStack = [];
    private readonly List<string> _iconNameStack = [];
    private TerminalBuffer _normal;
    private TerminalBuffer _alternate;
    private TerminalBuffer _active;
    private ModeState _modes = new();
    private CellStyle _style = CellStyle.Default;
    private bool[] _tabStops;
    private char? _pendingHighSurrogate;
    private Rune? _lastPrintedRune;
    private string? _lastPrintedGrapheme;
    private string _windowTitle = string.Empty;
    private string _iconName = string.Empty;
    private TerminalColor _sharedUnderlineColor;
    private TerminalUnderlineStyle _sharedUnderlineStyle;
    private int _sharedHyperlinkId;
    private bool _hasSharedExtendedAttributes;
    private ExtendedCellAttributes? _sharedExtendedAttributes;
    private int _dirtyStart = int.MaxValue;
    private int _dirtyEnd = -1;

    public TerminalEngine(TerminalOptions options, UnicodeRegistry unicode, EscapeSequenceParser parser)
    {
        _options = options;
        _unicode = unicode;
        _parser = parser;
        int columns = Math.Max(options.Columns, TerminalDimensions.MinimumColumns);
        int rows = Math.Max(options.Rows, TerminalDimensions.MinimumRows);
        _normal = new TerminalBuffer(TerminalBufferKind.Normal, columns, rows, options.Scrollback, EraseStyle);
        _alternate = new TerminalBuffer(TerminalBufferKind.Alternate, columns, rows, 0, EraseStyle);
        _active = _normal;
        _oscLinks = new OscLinkService(() => _active);
        _tabStops = CreateTabStops(columns);
        ConfigureParser();
    }

    public int Columns => _active.Columns;
    public int Rows => _active.Rows;
    internal TerminalOptions Options => _options;
    public int ViewportY => _active.YDisp;
    public TerminalBufferKind ActiveBufferKind => _active.Kind;
    internal TerminalBuffer ActiveBuffer => _active;
    internal int CurrentHyperlinkId => _style.HyperlinkId;
    internal IReadOnlyList<string> WindowTitleStack => _windowTitleStack;
    internal IReadOnlyList<string> IconNameStack => _iconNameStack;
    internal OscLinkData? GetLinkData(int linkId) => _oscLinks.GetLinkData(linkId);
    internal IReadOnlyList<TerminalKittyKeyboardFlags> KittyKeyboardStack =>
        (_active == _alternate ? _modes.KittyAlternateStack : _modes.KittyMainStack).Reverse().ToArray();

    private CellStyle EraseStyle => _style.ForErase();

    public async ValueTask WriteAsync(string data)
    {
        ArgumentNullException.ThrowIfNull(data);
        int index = 0;
        if (_pendingHighSurrogate is char high)
        {
            if (data.Length > 0 && char.IsLowSurrogate(data[0]))
            {
                await ProcessRuneAsync(new Rune(high, data[0])).ConfigureAwait(false);
                index = 1;
            }
            else
            {
                await ProcessRuneAsync(Rune.ReplacementChar).ConfigureAwait(false);
            }
            _pendingHighSurrogate = null;
        }

        while (index < data.Length)
        {
            char current = data[index++];
            if (char.IsHighSurrogate(current))
            {
                if (index == data.Length)
                {
                    _pendingHighSurrogate = current;
                    break;
                }
                char low = data[index];
                if (char.IsLowSurrogate(low))
                {
                    index++;
                    await ProcessRuneAsync(new Rune(current, low)).ConfigureAwait(false);
                }
                else
                {
                    await ProcessRuneAsync(Rune.ReplacementChar).ConfigureAwait(false);
                }
            }
            else if (char.IsLowSurrogate(current))
            {
                await ProcessRuneAsync(Rune.ReplacementChar).ConfigureAwait(false);
            }
            else if (current != '\uFEFF')
            {
                await ProcessRuneAsync(new Rune(current)).ConfigureAwait(false);
            }
        }
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data)
    {
        await _utf8Decoder.DecodeAsync(data, ProcessRuneAsync).ConfigureAwait(false);
    }

    private ValueTask ProcessRuneAsync(Rune rune)
    {
        _parserInput[0] = (uint)rune.Value;
        return _parser.ParseAsync(_parserInput);
    }

    private void ConfigureParser()
    {
        _parser.SetPrintHandler(PrintCodePoints);
        _parser.SetExecuteHandlerFallback(ExecuteControl);
        _parser.SetCsiHandlerFallback((identifier, parameters) =>
            HandleCsi(DecodeFunctionIdentifier(identifier), parameters));
        _parser.SetEscHandlerFallback(identifier => HandleEsc(DecodeFunctionIdentifier(identifier)));

        foreach (int identifier in new[] { 0, 1, 2, 4, 8, 10, 11, 12, 104, 110, 111, 112 })
        {
            RegisterBuiltInOscHandler(identifier);
        }

        FunctionIdentifier requestStatus = new('q', null, "$");
        _ = _parser.RegisterDcsHandler(requestStatus, (data, parameters) =>
        {
            HandleDcs(requestStatus, data, parameters);
            return true;
        });
    }

    private void PrintCodePoints(ReadOnlySpan<uint> data)
    {
        foreach (uint codePoint in data)
        {
            Print(new Rune((int)codePoint));
        }
    }

    private void RegisterBuiltInOscHandler(int identifier)
    {
        _ = _parser.RegisterOscHandler(identifier, data =>
        {
            HandleOsc(identifier, data);
            return true;
        });
    }

    private static FunctionIdentifier DecodeFunctionIdentifier(int identifier)
    {
        string value = EscapeSequenceParser.IdentifierToString(identifier);
        if (value.Length == 0)
        {
            throw new InvalidOperationException("A parser function identifier cannot be empty.");
        }

        int start = 0;
        char? prefix = null;
        if (value.Length > 1 && value[0] is >= '\x3C' and <= '\x3F')
        {
            prefix = value[0];
            start = 1;
        }
        return new FunctionIdentifier(value[^1], prefix, value[start..^1]);
    }

    public void ExecuteControl(int codePoint)
    {
        switch (codePoint)
        {
            case 0x07:
                _events.Add(new EngineEvent(EngineEventKind.Bell));
                break;
            case 0x08:
                Backspace();
                break;
            case 0x09:
                Tab();
                break;
            case 0x0A:
            case 0x0B:
            case 0x0C:
                LineFeed(_options.ConvertEol);
                break;
            case 0x0D:
                _active.CursorX = 0;
                _active.WrapPending = false;
                _lastPrintedRune = null;
                _lastPrintedGrapheme = null;
                CursorMoved();
                break;
            case 0x0E:
                _charset.SetGLevel(1);
                _lastPrintedRune = null;
                _lastPrintedGrapheme = null;
                break;
            case 0x0F:
                _charset.SetGLevel(0);
                _lastPrintedRune = null;
                _lastPrintedGrapheme = null;
                break;
            case 0x84:
                LineFeed(false);
                break;
            case 0x85:
                LineFeed(true);
                break;
            case 0x88:
                _tabStops[_active.CursorX] = true;
                break;
            case 0x8D:
                ReverseIndex();
                break;
        }
    }

    public void Print(Rune rune)
    {
        if (rune.Value == 0x00AD)
        {
            return;
        }
        string translated = _charset.Translate(rune);
        foreach (Rune translatedRune in translated.EnumerateRunes())
        {
            PrintCore(translatedRune);
        }
    }

    private void PrintCore(Rune rune)
    {
        if (_parser.PrecedingJoinState == 0)
        {
            _lastPrintedRune = null;
            _lastPrintedGrapheme = null;
        }
        IUnicodeProvider provider = _unicode.ActiveProvider;
        UnicodeCharacterProperties properties = provider.GetProperties(rune, _lastPrintedRune);
        if (properties.JoinPrevious || properties.Width == 0)
        {
            if (!AppendToPreviousCell(rune))
            {
                return;
            }
            _lastPrintedRune = rune;
            _lastPrintedGrapheme = string.Concat(_lastPrintedGrapheme, rune.ToString());
            _parser.PrecedingJoinState = 1;
            MarkDirty(_active.CursorY);
            return;
        }

        int width = Math.Clamp(properties.Width, 1, 2);
        if (_active.WrapPending)
        {
            if (_modes.Wraparound)
            {
                LineFeed(false);
                _active.CursorX = 0;
                _active.CursorLine.IsWrapped = true;
            }
            _active.WrapPending = false;
        }

        BufferLine line = _active.CursorLine;
        if (_active.CursorX > 0 && line.GetWidth(_active.CursorX - 1) == 2)
        {
            line.SetCell(_active.CursorX - 1, CreateCurrentBlankCell(1));
        }

        if (width == 2 && _active.CursorX == Columns - 1)
        {
            if (!_modes.Wraparound)
            {
                return;
            }
            // A wide character wraps before occupying the final single-width cell. Match
            // xterm.js by clearing that leftover cell on the old row before moving down.
            line.SetCell(_active.CursorX, CreateCurrentBlankCell(1));
            LineFeed(false);
            _active.CursorX = 0;
            _active.CursorLine.IsWrapped = true;
            line = _active.CursorLine;
        }

        if (_style.HyperlinkId != 0)
        {
            _oscLinks.AddLineToLink(_style.HyperlinkId, _active.YBase + _active.CursorY);
        }
        if (_modes.Insert)
        {
            line.InsertCells(_active.CursorX, width, EraseStyle);
        }

        line.SetCell(_active.CursorX, CreateCurrentCell(rune, (byte)width));
        if (width == 2 && _active.CursorX + 1 < Columns)
        {
            line.SetCell(_active.CursorX + 1, CreateCurrentBlankCell(0));
        }

        MarkDirty(_active.CursorY);
        _lastPrintedRune = rune;
        _lastPrintedGrapheme = rune.ToString();
        _parser.PrecedingJoinState = width;
        int next = _active.CursorX + width;
        if (next < Columns && line.GetWidth(next) == 0 && !line.HasContent(next))
        {
            line.SetCell(next, CreateCurrentBlankCell(1));
        }
        if (next >= Columns)
        {
            _active.CursorX = Columns - 1;
            _active.WrapPending = true;
        }
        else
        {
            _active.CursorX = next;
        }
        CursorMoved();
    }

    public void HandleEsc(FunctionIdentifier identifier)
    {
        if (!IsSupportedEscIdentifier(identifier))
        {
            return;
        }
        _lastPrintedRune = null;
        _lastPrintedGrapheme = null;
        if (identifier.Intermediates == "/")
        {
            return;
        }
        int charsetLevel = identifier.Intermediates switch
        {
            "(" => 0,
            ")" or "-" => 1,
            "*" or "." => 2,
            "+" or "/" => 3,
            _ => -1
        };
        if (charsetLevel >= 0)
        {
            _charset.SetGCharset(charsetLevel, CharsetMaps.Resolve(identifier.Final));
            return;
        }

        if (identifier.Intermediates == "%" && identifier.Final is '@' or 'G')
        {
            _charset.SetGLevel(0);
            _charset.SetGCharset(0, null);
            return;
        }

        if (identifier.Intermediates == "#" && identifier.Final == '8')
        {
            for (int row = 0; row < Rows; row++)
            {
                BufferLine line = _active.GetViewportLine(row);
                for (int column = 0; column < Columns; column++)
                {
                    line.SetCell(column, CellData.FromRune(new Rune('E'), 1, _style));
                }
            }
            MarkDirty(0, Rows - 1);
            return;
        }

        switch (identifier.Final)
        {
            case '7':
                SaveCursor();
                break;
            case '8':
                RestoreCursor();
                break;
            case 'D':
                LineFeed(false);
                break;
            case 'E':
                LineFeed(true);
                break;
            case 'H':
                _tabStops[_active.CursorX] = true;
                break;
            case 'M':
                ReverseIndex();
                break;
            case '=':
                _modes.ApplicationKeypad = true;
                break;
            case '>':
                _modes.ApplicationKeypad = false;
                break;
            case 'n':
            case '}':
                _charset.SetGLevel(2);
                break;
            case 'o':
            case '|':
                _charset.SetGLevel(3);
                break;
            case '~':
                _charset.SetGLevel(1);
                break;
            case 'c':
                Reset();
                break;
        }
    }

    private void HandleCsi(FunctionIdentifier identifier, CsiParameters parameters)
    {
        if (!IsSupportedCsiIdentifier(identifier))
        {
            return;
        }
        if (identifier.Final != 'b' || identifier.Prefix is not null || identifier.Intermediates.Length != 0)
        {
            _lastPrintedRune = null;
            _lastPrintedGrapheme = null;
        }
        RestrictCursor();
        int count = parameters.GetOrDefault(0);
        switch (identifier.Final)
        {
            case '@' when identifier.Intermediates == " ":
                ScrollColumns(count, left: true);
                break;
            case '@':
                _active.WrapPending = false;
                _active.CursorLine.InsertCells(_active.CursorX, count, EraseStyle);
                MarkDirty(_active.CursorY);
                break;
            case 'A' when identifier.Intermediates == " ": ScrollColumns(count, left: false); break;
            case 'A': MoveCursorY(-count); break;
            case 'B': MoveCursorY(count); break;
            case 'C': MoveCursorX(count); break;
            case 'D': MoveCursorX(-count); break;
            case 'E': MoveCursorY(count); _active.CursorX = 0; break;
            case 'F': MoveCursorY(-count); _active.CursorX = 0; break;
            case 'G': SetCursorColumn(count); break;
            case 'H':
            case 'f': SetCursorPosition(parameters); break;
            case 'I': CursorForwardTab(count); break;
            case 'J':
                if (identifier.Prefix == '?')
                {
                    SelectiveEraseInDisplay(parameters.Values.Length == 0 ? 0 : parameters.Values[0]);
                }
                else
                {
                    EraseInDisplay(parameters.Values.Length == 0 ? 0 : parameters.Values[0]);
                }
                break;
            case 'K':
                if (identifier.Prefix == '?')
                {
                    SelectiveEraseInLine(parameters.Values.Length == 0 ? 0 : parameters.Values[0]);
                }
                else
                {
                    EraseInLine(parameters.Values.Length == 0 ? 0 : parameters.Values[0]);
                }
                break;
            case 'L':
                _active.CursorX = 0;
                _active.WrapPending = false;
                _active.InsertLines(count, EraseStyle);
                MarkDirty(_active.CursorY, _active.ScrollBottom);
                break;
            case 'M':
                _active.CursorX = 0;
                _active.WrapPending = false;
                _active.DeleteLines(count, EraseStyle);
                MarkDirty(_active.CursorY, _active.ScrollBottom);
                break;
            case 'P':
                _active.WrapPending = false;
                _active.CursorLine.DeleteCells(_active.CursorX, count, EraseStyle);
                MarkDirty(_active.CursorY);
                break;
            case 'S': _active.ScrollUp(count, EraseStyle); MarkDirty(_active.ScrollTop, _active.ScrollBottom); ScrollChanged(); break;
            case 'T': _active.ScrollDown(count, EraseStyle); MarkDirty(_active.ScrollTop, _active.ScrollBottom); break;
            case '^': _active.ScrollDown(count, EraseStyle); MarkDirty(_active.ScrollTop, _active.ScrollBottom); break;
            case 'X':
                _active.WrapPending = false;
                _active.CursorLine.ReplaceCells(
                    _active.CursorX,
                    _active.CursorX + count,
                    CellData.Blank(EraseStyle));
                MarkDirty(_active.CursorY);
                break;
            case 'Z': CursorBackwardTab(count); break;
            case '`': SetCursorColumn(count); break;
            case 'a': MoveCursorX(count); break;
            case 'b': RepeatLastCharacter(count); break;
            case 'c' when identifier.Prefix is null && identifier.Intermediates.Length == 0:
                SendDeviceAttributesPrimary(parameters);
                break;
            case 'c' when identifier.Prefix == '>' && identifier.Intermediates.Length == 0:
                SendDeviceAttributesSecondary(parameters);
                break;
            case 'd': SetCursorRow(count); break;
            case 'e': MoveCursorYFull(count); break;
            case 'g': ClearTabStop(parameters.Values.Length == 0 ? 0 : parameters.Values[0]); break;
            case 'h': SetModes(identifier.Prefix, parameters, true); break;
            case 'l': SetModes(identifier.Prefix, parameters, false); break;
            case 'm': SelectGraphicRendition(parameters); break;
            case 'n': DeviceStatusReport(identifier.Prefix, parameters); break;
            case 'r': SetScrollRegion(parameters); break;
            case 's': SaveCursor(); break;
            case 't' when identifier.Prefix is null && identifier.Intermediates.Length == 0:
                HandleWindowOptions(parameters);
                break;
            case 'u' when identifier.Prefix == '=': SetKittyKeyboard(parameters); break;
            case 'u' when identifier.Prefix == '?': QueryKittyKeyboard(); break;
            case 'u' when identifier.Prefix == '>': PushKittyKeyboard(parameters); break;
            case 'u' when identifier.Prefix == '<': PopKittyKeyboard(parameters); break;
            case 'u': RestoreCursor(); break;
            case '}' when identifier.Intermediates == "'": ShiftColumns(count, insert: true); break;
            case '~' when identifier.Intermediates == "'": ShiftColumns(count, insert: false); break;
            case 'p' when identifier.Intermediates == "!": SoftReset(); break;
            case 'p' when identifier.Intermediates == "$": RequestMode(identifier.Prefix, parameters); break;
            case 'q' when identifier.Prefix == '>' && identifier.Intermediates.Length == 0:
                SendXtVersion(parameters);
                break;
            case 'q' when identifier.Intermediates == " ": SetCursorStyle(parameters); break;
            case 'q' when identifier.Intermediates == "\"":
                _style = _style with { IsProtected = parameters.GetOrDefault(0, 0) == 1 };
                break;
        }
    }

    private static bool IsSupportedCsiIdentifier(FunctionIdentifier identifier)
    {
        if (identifier.Intermediates.Length == 0)
        {
            return identifier.Prefix switch
            {
                null => identifier.Final is '@' or 'A' or 'B' or 'C' or 'D' or 'E' or 'F' or 'G' or
                    'H' or 'I' or 'J' or 'K' or 'L' or 'M' or 'P' or 'S' or 'T' or 'X' or 'Z' or
                    '^' or '`' or 'a' or 'b' or 'c' or 'd' or 'e' or 'f' or 'g' or 'h' or 'l' or
                    'm' or 'n' or 'r' or 's' or 't' or 'u',
                '?' => identifier.Final is 'J' or 'K' or 'h' or 'l' or 'n' or 'u',
                '>' => identifier.Final is 'c' or 'q' or 'u',
                '=' or '<' => identifier.Final == 'u',
                _ => false
            };
        }

        return (identifier.Prefix, identifier.Intermediates, identifier.Final) switch
        {
            (null, " ", '@' or 'A' or 'q') => true,
            (null, "!", 'p') => true,
            (null, "$", 'p') => true,
            ('?', "$", 'p') => true,
            (null, "\"", 'q') => true,
            (null, "'", '}' or '~') => true,
            _ => false
        };
    }

    private static bool IsSupportedEscIdentifier(FunctionIdentifier identifier)
    {
        if (identifier.Prefix is not null)
        {
            return false;
        }
        if (identifier.Intermediates.Length == 0)
        {
            return identifier.Final is '7' or '8' or 'D' or 'E' or 'H' or 'M' or '=' or '>' or
                'n' or 'o' or '|' or '}' or '~' or 'c';
        }
        if (identifier.Intermediates == "%")
        {
            return identifier.Final is '@' or 'G';
        }
        if (identifier.Intermediates == "#")
        {
            return identifier.Final == '8';
        }
        return identifier.Intermediates is "(" or ")" or "*" or "+" or "-" or "." or "/" &&
            identifier.Final is '0' or 'A' or 'B' or '4' or 'C' or '5' or 'R' or 'Q' or 'K' or
                'Y' or 'E' or '6' or 'Z' or 'H' or '7' or '=';
    }

    private void ScrollColumns(int count, bool left)
    {
        if (_active.CursorY < _active.ScrollTop || _active.CursorY > _active.ScrollBottom)
        {
            return;
        }
        for (int row = _active.ScrollTop; row <= _active.ScrollBottom; row++)
        {
            BufferLine line = _active.GetViewportLine(row);
            if (left)
            {
                line.DeleteCells(0, count, EraseStyle);
            }
            else
            {
                line.InsertCells(0, count, EraseStyle);
            }
            line.IsWrapped = false;
        }
        MarkDirty(_active.ScrollTop, _active.ScrollBottom);
    }

    private void ShiftColumns(int count, bool insert)
    {
        if (_active.CursorY < _active.ScrollTop || _active.CursorY > _active.ScrollBottom)
        {
            return;
        }
        for (int row = _active.ScrollTop; row <= _active.ScrollBottom; row++)
        {
            BufferLine line = _active.GetViewportLine(row);
            if (insert)
            {
                line.InsertCells(_active.CursorX, count, EraseStyle);
            }
            else
            {
                line.DeleteCells(_active.CursorX, count, EraseStyle);
            }
            line.IsWrapped = false;
        }
        MarkDirty(_active.ScrollTop, _active.ScrollBottom);
    }

    private void SetCursorStyle(CsiParameters parameters)
    {
        int value = parameters.Values.Length == 0 ? 1 : parameters.Values[0];
        if (value == 0)
        {
            _modes.CursorStyle = null;
            _modes.CursorBlink = null;
            return;
        }
        _modes.CursorStyle = value switch
        {
            1 or 2 => TerminalCursorStyle.Block,
            3 or 4 => TerminalCursorStyle.Underline,
            5 or 6 => TerminalCursorStyle.Bar,
            _ => _modes.CursorStyle
        };
        if (value is >= 1 and <= 6)
        {
            _modes.CursorBlink = value % 2 == 1;
        }
    }

    private void HandleWindowOptions(CsiParameters parameters)
    {
        int operation = parameters.Values.Length == 0 ? 0 : parameters.Values[0];
        if (!IsWindowOperationAllowed(operation))
        {
            return;
        }

        int selector = parameters.Values.Length > 1 ? parameters.Values[1] : 0;
        switch (operation)
        {
            case 14:
            case 16:
                // Pixel reports require renderer metrics and intentionally have no headless reply.
                break;
            case 18:
                EmitData($"\x1b[8;{Rows};{Columns}t");
                break;
            case 20:
                EmitData($"\x1b]L{_iconName}\x1b\\");
                break;
            case 21:
                EmitData($"\x1b]l{_windowTitle}\x1b\\");
                break;
            case 22:
                if (selector is 0 or 2)
                {
                    PushTitle(_windowTitleStack, _windowTitle);
                }
                if (selector is 0 or 1)
                {
                    PushTitle(_iconNameStack, _iconName);
                }
                break;
            case 23:
                if (selector is 0 or 2 && TryPopTitle(_windowTitleStack, out string windowTitle))
                {
                    SetWindowTitle(windowTitle);
                }
                if (selector is 0 or 1 && TryPopTitle(_iconNameStack, out string iconName))
                {
                    SetIconName(iconName);
                }
                break;
        }
    }

    private bool IsWindowOperationAllowed(int operation)
    {
        TerminalWindowOptions options = _options.WindowOptions;
        if (operation > 24)
        {
            return options.SetWindowLines;
        }
        return operation switch
        {
            1 => options.RestoreWindow,
            2 => options.MinimizeWindow,
            3 => options.SetWindowPosition,
            4 => options.SetWindowSizePixels,
            5 => options.RaiseWindow,
            6 => options.LowerWindow,
            7 => options.RefreshWindow,
            8 => options.SetWindowSizeCharacters,
            9 => options.MaximizeWindow,
            10 => options.FullscreenWindow,
            11 => options.GetWindowState,
            13 => options.GetWindowPosition,
            14 => options.GetWindowSizePixels,
            15 => options.GetScreenSizePixels,
            16 => options.GetCellSizePixels,
            18 => options.GetWindowSizeCharacters,
            19 => options.GetScreenSizeCharacters,
            20 => options.GetIconTitle,
            21 => options.GetWindowTitle,
            22 => options.PushTitle,
            23 => options.PopTitle,
            24 => options.SetWindowLines,
            _ => false
        };
    }

    private static void PushTitle(List<string> stack, string title)
    {
        if (stack.Count >= TitleStackLimit)
        {
            stack.RemoveAt(0);
        }
        stack.Add(title);
    }

    private static bool TryPopTitle(List<string> stack, out string title)
    {
        if (stack.Count == 0)
        {
            title = string.Empty;
            return false;
        }
        int index = stack.Count - 1;
        title = stack[index];
        stack.RemoveAt(index);
        return true;
    }

    private void SendXtVersion(CsiParameters parameters)
    {
        int value = parameters.Values.Length == 0 ? 0 : parameters.Values[0];
        if (value <= 0)
        {
            EmitData($"\x1bP>|xterm.js({XtermVersion})\x1b\\");
        }
    }

    private void SendDeviceAttributesPrimary(CsiParameters parameters)
    {
        int value = parameters.Values.Length == 0 ? 0 : parameters.Values[0];
        if (value <= 0)
        {
            EmitData("\x1b[?1;2c");
        }
    }

    private void SendDeviceAttributesSecondary(CsiParameters parameters)
    {
        int value = parameters.Values.Length == 0 ? 0 : parameters.Values[0];
        if (value <= 0)
        {
            EmitData("\x1b[>0;276;0c");
        }
    }

    private void RestrictCursor()
    {
        int cursorX = Math.Clamp(_active.CursorX, 0, Columns - 1);
        int cursorY = Math.Clamp(_active.CursorY, 0, Rows - 1);
        if (cursorX != _active.CursorX)
        {
            _active.CursorX = cursorX;
        }
        if (cursorY != _active.CursorY)
        {
            _active.CursorY = cursorY;
        }
    }

    public void HandleOsc(int identifier, string data)
    {
        switch (identifier)
        {
            case 0:
                SetIconName(data);
                SetWindowTitle(data);
                break;
            case 1:
                SetIconName(data);
                break;
            case 2:
                SetWindowTitle(data);
                break;
            case 8:
                SetHyperlink(data);
                break;
            case 4:
                SetOrReportIndexedColor(data);
                break;
            case 10:
                SetOrReportSpecialColor(data, 0);
                break;
            case 11:
                SetOrReportSpecialColor(data, 1);
                break;
            case 12:
                SetOrReportSpecialColor(data, 2);
                break;
            case 104:
                RestoreIndexedColor(data);
                break;
            case 110:
                EmitColorRequests(new TerminalColorRequest(
                    TerminalColorRequestType.Restore,
                    (int)TerminalSpecialColorIndex.Foreground));
                break;
            case 111:
                EmitColorRequests(new TerminalColorRequest(
                    TerminalColorRequestType.Restore,
                    (int)TerminalSpecialColorIndex.Background));
                break;
            case 112:
                EmitColorRequests(new TerminalColorRequest(
                    TerminalColorRequestType.Restore,
                    (int)TerminalSpecialColorIndex.Cursor));
                break;
        }
    }

    private void SetWindowTitle(string title)
    {
        _windowTitle = title;
        _events.Add(new EngineEvent(EngineEventKind.TitleChanged, title));
    }

    private void SetIconName(string iconName) => _iconName = iconName;

    public void HandleDcs(FunctionIdentifier identifier, string data, CsiParameters parameters)
    {
        if (identifier.Final == 'q' && identifier.Intermediates == "$" && identifier.Prefix is null)
        {
            RequestStatusString(data);
        }
    }

    public void HandleApc(FunctionIdentifier identifier, string data)
    {
    }

    public void Resize(int columns, int rows)
    {
        if (columns <= 0 || rows <= 0)
        {
            throw new ArgumentOutOfRangeException(columns <= 0 ? nameof(columns) : nameof(rows));
        }
        if (columns == Columns && rows == Rows)
        {
            return;
        }

        int effectiveColumns = Math.Max(columns, TerminalDimensions.MinimumColumns);
        int effectiveRows = Math.Max(rows, TerminalDimensions.MinimumRows);
        _normal.Resize(effectiveColumns, effectiveRows, _options.Scrollback, EraseStyle);
        _alternate.Resize(effectiveColumns, effectiveRows, 0, EraseStyle);
        Array.Resize(ref _tabStops, effectiveColumns);
        for (int i = 0; i < effectiveColumns; i++)
        {
            if (i % _options.TabStopWidth == 0)
            {
                _tabStops[i] = true;
            }
        }
        _events.Add(new EngineEvent(EngineEventKind.Resize, First: effectiveColumns, Second: effectiveRows));
        MarkDirty(0, effectiveRows - 1);
    }

    public TerminalOptions UpdateOptions(TerminalOptionsUpdate update)
    {
        TerminalOptions previous = _options;
        TerminalOptions next = previous.Apply(update);
        _options = next;

        if (next.UnicodeVersion != previous.UnicodeVersion)
        {
            _unicode.ActiveVersion = next.UnicodeVersion;
        }
        if (next.TabStopWidth != previous.TabStopWidth)
        {
            _tabStops = CreateTabStops(Columns);
        }
        if (next.Scrollback != previous.Scrollback)
        {
            _normal.Resize(Columns, Rows, next.Scrollback, EraseStyle);
            MarkDirty(0, Rows - 1);
            ScrollChanged();
        }
        _events.Add(new EngineEvent(
            EngineEventKind.OptionsChanged,
            PreviousOptions: previous,
            CurrentOptions: next));
        return next;
    }

    public void Reset()
    {
        _style = CellStyle.Default;
        InvalidateSharedExtendedAttributes();
        _modes = new ModeState();
        _mouse.Reset();
        _charset.Reset();
        _normal.Reset(EraseStyle);
        _alternate.Reset(EraseStyle);
        _active = _normal;
        _parser.Reset();
        _utf8Decoder.Reset();
        _pendingHighSurrogate = null;
        _lastPrintedRune = null;
        _lastPrintedGrapheme = null;
        _tabStops = CreateTabStops(Columns);
        MarkDirty(0, Rows - 1);
        CursorMoved();
        ScrollChanged();
    }

    public void Clear()
    {
        _active.Clear(EraseStyle);
        MarkDirty(0, Rows - 1);
        CursorMoved();
        ScrollChanged();
    }

    public void ScrollLines(int amount)
    {
        _active.ScrollLines(amount);
        ScrollChanged();
        MarkDirty(0, Rows - 1);
    }

    public void ScrollTo(int line)
    {
        _active.ScrollTo(line);
        ScrollChanged();
        MarkDirty(0, Rows - 1);
    }

    public void ScrollToBottom()
    {
        _active.ScrollToBottom();
        ScrollChanged();
        MarkDirty(0, Rows - 1);
    }

    public void SendInput(string data, bool wasUserInput)
    {
        if (wasUserInput && _options.ScrollOnUserInput)
        {
            _active.ScrollToBottom();
            ScrollChanged();
        }
        _events.Add(new EngineEvent(EngineEventKind.Data, data));
    }

    public void Paste(string data)
    {
        string prepared = data.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\n', '\r');
        if (_modes.BracketedPaste)
        {
            prepared = prepared.Replace("\x1b", "\u241b", StringComparison.Ordinal);
            prepared = $"\x1b[200~{prepared}\x1b[201~";
        }
        SendInput(prepared, wasUserInput: true);
    }

    public void SendFocus(bool focused)
    {
        if (_modes.SendFocus)
        {
            SendInput(focused ? "\x1b[I" : "\x1b[O", wasUserInput: false);
        }
    }

    public void SendMouse(TerminalMouseEvent value)
    {
        if (_mouse.TryEncode(value, out string sequence))
        {
            SendInput(sequence, wasUserInput: true);
        }
    }

    public void SendKey(TerminalKeyEvent value)
    {
        KeyboardResult result;
        bool modifierOnly = false;
        if (_modes.Win32InputMode && _options.EnableWin32InputMode)
        {
            result = _win32InputMode.Evaluate(value);
            modifierOnly = value.KeyCode is 16 or 17 or 18 or 91 or 92 or 93 or 224 || value.Key == "Meta";
        }
        else if (_modes.KittyKeyboardFlags != TerminalKittyKeyboardFlags.None && _options.EnableKittyKeyboard)
        {
            result = _kittyKeyboard.Evaluate(
                value,
                (KittyKeyboardFlags)(byte)_modes.KittyKeyboardFlags,
                macOptionAsAlt: OperatingSystem.IsMacOS() && _options.MacOptionIsMeta);
        }
        else
        {
            if (value.EventType == TerminalKeyEventType.Release)
            {
                return;
            }
            result = Keyboard.Evaluate(
                value,
                _modes.ApplicationCursorKeys,
                OperatingSystem.IsMacOS(),
                _options.MacOptionIsMeta);
        }

        switch (result.Type)
        {
            case KeyboardResultType.PageUp:
                ScrollLines(-(Rows - 1));
                break;
            case KeyboardResultType.PageDown:
                ScrollLines(Rows - 1);
                break;
            case KeyboardResultType.SendKey when result.Key is not null:
                SendInput(result.Key, wasUserInput: !modifierOnly);
                break;
        }
    }

    public TerminalMarker RegisterMarker(int cursorYOffset)
    {
        int line = _active.YBase + _active.CursorY + cursorYOffset;
        return _active.AddMarker(line);
    }

    public TerminalSnapshot CreateSnapshot(long revision, SnapshotScope scope)
    {
        TerminalBufferSnapshot active = _active.CreateSnapshot(scope == SnapshotScope.AllBuffers ? SnapshotScope.ActiveBuffer : scope);
        TerminalBufferSnapshot? normal = null;
        TerminalBufferSnapshot? alternate = null;
        if (scope == SnapshotScope.AllBuffers)
        {
            normal = _normal.CreateSnapshot(SnapshotScope.ActiveBuffer);
            alternate = _active == _alternate
                ? _alternate.CreateSnapshot(SnapshotScope.ActiveBuffer)
                : new TerminalBufferSnapshot(
                    TerminalBufferKind.Alternate,
                    0,
                    0,
                    0,
                    0,
                    ImmutableArray<TerminalLineSnapshot>.Empty);
        }

        return new TerminalSnapshot(
            revision,
            Columns,
            Rows,
            _active.Kind,
            _modes.Snapshot(),
            active,
            normal,
            alternate);
    }

    public IReadOnlyList<EngineEvent> ConsumeEvents(bool includeWriteParsed)
    {
        if (_dirtyEnd >= _dirtyStart)
        {
            _events.Add(new EngineEvent(EngineEventKind.Render, First: _dirtyStart, Second: _dirtyEnd));
        }
        if (includeWriteParsed)
        {
            _events.Add(new EngineEvent(EngineEventKind.WriteParsed));
        }
        var result = new List<EngineEvent>(_events.Count);
        int lastCursorMoved = _events.FindLastIndex(static terminalEvent =>
            terminalEvent.Kind == EngineEventKind.CursorMoved);
        for (int index = 0; index < _events.Count; index++)
        {
            EngineEvent terminalEvent = _events[index];
            if (terminalEvent.Kind == EngineEventKind.CursorMoved)
            {
                if (index != lastCursorMoved)
                {
                    continue;
                }
            }
            result.Add(terminalEvent);
        }
        _events.Clear();
        _dirtyStart = int.MaxValue;
        _dirtyEnd = -1;
        return result;
    }

    public void Dispose()
    {
        _parser.Dispose();
        _normal.Dispose();
        _alternate.Dispose();
        _events.Clear();
    }

    private void LineFeed(bool carriageReturn)
    {
        int previousBase = _active.YBase;
        int previousRow = _active.CursorY;
        bool scrolled = _active.CursorY == _active.ScrollBottom;
        if (scrolled)
        {
            _active.ScrollUp(1, EraseStyle);
        }
        else
        {
            _active.CursorY = Math.Min(Rows - 1, _active.CursorY + 1);
        }
        if (carriageReturn)
        {
            _active.CursorX = 0;
        }
        _active.WrapPending = false;
        _lastPrintedRune = null;
        _lastPrintedGrapheme = null;
        _events.Add(new EngineEvent(EngineEventKind.LineFeed));
        if (_active.YBase != previousBase)
        {
            ScrollChanged();
        }
        if (scrolled)
        {
            MarkDirty(_active.ScrollTop, _active.ScrollBottom);
        }
        else
        {
            MarkDirty(previousRow, _active.CursorY);
        }
        CursorMoved();
    }

    private void ReverseIndex()
    {
        if (_active.CursorY == _active.ScrollTop)
        {
            _active.ScrollDown(1, EraseStyle);
            MarkDirty(_active.ScrollTop, _active.ScrollBottom);
        }
        else
        {
            _active.CursorY = Math.Max(0, _active.CursorY - 1);
        }
        _active.WrapPending = false;
        _lastPrintedRune = null;
        _lastPrintedGrapheme = null;
        CursorMoved();
    }

    private void Backspace()
    {
        if (!_modes.ReverseWraparound)
        {
            if (_active.WrapPending)
            {
                _active.WrapPending = false;
            }
            if (_active.CursorX > 0)
            {
                _active.CursorX--;
            }
        }
        else if (_active.WrapPending)
        {
            _active.WrapPending = false;
        }
        else if (_active.CursorX > 0)
        {
            _active.CursorX--;
        }
        else if (_active.CursorY > _active.ScrollTop &&
                 _active.CursorY <= _active.ScrollBottom &&
                 _active.CursorLine.IsWrapped)
        {
            _active.CursorLine.IsWrapped = false;
            _active.CursorY--;
            _active.CursorX = Columns - 1;
            BufferLine previous = _active.CursorLine;
            if (previous[_active.CursorX].Width == 1 && previous[_active.CursorX].CodePoint == 0)
            {
                _active.CursorX = Math.Max(0, _active.CursorX - 1);
            }
        }
        _lastPrintedRune = null;
        _lastPrintedGrapheme = null;
        CursorMoved();
    }

    private void Tab()
    {
        if (_active.WrapPending)
        {
            return;
        }
        int column = _active.CursorX + 1;
        while (column < Columns - 1 && !_tabStops[column])
        {
            column++;
        }
        _active.CursorX = Math.Min(column, Columns - 1);
        _active.WrapPending = false;
        _lastPrintedRune = null;
        _lastPrintedGrapheme = null;
        CursorMoved();
    }

    private void CursorForwardTab(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Tab();
        }
    }

    private void CursorBackwardTab(int count)
    {
        // xterm.js represents a pending wrap as x === cols. CBT is a no-op in that
        // state; later controls can restrict the cursor back to the last physical cell.
        if (_active.LogicalCursorX >= Columns)
        {
            return;
        }
        int column = _active.CursorX;
        for (int n = 0; n < count; n++)
        {
            column--;
            while (column > 0 && !_tabStops[column])
            {
                column--;
            }
        }
        _active.CursorX = Math.Max(0, column);
        _active.WrapPending = false;
        _lastPrintedRune = null;
        _lastPrintedGrapheme = null;
        CursorMoved();
    }

    private void ClearTabStop(int mode)
    {
        if (mode == 0)
        {
            _tabStops[_active.CursorX] = false;
        }
        else if (mode == 3)
        {
            Array.Clear(_tabStops);
        }
    }

    private void MoveCursorX(int delta)
    {
        _active.CursorX = Math.Clamp(_active.CursorX + delta, 0, Columns - 1);
        _active.WrapPending = false;
        _lastPrintedRune = null;
        _lastPrintedGrapheme = null;
        CursorMoved();
    }

    private void MoveCursorY(int delta)
    {
        int minimum;
        int maximum;
        if (_modes.Origin)
        {
            minimum = _active.ScrollTop;
            maximum = _active.ScrollBottom;
        }
        else if (delta < 0)
        {
            minimum = _active.CursorY < _active.ScrollTop ? 0 : _active.ScrollTop;
            maximum = Rows - 1;
        }
        else
        {
            minimum = 0;
            maximum = _active.CursorY > _active.ScrollBottom ? Rows - 1 : _active.ScrollBottom;
        }
        _active.CursorY = Math.Clamp(_active.CursorY + delta, minimum, maximum);
        _active.WrapPending = false;
        _lastPrintedRune = null;
        _lastPrintedGrapheme = null;
        CursorMoved();
    }

    private void MoveCursorYFull(int delta)
    {
        _active.CursorY = Math.Clamp(_active.CursorY + delta, 0, Rows - 1);
        _active.WrapPending = false;
        _lastPrintedRune = null;
        _lastPrintedGrapheme = null;
        CursorMoved();
    }

    private void SetCursorColumn(int oneBasedColumn)
    {
        _active.CursorX = Math.Clamp(oneBasedColumn - 1, 0, Columns - 1);
        _active.WrapPending = false;
        _lastPrintedRune = null;
        _lastPrintedGrapheme = null;
        CursorMoved();
    }

    private void SetCursorRow(int oneBasedRow)
    {
        int row = Math.Max(0, oneBasedRow - 1);
        if (_modes.Origin)
        {
            row += _active.ScrollTop;
            row = Math.Clamp(row, _active.ScrollTop, _active.ScrollBottom);
        }
        else
        {
            row = Math.Min(row, Rows - 1);
        }
        _active.CursorY = row;
        _active.WrapPending = false;
        _lastPrintedRune = null;
        _lastPrintedGrapheme = null;
        CursorMoved();
    }

    private void SetCursorPosition(CsiParameters parameters)
    {
        int row = parameters.GetOrDefault(0) - 1;
        int column = parameters.GetOrDefault(1) - 1;
        if (_modes.Origin)
        {
            row += _active.ScrollTop;
            row = Math.Clamp(row, _active.ScrollTop, _active.ScrollBottom);
        }
        else
        {
            row = Math.Clamp(row, 0, Rows - 1);
        }
        _active.CursorY = row;
        _active.CursorX = Math.Clamp(column, 0, Columns - 1);
        _active.WrapPending = false;
        _lastPrintedRune = null;
        _lastPrintedGrapheme = null;
        CursorMoved();
    }

    private void SetScrollRegion(CsiParameters parameters)
    {
        int top = parameters.GetOrDefault(0) - 1;
        int bottom = parameters.Values.Length > 1 && parameters.Values[1] != 0
            ? parameters.Values[1] - 1
            : Rows - 1;
        top = Math.Clamp(top, 0, Rows - 1);
        bottom = Math.Clamp(bottom, 0, Rows - 1);
        if (top >= bottom)
        {
            return;
        }
        _active.ScrollTop = top;
        _active.ScrollBottom = bottom;
        _active.CursorX = 0;
        _active.CursorY = _modes.Origin ? top : 0;
        _active.WrapPending = false;
        CursorMoved();
    }

    private void EraseInDisplay(int mode)
    {
        int cursorColumn = _active.LogicalCursorX;
        switch (mode)
        {
            case 0:
                _active.CursorLine.ReplaceCells(cursorColumn, Columns, CellData.Blank(EraseStyle));
                if (cursorColumn == 0)
                {
                    _active.CursorLine.IsWrapped = false;
                }
                for (int row = _active.CursorY + 1; row < Rows; row++)
                {
                    BufferLine line = _active.GetViewportLine(row);
                    line.ReplaceCells(0, Columns, CellData.Blank(EraseStyle));
                    line.IsWrapped = false;
                }
                MarkDirty(_active.CursorY, Rows - 1);
                break;
            case 1:
                for (int row = 0; row < _active.CursorY; row++)
                {
                    BufferLine line = _active.GetViewportLine(row);
                    line.ReplaceCells(0, Columns, CellData.Blank(EraseStyle));
                    line.IsWrapped = false;
                }
                _active.CursorLine.ReplaceCells(0, cursorColumn + 1, CellData.Blank(EraseStyle));
                _active.CursorLine.IsWrapped = false;
                if (cursorColumn + 1 >= Columns && _active.CursorY + 1 < Rows)
                {
                    _active.GetViewportLine(_active.CursorY + 1).IsWrapped = false;
                }
                MarkDirty(0, _active.CursorY);
                break;
            case 2:
                if (_options.ScrollOnEraseInDisplay && _active.Kind == TerminalBufferKind.Normal)
                {
                    int lastContentRow = Rows - 1;
                    while (lastContentRow >= 0 && _active.GetViewportLine(lastContentRow).GetTrimmedLength() == 0)
                    {
                        lastContentRow--;
                    }
                    for (int row = 0; row <= lastContentRow; row++)
                    {
                        _active.ScrollUp(1, EraseStyle);
                    }
                    ScrollChanged();
                }
                else
                {
                    for (int row = 0; row < Rows; row++)
                    {
                        BufferLine line = _active.GetViewportLine(row);
                        line.ReplaceCells(0, Columns, CellData.Blank(EraseStyle));
                        line.IsWrapped = false;
                    }
                }
                MarkDirty(0, Rows - 1);
                break;
            case 3:
                _active.ClearScrollback();
                ScrollChanged();
                MarkDirty(0, Rows - 1);
                break;
        }
    }

    private void EraseInLine(int mode)
    {
        int cursorColumn = _active.LogicalCursorX;
        switch (mode)
        {
            case 0:
                _active.CursorLine.ReplaceCells(cursorColumn, Columns, CellData.Blank(EraseStyle));
                if (cursorColumn == 0)
                {
                    _active.CursorLine.IsWrapped = false;
                }
                break;
            case 1: _active.CursorLine.ReplaceCells(0, cursorColumn + 1, CellData.Blank(EraseStyle)); break;
            case 2:
                _active.CursorLine.ReplaceCells(0, Columns, CellData.Blank(EraseStyle));
                _active.CursorLine.IsWrapped = false;
                break;
        }
        MarkDirty(_active.CursorY);
    }

    private void SelectiveEraseInDisplay(int mode)
    {
        int cursorColumn = _active.LogicalCursorX;
        switch (mode)
        {
            case 0:
                _active.CursorLine.ReplaceCells(cursorColumn, Columns, CellData.Blank(EraseStyle), respectProtected: true);
                for (int row = _active.CursorY + 1; row < Rows; row++)
                {
                    _active.GetViewportLine(row).ReplaceCells(0, Columns, CellData.Blank(EraseStyle), respectProtected: true);
                }
                MarkDirty(_active.CursorY, Rows - 1);
                break;
            case 1:
                for (int row = 0; row < _active.CursorY; row++)
                {
                    _active.GetViewportLine(row).ReplaceCells(0, Columns, CellData.Blank(EraseStyle), respectProtected: true);
                }
                _active.CursorLine.ReplaceCells(0, cursorColumn + 1, CellData.Blank(EraseStyle), respectProtected: true);
                MarkDirty(0, _active.CursorY);
                break;
            case 2:
                for (int row = 0; row < Rows; row++)
                {
                    _active.GetViewportLine(row).ReplaceCells(0, Columns, CellData.Blank(EraseStyle), respectProtected: true);
                }
                MarkDirty(0, Rows - 1);
                break;
        }
    }

    private void SelectiveEraseInLine(int mode)
    {
        int cursorColumn = _active.LogicalCursorX;
        switch (mode)
        {
            case 0: _active.CursorLine.ReplaceCells(cursorColumn, Columns, CellData.Blank(EraseStyle), respectProtected: true); break;
            case 1: _active.CursorLine.ReplaceCells(0, cursorColumn + 1, CellData.Blank(EraseStyle), respectProtected: true); break;
            case 2: _active.CursorLine.ReplaceCells(0, Columns, CellData.Blank(EraseStyle), respectProtected: true); break;
        }
        MarkDirty(_active.CursorY);
    }

    private void SelectGraphicRendition(CsiParameters parameters)
    {
        ReadOnlySpan<int> values = parameters.Values.AsSpan();
        if (values.Length == 0)
        {
            ResetStylePreservingHyperlink();
            return;
        }

        for (int i = 0; i < values.Length; i++)
        {
            int value = values[i];
            ImmutableArray<int> subParameters = parameters.GetSubParameters(i);
            switch (value)
            {
                case 0: ResetStylePreservingHyperlink(); break;
                case 1: AddAttribute(CellAttributes.Bold); break;
                case 2: AddAttribute(CellAttributes.Dim); break;
                case 3: AddAttribute(CellAttributes.Italic); break;
                case 4:
                    ApplyUnderlineStyle(subParameters);
                    break;
                case 5: AddAttribute(CellAttributes.Blink); break;
                case 7: AddAttribute(CellAttributes.Inverse); break;
                case 8: AddAttribute(CellAttributes.Invisible); break;
                case 9: AddAttribute(CellAttributes.Strikethrough); break;
                case 21:
                    AddAttribute(CellAttributes.Underline);
                    _style = _style with { UnderlineStyle = TerminalUnderlineStyle.Double };
                    InvalidateSharedExtendedAttributes();
                    break;
                case 22: RemoveAttribute(CellAttributes.Bold | CellAttributes.Dim); break;
                case 221: RemoveAttribute(CellAttributes.Bold); break;
                case 222: RemoveAttribute(CellAttributes.Dim); break;
                case 23: RemoveAttribute(CellAttributes.Italic); break;
                case 24:
                    RemoveAttribute(CellAttributes.Underline);
                    _style = _style with { UnderlineStyle = TerminalUnderlineStyle.None };
                    InvalidateSharedExtendedAttributes();
                    break;
                case 25: RemoveAttribute(CellAttributes.Blink); break;
                case 27: RemoveAttribute(CellAttributes.Inverse); break;
                case 28: RemoveAttribute(CellAttributes.Invisible); break;
                case 29: RemoveAttribute(CellAttributes.Strikethrough); break;
                case >= 30 and <= 37: _style = _style with { Foreground = TerminalColor.Palette(value - 30) }; break;
                case 38:
                    i += ApplyExtendedColor(parameters, i, foreground: true, underline: false);
                    break;
                case 39: _style = _style with { Foreground = TerminalColor.Default }; break;
                case >= 40 and <= 47: _style = _style with { Background = TerminalColor.Palette(value - 40) }; break;
                case 48:
                    i += ApplyExtendedColor(parameters, i, foreground: false, underline: false);
                    break;
                case 49: _style = _style with { Background = TerminalColor.Default }; break;
                case 53: AddAttribute(CellAttributes.Overline); break;
                case 55: RemoveAttribute(CellAttributes.Overline); break;
                case 58:
                    i += ApplyExtendedColor(parameters, i, foreground: false, underline: true);
                    break;
                case 59:
                    _style = _style with { UnderlineColor = TerminalColor.Default };
                    InvalidateSharedExtendedAttributes();
                    break;
                case >= 90 and <= 97: _style = _style with { Foreground = TerminalColor.Palette(value - 90 + 8) }; break;
                case >= 100 and <= 107: _style = _style with { Background = TerminalColor.Palette(value - 100 + 8) }; break;
            }
        }
    }

    private void ApplyUnderlineStyle(ImmutableArray<int> subParameters)
    {
        TerminalUnderlineStyle style = subParameters.IsEmpty || subParameters[0] == 1
            ? TerminalUnderlineStyle.Single
            : subParameters[0] switch
            {
                0 => TerminalUnderlineStyle.None,
                2 => TerminalUnderlineStyle.Double,
                3 => TerminalUnderlineStyle.Curly,
                4 => TerminalUnderlineStyle.Dotted,
                5 => TerminalUnderlineStyle.Dashed,
                _ => TerminalUnderlineStyle.Single
            };

        if (style == TerminalUnderlineStyle.None)
        {
            RemoveAttribute(CellAttributes.Underline);
        }
        else
        {
            AddAttribute(CellAttributes.Underline);
        }
        _style = _style with { UnderlineStyle = style };
        InvalidateSharedExtendedAttributes();
    }

    private int ApplyExtendedColor(CsiParameters parameters, int position, bool foreground, bool underline)
    {
        // Normalize both semicolon and colon forms to xterm.js' six slots:
        // [target, color mode, color-space placeholder, component 1, component 2, component 3].
        int[] values = [0, 0, -1, 0, 0, 0];
        int colorSpaceOffset = 0;
        int advance = 0;

        do
        {
            values[advance + colorSpaceOffset] = parameters.Values[position + advance];
            if (parameters.HasSubParameters(position + advance))
            {
                ImmutableArray<int> subParameters = parameters.GetSubParameters(position + advance);
                int subParameterIndex = 0;
                do
                {
                    if (values[1] == 5)
                    {
                        colorSpaceOffset = 1;
                    }
                    values[advance + subParameterIndex + 1 + colorSpaceOffset] = subParameters[subParameterIndex];
                }
                while (++subParameterIndex < subParameters.Length &&
                       subParameterIndex + advance + 1 + colorSpaceOffset < values.Length);
                break;
            }

            if ((values[1] == 5 && advance + colorSpaceOffset >= 2) ||
                (values[1] == 2 && advance + colorSpaceOffset >= 5))
            {
                break;
            }
            if (values[1] != 0)
            {
                colorSpaceOffset = 1;
            }
        }
        while (++advance + position < parameters.Values.Length &&
               advance + colorSpaceOffset < values.Length);

        for (int index = 2; index < values.Length; index++)
        {
            if (values[index] == -1)
            {
                values[index] = 0;
            }
        }

        switch (values[1])
        {
            case 2:
                SetColor(
                    TerminalColor.Rgb(
                        (byte)Math.Clamp(values[3], 0, 255),
                        (byte)Math.Clamp(values[4], 0, 255),
                        (byte)Math.Clamp(values[5], 0, 255)),
                    foreground,
                    underline);
                break;
            case 5:
                SetColor(TerminalColor.Palette(Math.Clamp(values[3], 0, 255)), foreground, underline);
                break;
        }
        return advance;
    }

    private void SetColor(TerminalColor color, bool foreground, bool underline)
    {
        if (underline)
        {
            _style = _style with { UnderlineColor = color };
            InvalidateSharedExtendedAttributes();
        }
        else if (foreground)
        {
            _style = _style with { Foreground = color };
        }
        else
        {
            _style = _style with { Background = color };
        }
    }

    private void SetModes(char? prefix, CsiParameters parameters, bool enabled)
    {
        ReadOnlySpan<int> values = parameters.Values.AsSpan();
        foreach (int value in values)
        {
            if (prefix == '?')
            {
                switch (value)
                {
                    case 2 when enabled:
                        for (int level = 0; level < 4; level++)
                        {
                            _charset.SetGCharset(level, null);
                        }
                        break;
                    case 3 when _options.WindowOptions.SetWindowLines:
                        Resize(enabled ? 132 : 80, Rows);
                        break;
                    case 1: _modes.ApplicationCursorKeys = enabled; break;
                    case 6: _modes.Origin = enabled; SetCursorPosition(new CsiParameters([])); break;
                    case 7: _modes.Wraparound = enabled; break;
                    case 9: _modes.MouseTracking = enabled ? TerminalMouseTrackingMode.X10 : TerminalMouseTrackingMode.None; break;
                    case 25: _modes.ShowCursor = enabled; break;
                    case 45: _modes.ReverseWraparound = enabled; break;
                    case 66: _modes.ApplicationKeypad = enabled; break;
                    case 47:
                    case 1047: SwitchAlternateBuffer(enabled, clear: false); break;
                    case 1048: if (enabled) SaveCursor(); else RestoreCursor(); break;
                    case 1049: SwitchAlternateBuffer(enabled, clear: true); break;
                    case 1000: _modes.MouseTracking = enabled ? TerminalMouseTrackingMode.Vt200 : TerminalMouseTrackingMode.None; break;
                    case 1002: _modes.MouseTracking = enabled ? TerminalMouseTrackingMode.Drag : TerminalMouseTrackingMode.None; break;
                    case 1003: _modes.MouseTracking = enabled ? TerminalMouseTrackingMode.Any : TerminalMouseTrackingMode.None; break;
                    case 1006:
                        _modes.MouseEncoding = enabled ? TerminalMouseEncodingMode.Sgr : TerminalMouseEncodingMode.Default;
                        _mouse.ActiveEncoding = enabled ? "SGR" : "DEFAULT";
                        break;
                    case 1016:
                        _modes.MouseEncoding = enabled ? TerminalMouseEncodingMode.SgrPixels : TerminalMouseEncodingMode.Default;
                        _mouse.ActiveEncoding = enabled ? "SGR_PIXELS" : "DEFAULT";
                        break;
                    case 1004: _modes.SendFocus = enabled; break;
                    case 2004: _modes.BracketedPaste = enabled; break;
                    case 2026: _modes.SynchronizedOutput = enabled; break;
                    case 2031 when _options.ColorSchemeQuery: _modes.ColorSchemeUpdates = enabled; break;
                    case 9001 when _options.EnableWin32InputMode: _modes.Win32InputMode = enabled; break;
                }
                if (value is 9 or 1000 or 1002 or 1003)
                {
                    _mouse.ActiveProtocol = !enabled
                        ? "NONE"
                        : value switch
                        {
                            9 => "X10",
                            1000 => "VT200",
                            1002 => "DRAG",
                            _ => "ANY"
                        };
                }
            }
            else
            {
                switch (value)
                {
                    case 4: _modes.Insert = enabled; break;
                    case 20:
                        _options = _options.Apply(new TerminalOptionsUpdate { ConvertEol = enabled });
                        break;
                }
            }
        }
    }

    private void SwitchAlternateBuffer(bool enabled, bool clear)
    {
        if (enabled)
        {
            if (_active == _alternate)
            {
                return;
            }
            if (clear)
            {
                SaveCursor();
            }
            ResetAlternateBufferPreservingSavedState();
            _modes.KittyMainFlags = _modes.KittyKeyboardFlags;
            _modes.KittyKeyboardFlags = _modes.KittyAlternateFlags;
            _alternate.CursorX = _normal.CursorX;
            _alternate.CursorY = _normal.CursorY;
            _alternate.WrapPending = _normal.WrapPending;
            _active = _alternate;
        }
        else
        {
            if (_active == _normal)
            {
                return;
            }
            _normal.CursorX = _alternate.CursorX;
            _normal.CursorY = _alternate.CursorY;
            _normal.WrapPending = _alternate.WrapPending;
            ResetAlternateBufferPreservingSavedState();
            _active = _normal;
            _modes.KittyAlternateFlags = _modes.KittyKeyboardFlags;
            _modes.KittyKeyboardFlags = _modes.KittyMainFlags;
            if (clear)
            {
                RestoreCursor();
            }
        }
        MarkDirty(0, Rows - 1);
        CursorMoved();
        ScrollChanged();
    }

    private void ResetAlternateBufferPreservingSavedState()
    {
        int savedCursorX = _alternate.SavedCursorX;
        int savedCursorY = _alternate.SavedCursorY;
        CellStyle savedStyle = _alternate.SavedStyle;
        bool savedOrigin = _alternate.SavedOriginMode;
        bool savedWraparound = _alternate.SavedWraparoundMode;
        CharsetState? savedCharset = _alternate.SavedCharsetState;
        _alternate.Reset(EraseStyle);
        _alternate.SavedCursorX = savedCursorX;
        _alternate.SavedCursorY = savedCursorY;
        _alternate.SavedStyle = savedStyle;
        _alternate.SavedOriginMode = savedOrigin;
        _alternate.SavedWraparoundMode = savedWraparound;
        _alternate.SavedCharsetState = savedCharset;
    }

    private void DeviceStatusReport(char? prefix, CsiParameters parameters)
    {
        int value = parameters.Values.Length == 0 ? 0 : parameters.Values[0];
        if (prefix == '?')
        {
            if (value == 6)
            {
                EmitData($"\x1b[?{_active.CursorY + 1};{_active.LogicalCursorX + 1}R");
            }
        }
        else if (value == 5)
        {
            EmitData("\x1b[0n");
        }
        else if (value == 6)
        {
            EmitData($"\x1b[{_active.CursorY + 1};{_active.LogicalCursorX + 1}R");
        }
    }

    private void RequestMode(char? prefix, CsiParameters parameters)
    {
        int mode = parameters.Values.Length == 0 ? 0 : parameters.Values[0];
        int status;
        if (prefix is null)
        {
            status = mode switch
            {
                2 => 4,
                4 => _modes.Insert ? 1 : 2,
                12 => 3,
                20 => _options.ConvertEol ? 1 : 2,
                _ => 0
            };
            EmitData($"\x1b[{mode};{status}$y");
            return;
        }

        if (prefix != '?')
        {
            EmitData($"\x1b[{prefix}{mode};0$y");
            return;
        }

        status = mode switch
        {
            1 => BoolMode(_modes.ApplicationCursorKeys),
            3 => 0,
            6 => BoolMode(_modes.Origin),
            7 => BoolMode(_modes.Wraparound),
            8 => 3,
            9 => BoolMode(_modes.MouseTracking == TerminalMouseTrackingMode.X10),
            12 => 2,
            25 => BoolMode(_modes.ShowCursor),
            45 => BoolMode(_modes.ReverseWraparound),
            47 or 1047 or 1049 => BoolMode(_active == _alternate),
            66 => BoolMode(_modes.ApplicationKeypad),
            67 or 1005 or 1015 => 4,
            1000 => BoolMode(_modes.MouseTracking == TerminalMouseTrackingMode.Vt200),
            1002 => BoolMode(_modes.MouseTracking == TerminalMouseTrackingMode.Drag),
            1003 => BoolMode(_modes.MouseTracking == TerminalMouseTrackingMode.Any),
            1004 => BoolMode(_modes.SendFocus),
            1006 => BoolMode(_modes.MouseEncoding == TerminalMouseEncodingMode.Sgr),
            1016 => BoolMode(_modes.MouseEncoding == TerminalMouseEncodingMode.SgrPixels),
            1048 => 1,
            2004 => BoolMode(_modes.BracketedPaste),
            2026 => BoolMode(_modes.SynchronizedOutput),
            9001 => _options.EnableWin32InputMode ? BoolMode(_modes.Win32InputMode) : 0,
            _ => 0
        };
        EmitData($"\x1b[?{mode};{status}$y");
    }

    private void RequestStatusString(string data)
    {
        string response = data switch
        {
            "\"q" => $"1$r{(_style.IsProtected ? 1 : 0)}\"q",
            "\"p" => "1$r61;1\"p",
            "r" => $"1$r{_active.ScrollTop + 1};{_active.ScrollBottom + 1}r",
            "m" => "1$r0m",
            " q" => "1$r2 q",
            _ => "0$r"
        };
        EmitData($"\x1bP{response}\x1b\\");
    }

    private static int BoolMode(bool value) => value ? 1 : 2;

    private void SetKittyKeyboard(CsiParameters parameters)
    {
        if (!_options.EnableKittyKeyboard)
        {
            return;
        }
        TerminalKittyKeyboardFlags flags = (TerminalKittyKeyboardFlags)(byte)(parameters.Values.Length == 0 ? 0 : parameters.Values[0]);
        int mode = parameters.Values.Length > 1 && parameters.Values[1] != 0 ? parameters.Values[1] : 1;
        _modes.KittyKeyboardFlags = mode switch
        {
            1 => flags,
            2 => _modes.KittyKeyboardFlags | flags,
            3 => _modes.KittyKeyboardFlags & ~flags,
            _ => _modes.KittyKeyboardFlags
        };
    }

    private void QueryKittyKeyboard()
    {
        if (_options.EnableKittyKeyboard)
        {
            EmitData($"\x1b[?{(byte)_modes.KittyKeyboardFlags}u");
        }
    }

    private void PushKittyKeyboard(CsiParameters parameters)
    {
        if (!_options.EnableKittyKeyboard)
        {
            return;
        }
        Stack<TerminalKittyKeyboardFlags> stack = _active == _alternate
            ? _modes.KittyAlternateStack
            : _modes.KittyMainStack;
        if (stack.Count >= 16)
        {
            TerminalKittyKeyboardFlags[] values = stack.Reverse().Skip(1).ToArray();
            stack.Clear();
            foreach (TerminalKittyKeyboardFlags value in values)
            {
                stack.Push(value);
            }
        }
        stack.Push(_modes.KittyKeyboardFlags);
        _modes.KittyKeyboardFlags = (TerminalKittyKeyboardFlags)(byte)(parameters.Values.Length == 0 ? 0 : parameters.Values[0]);
    }

    private void PopKittyKeyboard(CsiParameters parameters)
    {
        if (!_options.EnableKittyKeyboard)
        {
            return;
        }
        int count = Math.Max(1, parameters.Values.Length == 0 || parameters.Values[0] == 0 ? 1 : parameters.Values[0]);
        Stack<TerminalKittyKeyboardFlags> stack = _active == _alternate
            ? _modes.KittyAlternateStack
            : _modes.KittyMainStack;
        for (int index = 0; index < count && stack.Count > 0; index++)
        {
            _modes.KittyKeyboardFlags = stack.Pop();
        }
        if (stack.Count == 0)
        {
            _modes.KittyKeyboardFlags = TerminalKittyKeyboardFlags.None;
        }
    }

    private void SaveCursor()
    {
        _active.SavedCursorX = _active.CursorX;
        _active.SavedCursorY = _active.CursorY;
        _active.SavedStyle = _style;
        _active.SavedOriginMode = _modes.Origin;
        _active.SavedWraparoundMode = _modes.Wraparound;
        _active.SavedCharsetState = _charset.CaptureState();
    }

    private void RestoreCursor()
    {
        _active.CursorX = Math.Clamp(_active.SavedCursorX, 0, Columns - 1);
        _active.CursorY = Math.Clamp(_active.SavedCursorY, 0, Rows - 1);
        _active.WrapPending = false;
        _style = _active.SavedStyle;
        InvalidateSharedExtendedAttributes();
        _modes.Origin = _active.SavedOriginMode;
        _modes.Wraparound = _active.SavedWraparoundMode;
        if (_active.SavedCharsetState is CharsetState charsetState)
        {
            _charset.RestoreState(charsetState);
        }
        CursorMoved();
    }

    private void SoftReset()
    {
        _modes = new ModeState();
        _mouse.Reset();
        _charset.Reset();
        _style = CellStyle.Default;
        InvalidateSharedExtendedAttributes();
        _active.ScrollTop = 0;
        _active.ScrollBottom = Rows - 1;
        _active.SavedCursorX = 0;
        _active.SavedCursorY = 0;
        _active.SavedStyle = CellStyle.Default;
        _active.SavedOriginMode = false;
        _active.SavedWraparoundMode = true;
        _active.SavedCharsetState = _charset.CaptureState();
        _active.WrapPending = false;
        _lastPrintedRune = null;
        _lastPrintedGrapheme = null;
    }

    private void ResetStylePreservingHyperlink()
    {
        int hyperlinkId = _style.HyperlinkId;
        _style = CellStyle.Default with { HyperlinkId = hyperlinkId };
        InvalidateSharedExtendedAttributes();
    }

    private CellData CreateCurrentCell(Rune rune, byte width)
    {
        CellStyle style = GetPersistedStyle();
        return new CellData
        {
            CodePoint = rune.Value,
            Width = width,
            Style = style,
            Extended = GetSharedExtendedAttributes(style)
        };
    }

    private CellData CreateCurrentBlankCell(byte width)
    {
        CellStyle style = GetPersistedStyle();
        return new CellData
        {
            CodePoint = 0,
            Width = width,
            Style = style,
            Extended = GetSharedExtendedAttributes(style)
        };
    }

    private CellStyle GetPersistedStyle() => _style.UnderlineStyle == TerminalUnderlineStyle.None
        ? _style with { UnderlineColor = TerminalColor.Default }
        : _style;

    private ExtendedCellAttributes? GetSharedExtendedAttributes(CellStyle style)
    {
        if (style.UnderlineStyle == TerminalUnderlineStyle.None && style.HyperlinkId == 0)
        {
            return null;
        }
        if (!_hasSharedExtendedAttributes ||
            _sharedUnderlineColor != style.UnderlineColor ||
            _sharedUnderlineStyle != style.UnderlineStyle ||
            _sharedHyperlinkId != style.HyperlinkId)
        {
            _sharedUnderlineColor = style.UnderlineColor;
            _sharedUnderlineStyle = style.UnderlineStyle;
            _sharedHyperlinkId = style.HyperlinkId;
            _sharedExtendedAttributes = new ExtendedCellAttributes
            {
                UnderlineColor = style.UnderlineColor,
                UnderlineStyle = style.UnderlineStyle,
                UrlId = style.HyperlinkId
            };
            _hasSharedExtendedAttributes = true;
        }
        return _sharedExtendedAttributes;
    }

    private void InvalidateSharedExtendedAttributes()
    {
        _hasSharedExtendedAttributes = false;
        _sharedExtendedAttributes = null;
    }

    private void RepeatLastCharacter(int count)
    {
        if (_parser.PrecedingJoinState == 0 || string.IsNullOrEmpty(_lastPrintedGrapheme))
        {
            return;
        }
        string grapheme = _lastPrintedGrapheme;
        for (int i = 0; i < count; i++)
        {
            foreach (Rune rune in grapheme.EnumerateRunes())
            {
                PrintCore(rune);
            }
        }
    }

    private bool AppendToPreviousCell(Rune rune)
    {
        int row = _active.CursorY;
        int column = _active.WrapPending ? _active.CursorX : _active.CursorX - 1;
        if (column < 0 && row > 0)
        {
            row--;
            column = Columns - 1;
        }
        if (column < 0)
        {
            return false;
        }

        BufferLine line = _active.GetViewportLine(row);
        while (column > 0 && line[column].Width == 0)
        {
            column--;
        }
        line.AppendCombining(column, rune);
        return true;
    }

    private void SetHyperlink(string data)
    {
        int separator = data.IndexOf(';');
        if (separator < 0)
        {
            return;
        }

        string parameters = data[..separator].Trim();
        string uri = data[(separator + 1)..];
        if (uri.Length == 0)
        {
            if (parameters.Length == 0)
            {
                _style = _style with { HyperlinkId = 0 };
                InvalidateSharedExtendedAttributes();
            }
            return;
        }

        string? id = null;
        foreach (string parameter in parameters.Split(':'))
        {
            if (parameter.StartsWith("id=", StringComparison.Ordinal))
            {
                string value = parameter[3..];
                id = value.Length == 0 ? null : value;
                break;
            }
        }
        int linkId = _oscLinks.RegisterLink(new OscLinkData(uri, id));
        _style = _style with { HyperlinkId = linkId };
        InvalidateSharedExtendedAttributes();
    }

    private void SetOrReportIndexedColor(string data)
    {
        string[] slots = data.Split(';');
        var requests = new List<TerminalColorRequest>();
        for (int index = 0; index + 1 < slots.Length; index += 2)
        {
            if (!int.TryParse(slots[index], out int colorIndex) || colorIndex is < 0 or >= 256)
            {
                continue;
            }

            string specification = slots[index + 1];
            if (specification == "?")
            {
                requests.Add(new TerminalColorRequest(TerminalColorRequestType.Report, colorIndex));
                continue;
            }
            if (XParseColor.ParseColor(specification) is RgbColor color)
            {
                requests.Add(new TerminalColorRequest(
                    TerminalColorRequestType.Set,
                    colorIndex,
                    TerminalColor.Rgb(color.Red, color.Green, color.Blue)));
            }
        }
        EmitColorRequests(requests);
    }

    private void SetOrReportSpecialColor(string data, int offset)
    {
        string[] slots = data.Split(';');
        for (int index = 0; index < slots.Length && offset < 3; index++, offset++)
        {
            int colorIndex = (int)TerminalSpecialColorIndex.Foreground + offset;
            if (slots[index] == "?")
            {
                EmitColorRequests(new TerminalColorRequest(TerminalColorRequestType.Report, colorIndex));
                continue;
            }
            if (XParseColor.ParseColor(slots[index]) is RgbColor color)
            {
                EmitColorRequests(new TerminalColorRequest(
                    TerminalColorRequestType.Set,
                    colorIndex,
                    TerminalColor.Rgb(color.Red, color.Green, color.Blue)));
            }
        }
    }

    private void RestoreIndexedColor(string data)
    {
        if (data.Length == 0)
        {
            EmitColorRequests(new TerminalColorRequest(TerminalColorRequestType.Restore));
            return;
        }

        var requests = new List<TerminalColorRequest>();
        foreach (string slot in data.Split(';'))
        {
            if (int.TryParse(slot, out int colorIndex) && colorIndex is >= 0 and < 256)
            {
                requests.Add(new TerminalColorRequest(TerminalColorRequestType.Restore, colorIndex));
            }
        }
        EmitColorRequests(requests);
    }

    private void EmitColorRequests(IEnumerable<TerminalColorRequest> requests)
    {
        TerminalColorRequest[] values = requests.ToArray();
        if (values.Length != 0)
        {
            _events.Add(new EngineEvent(EngineEventKind.ColorRequest, ColorRequests: values));
        }
    }

    private void EmitColorRequests(params TerminalColorRequest[] requests)
    {
        if (requests.Length != 0)
        {
            _events.Add(new EngineEvent(EngineEventKind.ColorRequest, ColorRequests: requests));
        }
    }

    private void AddAttribute(CellAttributes attributes) =>
        _style = _style with { Attributes = _style.Attributes | attributes };

    private void RemoveAttribute(CellAttributes attributes) =>
        _style = _style with { Attributes = _style.Attributes & ~attributes };

    private void EmitData(string data) => _events.Add(new EngineEvent(EngineEventKind.Data, data));

    private void CursorMoved() => _events.Add(new EngineEvent(EngineEventKind.CursorMoved));

    private void ScrollChanged() => _events.Add(new EngineEvent(EngineEventKind.Scroll, First: _active.YDisp));

    private void MarkDirty(int row) => MarkDirty(row, row);

    private void MarkDirty(int start, int end)
    {
        _dirtyStart = Math.Min(_dirtyStart, Math.Clamp(start, 0, Rows - 1));
        _dirtyEnd = Math.Max(_dirtyEnd, Math.Clamp(end, 0, Rows - 1));
    }

    private bool[] CreateTabStops(int columns)
    {
        var result = new bool[columns];
        for (int i = 0; i < columns; i += _options.TabStopWidth)
        {
            result[i] = true;
        }
        return result;
    }
}

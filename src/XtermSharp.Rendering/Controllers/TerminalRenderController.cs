using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace XtermSharp.Rendering.Controllers;

public sealed class TerminalRenderController : IDisposable
{
    private readonly Terminal _terminal;
    private readonly ITerminalFontMetricsProvider _metricsProvider;
    private readonly object _gate = new();
    private readonly SemaphoreSlim _prepareGate = new(1, 1);
    private readonly Dictionary<int, RowCache> _rowCache = [];
    private readonly CancellationTokenSource _disposeCancellation = new();
    private TerminalRenderOptions _options;
    private TerminalTheme _baseTheme;
    private TerminalTheme _theme;
    private TerminalSelection? _selection;
    private TerminalLinkRange? _hoveredLink;
    private TerminalRenderFrame? _currentFrame;
    private CursorOverlayCache? _cursorOverlayCache;
    private int _pendingStart = int.MaxValue;
    private int _pendingEnd = -1;
    private long _pendingRevision;
    private int _configurationVersion;
    private int _blinkVersion;
    private int _invalidationVersion;
    private bool _fullInvalidation = true;
    private bool _focused;
    private bool _cursorPhase = true;
    private bool _blinkPhase = true;
    private string _preeditText = string.Empty;
    private DateTimeOffset? _synchronizedSince;
    private Task? _synchronizedTimeoutTask;
    private int _disposed;

    public TerminalRenderController(
        Terminal terminal,
        ITerminalFontMetricsProvider metricsProvider,
        TerminalRenderOptions? options = null,
        TerminalTheme? theme = null)
    {
        _terminal = terminal ?? throw new ArgumentNullException(nameof(terminal));
        _metricsProvider = metricsProvider ?? throw new ArgumentNullException(nameof(metricsProvider));
        _options = options ?? new TerminalRenderOptions();
        _baseTheme = theme ?? TerminalTheme.Default;
        _theme = _baseTheme;
        _selection = ToRenderingSelection(_terminal.Selection);
        _terminal.RenderRequested += OnRenderRequested;
        _terminal.Resized += OnFullInvalidation;
        _terminal.Scrolled += OnFullInvalidation;
        _terminal.OptionsChanged += OnOptionsChanged;
        _terminal.ColorRequested += OnColorRequested;
        _terminal.SelectionChanged += OnSelectionChanged;
        _terminal.DecorationsChanged += OnDecorationsChanged;
    }

    public Terminal Terminal => _terminal;
    public TerminalRenderFrame? CurrentFrame => Volatile.Read(ref _currentFrame);
    public TerminalSelection? Selection
    {
        get
        {
            lock (_gate)
            {
                return _selection;
            }
        }
    }
    public TerminalLinkRange? HoveredLink
    {
        get
        {
            lock (_gate)
            {
                return _hoveredLink;
            }
        }
    }

    public TerminalRenderOptions Options
    {
        get
        {
            lock (_gate)
            {
                return _options;
            }
        }
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            lock (_gate)
            {
                _options = value;
                InvalidateConfigurationLocked();
            }
            RaiseInvalidated();
        }
    }

    public TerminalTheme Theme
    {
        get
        {
            lock (_gate)
            {
                return _baseTheme;
            }
        }
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            lock (_gate)
            {
                _baseTheme = value;
                _theme = value;
                InvalidateConfigurationLocked();
            }
            RaiseInvalidated();
        }
    }

    public bool IsFocused
    {
        get
        {
            lock (_gate)
            {
                return _focused;
            }
        }
        set
        {
            bool changed;
            lock (_gate)
            {
                changed = _focused != value;
                _focused = value;
            }
            if (changed)
            {
                RaiseInvalidated();
            }
        }
    }

    public event EventHandler? Invalidated;

    public void SetSelection(TerminalSelection? selection)
    {
        _terminal.SetSelection(ToCoreSelection(selection));
    }

    public void SetHoveredLink(TerminalLinkRange? range)
    {
        bool changed;
        lock (_gate)
        {
            changed = _hoveredLink != range;
            if (changed)
            {
                _hoveredLink = range;
                _fullInvalidation = true;
                _invalidationVersion++;
            }
        }
        if (changed)
        {
            RaiseInvalidated();
        }
    }

    public void SetBlinkPhases(bool cursorVisible, bool textVisible)
    {
        bool changed;
        lock (_gate)
        {
            changed = _cursorPhase != cursorVisible || _blinkPhase != textVisible;
            bool textChanged = _blinkPhase != textVisible;
            _cursorPhase = cursorVisible;
            _blinkPhase = textVisible;
            if (textChanged)
            {
                _blinkVersion++;
            }
        }
        if (changed)
        {
            RaiseInvalidated();
        }
    }

    public void SetPreeditText(string? text)
    {
        text ??= string.Empty;
        bool changed;
        lock (_gate)
        {
            changed = !string.Equals(_preeditText, text, StringComparison.Ordinal);
            _preeditText = text;
        }
        if (changed)
        {
            RaiseInvalidated();
        }
    }

    public async ValueTask<TerminalRenderFrame> PrepareFrameAsync(
        TerminalViewport viewport,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _disposeCancellation.Token);
        await _prepareGate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            TerminalSnapshot snapshot = await _terminal.GetSnapshotAsync(
                SnapshotScope.Viewport,
                linked.Token).ConfigureAwait(false);
            TerminalRenderOptions options;
            TerminalTheme theme;
            IReadOnlyList<TerminalDecoration> terminalDecorations = _terminal.Decorations;
            TerminalSelection? selection;
            TerminalLinkRange? hoveredLink;
            bool focused;
            bool cursorPhase;
            bool blinkPhase;
            string preeditText;
            bool full;
            int version;
            int blinkVersion;
            int pendingStart;
            int pendingEnd;
            int invalidationVersion;
            TerminalRenderFrame? previousFrame;
            lock (_gate)
            {
                options = _options;
                theme = _theme;
                selection = _selection;
                hoveredLink = _hoveredLink;
                focused = _focused;
                cursorPhase = _cursorPhase;
                blinkPhase = _blinkPhase;
                preeditText = _preeditText;
                previousFrame = CurrentFrame;
                full = _fullInvalidation || previousFrame?.Viewport != viewport;
                version = _configurationVersion;
                blinkVersion = _blinkVersion;
                pendingStart = _pendingStart;
                pendingEnd = _pendingEnd;
                invalidationVersion = _invalidationVersion;
            }

            TerminalRenderConfiguration configuration = options.Resolve(_terminal.Options, viewport.RenderScale);
            TerminalFontMetrics metrics = _metricsProvider.MeasureFont(configuration);

            if (snapshot.Modes.SynchronizedOutput && CurrentFrame is not null && !SynchronizedTimeoutElapsed(options))
            {
                ScheduleSynchronizedTimeout(options.SynchronizedOutputTimeout);
                return CurrentFrame;
            }
            lock (_gate)
            {
                _synchronizedSince = null;
            }

            var rows = ImmutableArray.CreateBuilder<TerminalDisplayRow>(snapshot.Rows);
            int changedStart = int.MaxValue;
            int changedEnd = -1;
            for (int row = 0; row < snapshot.Rows; row++)
            {
                TerminalLineSnapshot line = snapshot.ActiveBuffer.Lines[row];
                _rowCache.TryGetValue(row, out RowCache? cached);
                bool pendingDamage = row >= pendingStart && row <= pendingEnd;
                if (full || cached is null ||
                    cached.ConfigurationVersion != version ||
                    cached.HasBlinkingText && cached.BlinkVersion != blinkVersion ||
                    pendingDamage && !ReferenceEquals(cached.Line, line))
                {
                    TerminalDisplayRow content = BuildContentRow(
                        row,
                        line,
                        snapshot,
                        viewport,
                        metrics,
                        configuration,
                        theme,
                        terminalDecorations,
                        selection,
                        hoveredLink,
                        blinkPhase,
                        out bool hasBlinkingText);
                    cached = new RowCache(line, version, blinkVersion, content, hasBlinkingText);
                    _rowCache[row] = cached;
                }
                TerminalDisplayRow displayRow = GetDisplayRow(
                    cached.Row,
                    row,
                    snapshot,
                    viewport,
                    metrics,
                    configuration,
                    theme,
                    focused,
                    cursorPhase,
                    preeditText);
                rows.Add(displayRow);
                if (full || previousFrame is null || row >= previousFrame.DisplayList.Rows.Length ||
                    !ReferenceEquals(previousFrame.DisplayList.Rows[row], displayRow))
                {
                    changedStart = Math.Min(changedStart, row);
                    changedEnd = Math.Max(changedEnd, row);
                }
            }

            if (previousFrame is not null && previousFrame.Rows > snapshot.Rows)
            {
                for (int row = snapshot.Rows; row < previousFrame.Rows; row++)
                {
                    _rowCache.Remove(row);
                }
            }
            var frame = new TerminalRenderFrame(
                snapshot.Revision,
                viewport,
                metrics,
                snapshot.Columns,
                snapshot.Rows,
                snapshot.ActiveBuffer.ViewportY,
                snapshot.ActiveBuffer.BaseY,
                new TerminalDisplayList(rows.MoveToImmutable()),
                changedEnd < changedStart
                    ? TerminalDamage.Empty
                    : new TerminalDamage(changedStart, changedEnd))
            {
                Modes = snapshot.Modes,
                CursorColumn = snapshot.ActiveBuffer.CursorX,
                CursorRow = snapshot.ActiveBuffer.BaseY + snapshot.ActiveBuffer.CursorY - snapshot.ActiveBuffer.ViewportY
            };
            if (_metricsProvider is ITerminalFramePreparer framePreparer)
            {
                framePreparer.PrepareFrame(frame);
            }
            Volatile.Write(ref _currentFrame, frame);
            lock (_gate)
            {
                if (_invalidationVersion == invalidationVersion && _pendingRevision <= snapshot.Revision)
                {
                    _fullInvalidation = false;
                }
                if (_pendingRevision <= snapshot.Revision)
                {
                    _pendingStart = int.MaxValue;
                    _pendingEnd = -1;
                    _pendingRevision = 0;
                }
            }
            return frame;
        }
        finally
        {
            _prepareGate.Release();
        }
    }

    public async ValueTask<string> GetSelectedTextAsync(CancellationToken cancellationToken = default)
    {
        TerminalSelection? selected = Selection;
        if (selected is null || selected.Value.IsEmpty)
        {
            return string.Empty;
        }
        TerminalSelection selection = selected.Value.Normalize();
        TerminalSnapshot snapshot = await _terminal.GetSnapshotAsync(
            SnapshotScope.ActiveBuffer,
            cancellationToken).ConfigureAwait(false);
        TerminalBufferSnapshot buffer = snapshot.ActiveBuffer;
        int firstLine = Math.Clamp(selection.StartLine, 0, Math.Max(0, buffer.Lines.Length - 1));
        int lastLine = Math.Clamp(selection.EndLine, firstLine, Math.Max(0, buffer.Lines.Length - 1));
        var result = new StringBuilder();
        for (int lineIndex = firstLine; lineIndex <= lastLine; lineIndex++)
        {
            TerminalLineSnapshot line = buffer.Lines[lineIndex];
            int start = selection.ColumnMode ? Math.Min(selection.StartColumn, selection.EndColumn) :
                lineIndex == firstLine ? selection.StartColumn : 0;
            int end = selection.ColumnMode ? Math.Max(selection.StartColumn, selection.EndColumn) :
                lineIndex == lastLine ? selection.EndColumn : snapshot.Columns;
            result.Append(line.TranslateToString(trimRight: true, start, end));
            if (lineIndex < lastLine && (selection.ColumnMode || !buffer.Lines[lineIndex + 1].IsWrapped))
            {
                result.Append('\n');
            }
        }
        return result.ToString();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }
        _terminal.RenderRequested -= OnRenderRequested;
        _terminal.Resized -= OnFullInvalidation;
        _terminal.Scrolled -= OnFullInvalidation;
        _terminal.OptionsChanged -= OnOptionsChanged;
        _terminal.ColorRequested -= OnColorRequested;
        _terminal.SelectionChanged -= OnSelectionChanged;
        _terminal.DecorationsChanged -= OnDecorationsChanged;
        _disposeCancellation.Cancel();
    }

    private TerminalDisplayRow BuildContentRow(
        int row,
        TerminalLineSnapshot line,
        TerminalSnapshot snapshot,
        TerminalViewport viewport,
        TerminalFontMetrics metrics,
        TerminalRenderConfiguration configuration,
        TerminalTheme theme,
        IReadOnlyList<TerminalDecoration> terminalDecorations,
        TerminalSelection? selection,
        TerminalLinkRange? hoveredLink,
        bool blinkPhase,
        out bool hasBlinkingText)
    {
        var backgrounds = ImmutableArray.CreateBuilder<TerminalDrawCommand>();
        var bottomDecorationBackgrounds = ImmutableArray.CreateBuilder<TerminalDrawCommand>();
        var selectionBackgrounds = ImmutableArray.CreateBuilder<TerminalDrawCommand>();
        var topDecorationBackgrounds = ImmutableArray.CreateBuilder<TerminalDrawCommand>();
        var text = ImmutableArray.CreateBuilder<TerminalDrawCommand>();
        var decorations = ImmutableArray.CreateBuilder<TerminalDrawCommand>();
        double y = viewport.Padding.Top + row * metrics.CellHeight;
        backgrounds.Add(new TerminalFillRectangleCommand(
            new TerminalRect(viewport.Padding.Left, y, snapshot.Columns * metrics.CellWidth, metrics.CellHeight),
            theme.Background));
        int absoluteLine = snapshot.ActiveBuffer.ViewportY + row;
        foreach (TerminalDecoration decoration in terminalDecorations)
        {
            if (decoration.Range.Line != absoluteLine ||
                !TryGetDecorationBounds(decoration.Range, snapshot.Columns, viewport, metrics, y, out TerminalRect bounds))
            {
                continue;
            }
            if (decoration.Background is TerminalColor background)
            {
                ImmutableArray<TerminalDrawCommand>.Builder target =
                    decoration.Layer == TerminalDecorationLayer.Top
                        ? topDecorationBackgrounds
                        : bottomDecorationBackgrounds;
                target.Add(new TerminalFillRectangleCommand(bounds, theme.Resolve(background, foreground: false)));
            }
            if (decoration.Border is TerminalColor border)
            {
                decorations.Add(new TerminalStrokeRectangleCommand(
                    bounds,
                    theme.Resolve(border, foreground: true),
                    Math.Max(1, configuration.RenderScale)));
            }
        }
        hasBlinkingText = false;

        bool hasBackgroundRun = false;
        int backgroundStart = 0;
        int backgroundEnd = 0;
        TerminalRgbaColor backgroundRunColor = default;
        StringBuilder? textRun = null;
        int textStart = 0;
        int textEnd = 0;
        TerminalRgbaColor textRunColor = default;
        bool textRunBold = false;
        bool textRunItalic = false;

        void FlushBackgroundRun()
        {
            if (!hasBackgroundRun)
            {
                return;
            }
            backgrounds.Add(new TerminalFillRectangleCommand(
                new TerminalRect(
                    viewport.Padding.Left + backgroundStart * metrics.CellWidth,
                    y,
                    (backgroundEnd - backgroundStart) * metrics.CellWidth,
                    metrics.CellHeight),
                backgroundRunColor));
            hasBackgroundRun = false;
        }

        void FlushTextRun()
        {
            if (textRun is null)
            {
                return;
            }
            text.Add(new TerminalTextCommand(
                new TerminalRect(
                    viewport.Padding.Left + textStart * metrics.CellWidth,
                    y,
                    (textEnd - textStart) * metrics.CellWidth,
                    metrics.CellHeight),
                textRun.ToString(),
                textRunColor,
                textRunBold,
                textRunItalic,
                configuration.RescaleOverlappingGlyphs)
            {
                CellCount = textEnd - textStart
            });
            textRun = null;
        }

        for (int column = 0; column < line.Cells.Length; column++)
        {
            TerminalCellSnapshot cell = line.Cells[column];
            if (cell.Width == 0)
            {
                continue;
            }
            int cellWidth = Math.Max(1, (int)cell.Width);
            TerminalRgbaColor foreground = ResolveForeground(cell, theme, configuration);
            TerminalRgbaColor background = theme.Resolve(cell.Background, foreground: false);
            if (cell.Attributes.HasFlag(CellAttributes.Inverse))
            {
                (foreground, background) = (background, foreground);
            }
            if (cell.Attributes.HasFlag(CellAttributes.Dim))
            {
                foreground = foreground.Blend(background, 0.5);
            }
            var bounds = new TerminalRect(
                viewport.Padding.Left + column * metrics.CellWidth,
                y,
                metrics.CellWidth * cellWidth,
                metrics.CellHeight);
            bool selected = selection?.Contains(column, absoluteLine) == true;
            if (selected)
            {
                foreground = theme.SelectionForeground;
                selectionBackgrounds.Add(new TerminalFillRectangleCommand(bounds, theme.SelectionBackground));
            }
            if (background != theme.Background)
            {
                if (hasBackgroundRun && backgroundRunColor == background && backgroundEnd == column)
                {
                    backgroundEnd = column + cellWidth;
                }
                else
                {
                    FlushBackgroundRun();
                    hasBackgroundRun = true;
                    backgroundStart = column;
                    backgroundEnd = column + cellWidth;
                    backgroundRunColor = background;
                }
            }
            else
            {
                FlushBackgroundRun();
            }
            bool invisible = cell.Attributes.HasFlag(CellAttributes.Invisible);
            bool blinking = cell.Attributes.HasFlag(CellAttributes.Blink);
            hasBlinkingText |= !invisible && blinking && cell.Text.Length != 0;
            if (!invisible && (!blinking || blinkPhase) && cell.Text.Length != 0)
            {
                bool bold = cell.Attributes.HasFlag(CellAttributes.Bold);
                bool italic = cell.Attributes.HasFlag(CellAttributes.Italic);
                bool mergeableAscii = configuration.LetterSpacing == 0 &&
                    cellWidth == 1 && cell.Text.Length == 1 && cell.Text[0] is >= ' ' and <= '~';
                if (mergeableAscii)
                {
                    if (textRun is null || textEnd != column || textRunColor != foreground ||
                        textRunBold != bold || textRunItalic != italic)
                    {
                        FlushTextRun();
                        textRun = new StringBuilder();
                        textStart = column;
                        textRunColor = foreground;
                        textRunBold = bold;
                        textRunItalic = italic;
                    }
                    textRun.Append(cell.Text);
                    textEnd = column + 1;
                }
                else
                {
                    FlushTextRun();
                    text.Add(new TerminalTextCommand(
                        bounds,
                        cell.Text,
                        foreground,
                        bold,
                        italic,
                        configuration.RescaleOverlappingGlyphs));
                }
                bool linkUnderline = hoveredLink?.Contains(column + 1, absoluteLine + 1, snapshot.Columns) == true;
                AddDecorations(decorations, bounds, cell, foreground, theme, metrics, linkUnderline);
            }
            else
            {
                FlushTextRun();
            }
        }
        FlushBackgroundRun();
        FlushTextRun();

        var commands = ImmutableArray.CreateBuilder<TerminalDrawCommand>(
            backgrounds.Count + bottomDecorationBackgrounds.Count + selectionBackgrounds.Count +
            topDecorationBackgrounds.Count + text.Count + decorations.Count);
        commands.AddRange(backgrounds);
        commands.AddRange(bottomDecorationBackgrounds);
        commands.AddRange(selectionBackgrounds);
        commands.AddRange(topDecorationBackgrounds);
        commands.AddRange(text);
        commands.AddRange(decorations);
        return new TerminalDisplayRow(row, commands.MoveToImmutable());
    }

    private static bool TryGetDecorationBounds(
        TerminalDecorationRange range,
        int columns,
        TerminalViewport viewport,
        TerminalFontMetrics metrics,
        double y,
        out TerminalRect bounds)
    {
        int start = Math.Clamp(range.Column, 0, columns);
        long requestedEnd = (long)range.Column + range.Width;
        int end = (int)Math.Clamp(requestedEnd, start, columns);
        if (range.Width <= 0 || end <= start)
        {
            bounds = default;
            return false;
        }
        bounds = new TerminalRect(
            viewport.Padding.Left + start * metrics.CellWidth,
            y,
            (end - start) * metrics.CellWidth,
            metrics.CellHeight);
        return true;
    }

    private TerminalDisplayRow GetDisplayRow(
        TerminalDisplayRow contentRow,
        int rowIndex,
        TerminalSnapshot snapshot,
        TerminalViewport viewport,
        TerminalFontMetrics metrics,
        TerminalRenderConfiguration configuration,
        TerminalTheme theme,
        bool focused,
        bool cursorPhase,
        string preeditText)
    {
        int cursorRow = snapshot.ActiveBuffer.BaseY + snapshot.ActiveBuffer.CursorY - snapshot.ActiveBuffer.ViewportY;
        if (cursorRow != rowIndex)
        {
            return contentRow;
        }
        int column = Math.Clamp(snapshot.ActiveBuffer.CursorX, 0, Math.Max(0, snapshot.Columns - 1));
        CursorOverlayCache? cached = _cursorOverlayCache;
        if (cached is not null && ReferenceEquals(cached.ContentRow, contentRow) &&
            cached.Row == rowIndex && cached.Column == column && cached.Focused == focused &&
            cached.CursorPhase == cursorPhase && cached.ShowCursor == snapshot.Modes.ShowCursor &&
            cached.CursorBlink == snapshot.Modes.CursorBlink && cached.CursorStyle == snapshot.Modes.CursorStyle &&
            string.Equals(cached.PreeditText, preeditText, StringComparison.Ordinal))
        {
            return cached.DisplayRow;
        }
        TerminalDisplayRow displayRow = AddCursorOverlay(
            contentRow,
            rowIndex,
            snapshot,
            viewport,
            metrics,
            configuration,
            theme,
            focused,
            cursorPhase,
            preeditText);
        _cursorOverlayCache = new CursorOverlayCache(
            contentRow,
            rowIndex,
            column,
            focused,
            cursorPhase,
            snapshot.Modes.ShowCursor,
            snapshot.Modes.CursorBlink,
            snapshot.Modes.CursorStyle,
            preeditText,
            displayRow);
        return displayRow;
    }

    private static TerminalDisplayRow AddCursorOverlay(
        TerminalDisplayRow row,
        int rowIndex,
        TerminalSnapshot snapshot,
        TerminalViewport viewport,
        TerminalFontMetrics metrics,
        TerminalRenderConfiguration configuration,
        TerminalTheme theme,
        bool focused,
        bool cursorPhase,
        string preeditText)
    {
        int cursorRow = snapshot.ActiveBuffer.BaseY + snapshot.ActiveBuffer.CursorY - snapshot.ActiveBuffer.ViewportY;
        if (cursorRow != rowIndex)
        {
            return row;
        }
        bool blink = snapshot.Modes.CursorBlink ?? configuration.CursorBlink;
        int column = Math.Clamp(snapshot.ActiveBuffer.CursorX, 0, Math.Max(0, snapshot.Columns - 1));
        var bounds = new TerminalRect(
            viewport.Padding.Left + column * metrics.CellWidth,
            viewport.Padding.Top + rowIndex * metrics.CellHeight,
            metrics.CellWidth,
            metrics.CellHeight);
        var commands = row.Commands.ToBuilder();
        if (snapshot.Modes.ShowCursor && (!focused || !blink || cursorPhase))
        {
            TerminalCursorStyle style = snapshot.Modes.CursorStyle ?? configuration.CursorStyle;
            if (!focused)
            {
                commands.Add(new TerminalStrokeRectangleCommand(bounds, theme.Cursor, Math.Max(1, configuration.RenderScale)));
            }
            else if (style == TerminalCursorStyle.Block)
            {
                commands.Add(new TerminalFillRectangleCommand(bounds, theme.Cursor));
                TerminalCellSnapshot cell = snapshot.ActiveBuffer.Lines[rowIndex].Cells[column];
                if (cell.Width != 0 && cell.Text.Length != 0 && !cell.Attributes.HasFlag(CellAttributes.Invisible))
                {
                    commands.Add(new TerminalTextCommand(
                        bounds with { Width = metrics.CellWidth * Math.Max(1, (int)cell.Width) },
                        cell.Text,
                        theme.CursorAccent,
                        cell.Attributes.HasFlag(CellAttributes.Bold),
                        cell.Attributes.HasFlag(CellAttributes.Italic),
                        configuration.RescaleOverlappingGlyphs));
                }
            }
            else if (style == TerminalCursorStyle.Bar)
            {
                commands.Add(new TerminalFillRectangleCommand(
                    bounds with { Width = Math.Max(1, configuration.CursorWidth * configuration.RenderScale) },
                    theme.Cursor));
            }
            else
            {
                double thickness = Math.Max(1, metrics.UnderlineThickness);
                commands.Add(new TerminalFillRectangleCommand(
                    new TerminalRect(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness),
                    theme.Cursor));
            }
        }
        if (preeditText.Length != 0)
        {
            int cells = Math.Max(1, preeditText.EnumerateRunes().Count());
            TerminalRect preeditBounds = bounds with { Width = metrics.CellWidth * cells };
            commands.Add(new TerminalFillRectangleCommand(preeditBounds, theme.Background));
            commands.Add(new TerminalTextCommand(
                preeditBounds,
                preeditText,
                theme.Foreground,
                false,
                false,
                configuration.RescaleOverlappingGlyphs));
            commands.Add(new TerminalLineCommand(
                preeditBounds,
                new TerminalPoint(preeditBounds.X, preeditBounds.Bottom - metrics.UnderlineThickness),
                new TerminalPoint(preeditBounds.Right, preeditBounds.Bottom - metrics.UnderlineThickness),
                theme.Foreground,
                metrics.UnderlineThickness));
        }
        if (commands.Count == row.Commands.Length)
        {
            return row;
        }
        return new TerminalDisplayRow(row.Row, commands.ToImmutable());
    }

    private static TerminalRgbaColor ResolveForeground(
        TerminalCellSnapshot cell,
        TerminalTheme theme,
        TerminalRenderConfiguration configuration)
    {
        TerminalColor color = cell.Foreground;
        if (configuration.DrawBoldTextInBrightColors &&
            cell.Attributes.HasFlag(CellAttributes.Bold) &&
            color.Mode == TerminalColorMode.Palette && color.Value is >= 0 and < 8)
        {
            color = TerminalColor.Palette(color.Value + 8);
        }
        return theme.Resolve(color, foreground: true);
    }

    private static void AddDecorations(
        ImmutableArray<TerminalDrawCommand>.Builder commands,
        TerminalRect bounds,
        TerminalCellSnapshot cell,
        TerminalRgbaColor foreground,
        TerminalTheme theme,
        TerminalFontMetrics metrics,
        bool linkUnderline)
    {
        double thickness = Math.Max(1, metrics.UnderlineThickness);
        bool cellUnderline = cell.Attributes.HasFlag(CellAttributes.Underline);
        if (cellUnderline || linkUnderline)
        {
            TerminalRgbaColor color = !cellUnderline || cell.UnderlineColor.Mode == TerminalColorMode.Default
                ? foreground
                : theme.Resolve(cell.UnderlineColor, foreground: true);
            double y = bounds.Y + metrics.UnderlineOffset;
            commands.Add(new TerminalLineCommand(
                bounds,
                new TerminalPoint(bounds.X, y),
                new TerminalPoint(bounds.Right, y),
                color,
                thickness,
                !cellUnderline || cell.UnderlineStyle == TerminalUnderlineStyle.None
                    ? TerminalUnderlineStyle.Single
                    : cell.UnderlineStyle));
            if (cellUnderline && cell.UnderlineStyle == TerminalUnderlineStyle.Double)
            {
                commands.Add(new TerminalLineCommand(
                    bounds,
                    new TerminalPoint(bounds.X, y + thickness * 2),
                    new TerminalPoint(bounds.Right, y + thickness * 2),
                    color,
                    thickness,
                    TerminalUnderlineStyle.Double));
            }
        }
        if (cell.Attributes.HasFlag(CellAttributes.Strikethrough))
        {
            double y = bounds.Y + metrics.StrikeOffset;
            commands.Add(new TerminalLineCommand(
                bounds,
                new TerminalPoint(bounds.X, y),
                new TerminalPoint(bounds.Right, y),
                foreground,
                thickness));
        }
        if (cell.Attributes.HasFlag(CellAttributes.Overline))
        {
            commands.Add(new TerminalLineCommand(
                bounds,
                new TerminalPoint(bounds.X, bounds.Y + thickness),
                new TerminalPoint(bounds.Right, bounds.Y + thickness),
                foreground,
                thickness));
        }
    }

    private void OnRenderRequested(object? sender, TerminalRenderEventArgs args)
    {
        lock (_gate)
        {
            _pendingStart = Math.Min(_pendingStart, args.StartRow);
            _pendingEnd = Math.Max(_pendingEnd, args.EndRow);
            _pendingRevision = Math.Max(_pendingRevision, args.Revision);
        }
        RaiseInvalidated();
    }

    private void OnFullInvalidation(object? sender, TerminalEventArgs args)
    {
        lock (_gate)
        {
            _fullInvalidation = true;
            _invalidationVersion++;
            _pendingRevision = Math.Max(_pendingRevision, args.Revision);
        }
        RaiseInvalidated();
    }

    private void OnSelectionChanged(object? sender, EventArgs args)
    {
        lock (_gate)
        {
            _selection = ToRenderingSelection(_terminal.Selection);
            _fullInvalidation = true;
            _invalidationVersion++;
        }
        RaiseInvalidated();
    }

    private void OnDecorationsChanged(object? sender, EventArgs args)
    {
        lock (_gate)
        {
            _fullInvalidation = true;
            _invalidationVersion++;
        }
        RaiseInvalidated();
    }

    private void OnOptionsChanged(object? sender, TerminalOptionsChangedEventArgs args)
    {
        lock (_gate)
        {
            InvalidateConfigurationLocked();
            _pendingRevision = Math.Max(_pendingRevision, args.Revision);
        }
        RaiseInvalidated();
    }

    private void OnColorRequested(object? sender, TerminalColorRequestEventArgs args)
    {
        TerminalRenderOptions options;
        List<string>? responses = null;
        bool changed = false;
        lock (_gate)
        {
            options = _options;
            if (!options.HandleColorRequests)
            {
                return;
            }
            foreach (TerminalColorRequest request in args.Requests)
            {
                if (request.Type == TerminalColorRequestType.Set && request.Index is int setIndex && request.Color is TerminalColor color)
                {
                    _theme = _theme.WithColor(setIndex, _theme.Resolve(color, foreground: true));
                    changed = true;
                }
                else if (request.Type == TerminalColorRequestType.Restore)
                {
                    _theme = _theme.RestoreColor(request.Index, _baseTheme);
                    changed = true;
                }
                else if (request.Type == TerminalColorRequestType.Report && request.Index is int reportIndex)
                {
                    responses ??= [];
                    responses.Add(FormatColorResponse(reportIndex, _theme.GetIndexedColor(reportIndex)));
                }
            }
            if (changed)
            {
                InvalidateConfigurationLocked();
            }
        }
        if (responses is not null)
        {
            foreach (string response in responses)
            {
                _ = SendResponseAsync(response);
            }
        }
        if (changed)
        {
            RaiseInvalidated();
        }
    }

    private async Task SendResponseAsync(string response)
    {
        try
        {
            await _terminal.SendInputAsync(response, wasUserInput: false).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception exception)
        {
            _terminal.Options.Logger?.Log(TerminalLogLevel.Error, "Failed to send a terminal color response.", exception);
        }
    }

    private static string FormatColorResponse(int index, TerminalRgbaColor color)
    {
        string rgb = string.Create(
            CultureInfo.InvariantCulture,
            $"rgb:{color.Red * 257:x4}/{color.Green * 257:x4}/{color.Blue * 257:x4}");
        return index switch
        {
            >= 0 and < 256 => $"\x1b]4;{index};{rgb}\x1b\\",
            (int)TerminalSpecialColorIndex.Foreground => $"\x1b]10;{rgb}\x1b\\",
            (int)TerminalSpecialColorIndex.Background => $"\x1b]11;{rgb}\x1b\\",
            (int)TerminalSpecialColorIndex.Cursor => $"\x1b]12;{rgb}\x1b\\",
            _ => string.Empty
        };
    }

    private bool SynchronizedTimeoutElapsed(TerminalRenderOptions options)
    {
        lock (_gate)
        {
            _synchronizedSince ??= DateTimeOffset.UtcNow;
            return DateTimeOffset.UtcNow - _synchronizedSince >= options.SynchronizedOutputTimeout;
        }
    }

    private void ScheduleSynchronizedTimeout(TimeSpan timeout)
    {
        lock (_gate)
        {
            if (_synchronizedTimeoutTask is { IsCompleted: false })
            {
                return;
            }
            _synchronizedTimeoutTask = WaitForSynchronizedTimeoutAsync(timeout, _disposeCancellation.Token);
        }
    }

    private async Task WaitForSynchronizedTimeoutAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(timeout, cancellationToken).ConfigureAwait(false);
            RaiseInvalidated();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void InvalidateConfigurationLocked()
    {
        _configurationVersion++;
        _fullInvalidation = true;
        _invalidationVersion++;
    }

    private void RaiseInvalidated() => Invalidated?.Invoke(this, EventArgs.Empty);

    private static TerminalSelection? ToRenderingSelection(TerminalSelectionRange? selection) =>
        selection is TerminalSelectionRange value
            ? new TerminalSelection(
                value.StartColumn,
                value.StartLine,
                value.EndColumn,
                value.EndLine,
                value.ColumnMode)
            : null;

    private static TerminalSelectionRange? ToCoreSelection(TerminalSelection? selection) =>
        selection is TerminalSelection value
            ? new TerminalSelectionRange(
                value.StartColumn,
                value.StartLine,
                value.EndColumn,
                value.EndLine,
                value.ColumnMode)
            : null;

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(TerminalRenderController));
        }
    }
}

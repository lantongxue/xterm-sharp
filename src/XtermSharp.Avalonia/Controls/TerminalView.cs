using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Input.TextInput;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Text;
using XtermSharp.Rendering;
using XtermSharp.Rendering.Skia;

namespace XtermSharp.Avalonia;

public sealed class TerminalView : TemplatedControl
{
    public static readonly StyledProperty<Terminal?> TerminalProperty =
        AvaloniaProperty.Register<TerminalView, Terminal?>(nameof(Terminal));

    public static readonly StyledProperty<TerminalTheme> TerminalThemeProperty =
        AvaloniaProperty.Register<TerminalView, TerminalTheme>(
            nameof(TerminalTheme),
            global::XtermSharp.Rendering.TerminalTheme.Default);

    public static readonly StyledProperty<TerminalRenderOptions> RenderOptionsProperty =
        AvaloniaProperty.Register<TerminalView, TerminalRenderOptions>(
            nameof(RenderOptions),
            new TerminalRenderOptions());

    public static readonly StyledProperty<bool> ShowRenderingDebugOverlayProperty =
        AvaloniaProperty.Register<TerminalView, bool>(nameof(ShowRenderingDebugOverlay));

    public static readonly DirectProperty<TerminalView, int> ScrollValueProperty =
        AvaloniaProperty.RegisterDirect<TerminalView, int>(nameof(ScrollValue), view => view.ScrollValue);

    public static readonly DirectProperty<TerminalView, int> ScrollMaximumProperty =
        AvaloniaProperty.RegisterDirect<TerminalView, int>(nameof(ScrollMaximum), view => view.ScrollMaximum);

    public static readonly DirectProperty<TerminalView, int> ColumnsProperty =
        AvaloniaProperty.RegisterDirect<TerminalView, int>(nameof(Columns), view => view.Columns);

    public static readonly DirectProperty<TerminalView, int> RowsProperty =
        AvaloniaProperty.RegisterDirect<TerminalView, int>(nameof(Rows), view => view.Rows);

    private readonly DispatcherTimer _blinkTimer;
    private readonly TerminalTextInputMethodClient _textInputClient;
    private readonly AvaloniaKeyStateTracker _keyState = new();
    private readonly RenderingDebugMetrics _renderingDebugMetrics = new();
    private SkiaTerminalRenderBackend? _backend;
    private TerminalRenderController? _controller;
    private TerminalRenderFrame? _frame;
    private CancellationTokenSource? _prepareCancellation;
    private int _prepareScheduled;
    private int _preparing;
    private int _prepareAgain;
    private int _lastColumns;
    private int _lastRows;
    private bool _cursorPhase = true;
    private bool _blinkPhase = true;
    private bool _selecting;
    private TerminalPoint _selectionAnchor;
    private DateTimeOffset _lastClickTime;
    private TerminalPoint _lastClickCell;
    private int _clickCount;
    private bool _attached;

    public TerminalView()
    {
        Focusable = true;
        ClipToBounds = true;
        InputMethod.SetIsInputMethodEnabled(this, true);
        _textInputClient = new TerminalTextInputMethodClient(this);
        TextInputMethodClientRequested += (_, args) => args.Client = _textInputClient;
        _blinkTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Render, OnBlinkTick);
    }

    public Terminal? Terminal
    {
        get => GetValue(TerminalProperty);
        set => SetValue(TerminalProperty, value);
    }

    public TerminalTheme TerminalTheme
    {
        get => GetValue(TerminalThemeProperty);
        set => SetValue(TerminalThemeProperty, value);
    }

    public TerminalRenderOptions RenderOptions
    {
        get => GetValue(RenderOptionsProperty);
        set => SetValue(RenderOptionsProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the top-right rendering FPS and frame-time overlay is visible.
    /// </summary>
    public bool ShowRenderingDebugOverlay
    {
        get => GetValue(ShowRenderingDebugOverlayProperty);
        set => SetValue(ShowRenderingDebugOverlayProperty, value);
    }

    public TerminalSelection? Selection => _controller?.Selection;
    public bool HasSelection => HasNonEmptySelection(Selection);
    public int ScrollValue => _frame?.ViewportY ?? 0;
    public int ScrollMaximum => _frame?.BaseY ?? 0;
    public int Columns => _frame?.Columns ?? Terminal?.Columns ?? 0;
    public int Rows => _frame?.Rows ?? Terminal?.Rows ?? 0;

    public event EventHandler? SelectionChanged;

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_backend is not null && _frame is not null)
        {
            context.Custom(new SkiaDrawOperation(
                new Rect(Bounds.Size),
                _backend,
                _frame,
                ShowRenderingDebugOverlay ? _renderingDebugMetrics : null));
        }
    }

    public void ClearSelection()
    {
        _controller?.SetSelection(null);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public async ValueTask SelectAllAsync(CancellationToken cancellationToken = default)
    {
        Terminal? terminal = Terminal;
        if (terminal is null || _controller is null)
        {
            return;
        }
        TerminalSnapshot snapshot = await terminal.GetSnapshotAsync(SnapshotScope.ActiveBuffer, cancellationToken);
        if (snapshot.ActiveBuffer.Lines.Length == 0)
        {
            return;
        }
        _controller.SetSelection(new TerminalSelection(
            0,
            0,
            snapshot.Columns,
            snapshot.ActiveBuffer.Lines.Length - 1));
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public async ValueTask<string> CopySelectionAsync(CancellationToken cancellationToken = default)
    {
        if (_controller is null)
        {
            return string.Empty;
        }
        string text = await _controller.GetSelectedTextAsync(cancellationToken);
        IClipboard? clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null && text.Length != 0)
        {
            await clipboard.SetTextAsync(text);
        }
        return text;
    }

    public async ValueTask PasteAsync(string? text = null, CancellationToken cancellationToken = default)
    {
        Terminal? terminal = Terminal;
        if (terminal is null)
        {
            return;
        }
        if (text is null)
        {
            IClipboard? clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            text = clipboard is null ? null : await clipboard.TryGetTextAsync();
        }
        if (!string.IsNullOrEmpty(text))
        {
            await terminal.PasteAsync(text, cancellationToken);
        }
    }

    public ValueTask ScrollToLineAsync(int line, CancellationToken cancellationToken = default) =>
        Terminal?.ScrollToLineAsync(line, cancellationToken) ?? ValueTask.CompletedTask;

    internal Rect GetCursorRectangle()
    {
        TerminalRenderFrame? frame = _frame;
        if (frame is null)
        {
            return default;
        }
        return new Rect(
            frame.Viewport.Padding.Left + frame.CursorColumn * frame.Metrics.CellWidth,
            frame.Viewport.Padding.Top + frame.CursorRow * frame.Metrics.CellHeight,
            frame.Metrics.CellWidth,
            frame.Metrics.CellHeight);
    }

    internal void SetPreeditText(string text)
    {
        _controller?.SetPreeditText(text);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        AttachTerminal(Terminal);
        _blinkTimer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _attached = false;
        _keyState.Clear();
        _blinkTimer.Stop();
        DetachTerminal();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TerminalProperty && _attached)
        {
            AttachTerminal(change.GetNewValue<Terminal?>());
        }
        else if (change.Property == TerminalThemeProperty && _controller is not null)
        {
            _controller.Theme = change.GetNewValue<TerminalTheme>();
        }
        else if (change.Property == RenderOptionsProperty && _controller is not null)
        {
            _controller.Options = change.GetNewValue<TerminalRenderOptions>();
        }
        else if (change.Property == ShowRenderingDebugOverlayProperty)
        {
            _renderingDebugMetrics.Reset();
            InvalidateVisual();
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Size result = base.ArrangeOverride(finalSize);
        SchedulePrepareFrame();
        return result;
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (_controller is not null)
        {
            _controller.IsFocused = true;
        }
        SendWithoutThrow(Terminal?.SendFocusAsync(true) ?? ValueTask.CompletedTask);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        _keyState.Clear();
        if (_controller is not null)
        {
            _controller.IsFocused = false;
            _controller.SetPreeditText(null);
        }
        SendWithoutThrow(Terminal?.SendFocusAsync(false) ?? ValueTask.CompletedTask);
        base.OnLostFocus(e);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (Terminal is not null && !string.IsNullOrEmpty(e.Text))
        {
            _controller?.SetPreeditText(null);
            SendWithoutThrow(Terminal.SendInputAsync(e.Text));
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        bool isMac = OperatingSystem.IsMacOS();
        if (AvaloniaKeyMapper.ShouldCopy(e.Key, e.KeyModifiers, isMac, HasSelection))
        {
            _keyState.SuppressRelease(e.PhysicalKey, e.Key);
            SendWithoutThrow(CopySelectionAsync());
            e.Handled = true;
            return;
        }
        if (AvaloniaKeyMapper.ShouldPaste(e.Key, e.KeyModifiers, isMac))
        {
            _keyState.SuppressRelease(e.PhysicalKey, e.Key);
            SendWithoutThrow(PasteAsync());
            e.Handled = true;
            return;
        }
        if (AvaloniaKeyMapper.ShouldSelectAll(e.Key, e.KeyModifiers, isMac))
        {
            _keyState.SuppressRelease(e.PhysicalKey, e.Key);
            SendWithoutThrow(SelectAllAsync());
            e.Handled = true;
            return;
        }
        Terminal? terminal = Terminal;
        if (terminal is null)
        {
            return;
        }
        TerminalModes? modes = GetCurrentModes(terminal);
        bool enhancedKeyboardMode =
            modes?.KittyKeyboardFlags != TerminalKittyKeyboardFlags.None || modes?.Win32InputMode == true;
        TerminalKeyEventType eventType = _keyState.KeyDown(e.PhysicalKey, e.Key);
        TerminalKeyEvent key = AvaloniaKeyMapper.Create(
            e.Key,
            e.PhysicalKey,
            e.KeySymbol,
            e.KeyModifiers,
            eventType);
        if (!AvaloniaKeyMapper.ShouldUseTextInput(
                key,
                enhancedKeyboardMode,
                isMac,
                OperatingSystem.IsWindows(),
                terminal.Options.MacOptionIsMeta))
        {
            SendWithoutThrow(terminal.SendKeyAsync(key));
            e.Handled = true;
        }
    }

    internal static bool HasNonEmptySelection(TerminalSelection? selection) =>
        selection is { IsEmpty: false };

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (_keyState.KeyUp(e.PhysicalKey, e.Key))
        {
            e.Handled = true;
            return;
        }
        Terminal? terminal = Terminal;
        TerminalModes? modes = terminal is null ? null : GetCurrentModes(terminal);
        if (terminal is null || modes is null ||
            modes.KittyKeyboardFlags == TerminalKittyKeyboardFlags.None && !modes.Win32InputMode)
        {
            return;
        }
        TerminalKeyEvent key = AvaloniaKeyMapper.Create(
            e.Key,
            e.PhysicalKey,
            e.KeySymbol,
            e.KeyModifiers,
            TerminalKeyEventType.Release);
        SendWithoutThrow(terminal.SendKeyAsync(key));
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        Terminal? terminal = Terminal;
        TerminalRenderFrame? frame = _frame;
        if (terminal is null || frame is null)
        {
            return;
        }
        Point position = e.GetPosition(this);
        TerminalPoint cell = HitCell(position, frame);
        TerminalMouseTrackingMode mouseTracking = frame.Modes?.MouseTracking ?? TerminalMouseTrackingMode.None;
        bool localSelection = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ||
            mouseTracking == TerminalMouseTrackingMode.None;
        if (!localSelection)
        {
            SendMouse(position, TerminalMouseButton.Left, TerminalMouseAction.Down, e.KeyModifiers);
        }
        else
        {
            UpdateClickCount(cell);
            _selecting = true;
            _selectionAnchor = cell;
            e.Pointer.Capture(this);
            SendWithoutThrow(BeginSelectionAsync(cell, _clickCount));
        }
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Terminal? terminal = Terminal;
        TerminalRenderFrame? frame = _frame;
        if (terminal is null || frame is null)
        {
            return;
        }
        Point position = e.GetPosition(this);
        TerminalMouseTrackingMode mouseTracking = frame.Modes?.MouseTracking ?? TerminalMouseTrackingMode.None;
        if (_selecting)
        {
            TerminalPoint cell = HitCell(position, frame);
            _controller?.SetSelection(new TerminalSelection(
                (int)_selectionAnchor.X,
                (int)_selectionAnchor.Y,
                (int)cell.X + 1,
                (int)cell.Y));
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
        else if (mouseTracking == TerminalMouseTrackingMode.Any ||
                 mouseTracking == TerminalMouseTrackingMode.Drag &&
                 e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            SendMouse(position, TerminalMouseButton.Left, TerminalMouseAction.Move, e.KeyModifiers);
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_selecting)
        {
            _selecting = false;
            e.Pointer.Capture(null);
        }
        else if ((_frame?.Modes?.MouseTracking ?? TerminalMouseTrackingMode.None) != TerminalMouseTrackingMode.None)
        {
            SendMouse(e.GetPosition(this), TerminalMouseButton.Left, TerminalMouseAction.Up, e.KeyModifiers);
        }
        e.Handled = true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        Terminal? terminal = Terminal;
        if (terminal is null)
        {
            return;
        }
        if ((_frame?.Modes?.MouseTracking ?? TerminalMouseTrackingMode.None) != TerminalMouseTrackingMode.None &&
            !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            SendMouse(
                e.GetPosition(this),
                TerminalMouseButton.Wheel,
                e.Delta.Y > 0 ? TerminalMouseAction.Up : TerminalMouseAction.Down,
                e.KeyModifiers);
        }
        else
        {
            int lines = Math.Max(1, (int)Math.Round(Math.Abs(e.Delta.Y) * 3));
            SendWithoutThrow(terminal.ScrollLinesAsync(e.Delta.Y > 0 ? -lines : lines));
        }
        e.Handled = true;
    }

    private void AttachTerminal(Terminal? terminal)
    {
        DetachTerminal();
        if (terminal is null || terminal.IsDisposed)
        {
            return;
        }
        _backend = new SkiaTerminalRenderBackend();
        _controller = new TerminalRenderController(terminal, _backend, RenderOptions, TerminalTheme);
        _controller.IsFocused = IsFocused;
        _controller.Invalidated += OnControllerInvalidated;
        SchedulePrepareFrame();
    }

    private void DetachTerminal()
    {
        _prepareCancellation?.Cancel();
        _prepareCancellation = null;
        if (_controller is not null)
        {
            _controller.Invalidated -= OnControllerInvalidated;
            _controller.Dispose();
            _controller = null;
        }
        _backend?.Dispose();
        _backend = null;
        _renderingDebugMetrics.Reset();
        PublishFrame(null);
        _lastColumns = 0;
        _lastRows = 0;
        InvalidateVisual();
    }

    private void OnControllerInvalidated(object? sender, EventArgs args) => SchedulePrepareFrame();

    private void SchedulePrepareFrame()
    {
        if (!_attached || _controller is null || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }
        if (Volatile.Read(ref _preparing) != 0)
        {
            Interlocked.Exchange(ref _prepareAgain, 1);
            return;
        }
        if (Interlocked.Exchange(ref _prepareScheduled, 1) != 0)
        {
            return;
        }
        Dispatcher.UIThread.Post(PrepareFrameAsync, DispatcherPriority.Render);
    }

    private async void PrepareFrameAsync()
    {
        Interlocked.Exchange(ref _prepareScheduled, 0);
        if (Interlocked.Exchange(ref _preparing, 1) != 0)
        {
            Interlocked.Exchange(ref _prepareAgain, 1);
            return;
        }
        TerminalRenderController? controller = _controller;
        Terminal? terminal = Terminal;
        if (controller is null || terminal is null || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            Interlocked.Exchange(ref _preparing, 0);
            return;
        }
        var cancellation = new CancellationTokenSource();
        _prepareCancellation = cancellation;
        try
        {
            Thickness padding = Padding;
            double scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
            var viewport = new TerminalViewport(
                Bounds.Width,
                Bounds.Height,
                scale,
                new TerminalThickness(padding.Left, padding.Top, padding.Right, padding.Bottom));
            TerminalRenderFrame frame = await Task.Run(
                async () => await controller.PrepareFrameAsync(viewport, cancellation.Token).ConfigureAwait(false),
                cancellation.Token);
            if (!ReferenceEquals(controller, _controller))
            {
                return;
            }
            TerminalRenderFrame? previousFrame = _frame;
            PublishFrame(frame);
            if (previousFrame is null || previousFrame.CursorColumn != frame.CursorColumn ||
                previousFrame.CursorRow != frame.CursorRow || previousFrame.Metrics != frame.Metrics ||
                previousFrame.Viewport != frame.Viewport)
            {
                _textInputClient.NotifyCursorChanged();
            }
            int columns = Math.Max(2, (int)Math.Floor((Bounds.Width - padding.Left - padding.Right) / frame.Metrics.CellWidth));
            int rows = Math.Max(1, (int)Math.Floor((Bounds.Height - padding.Top - padding.Bottom) / frame.Metrics.CellHeight));
            if (columns != _lastColumns || rows != _lastRows)
            {
                _lastColumns = columns;
                _lastRows = rows;
                await terminal.ResizeAsync(columns, rows, cancellation.Token);
            }
            if (!ReferenceEquals(previousFrame, frame) && !frame.Damage.IsEmpty)
            {
                InvalidateVisual();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception exception)
        {
            terminal.Options.Logger?.Log(TerminalLogLevel.Error, "Failed to prepare an Avalonia terminal frame.", exception);
        }
        finally
        {
            if (ReferenceEquals(_prepareCancellation, cancellation))
            {
                _prepareCancellation = null;
            }
            cancellation.Dispose();
            Interlocked.Exchange(ref _preparing, 0);
            if (Interlocked.Exchange(ref _prepareAgain, 0) != 0)
            {
                SchedulePrepareFrame();
            }
        }
    }

    private async ValueTask BeginSelectionAsync(TerminalPoint cell, int clickCount)
    {
        Terminal? terminal = Terminal;
        TerminalRenderController? controller = _controller;
        if (terminal is null || controller is null)
        {
            return;
        }
        if (clickCount == 1)
        {
            controller.SetSelection(new TerminalSelection((int)cell.X, (int)cell.Y, (int)cell.X, (int)cell.Y));
        }
        else
        {
            TerminalSnapshot snapshot = await terminal.GetSnapshotAsync(SnapshotScope.ActiveBuffer);
            int lineIndex = Math.Clamp((int)cell.Y, 0, snapshot.ActiveBuffer.Lines.Length - 1);
            if (clickCount == 2)
            {
                TerminalLineSnapshot line = snapshot.ActiveBuffer.Lines[lineIndex];
                int column = Math.Clamp((int)cell.X, 0, line.Cells.Length - 1);
                int start = column;
                int end = column + 1;
                int kind = CellKind(line.Cells[column].Text);
                while (start > 0 && CellKind(line.Cells[start - 1].Text) == kind) start--;
                while (end < line.Cells.Length && CellKind(line.Cells[end].Text) == kind) end++;
                controller.SetSelection(new TerminalSelection(start, lineIndex, end, lineIndex));
            }
            else
            {
                int startLine = lineIndex;
                int endLine = lineIndex;
                while (startLine > 0 && snapshot.ActiveBuffer.Lines[startLine].IsWrapped) startLine--;
                while (endLine + 1 < snapshot.ActiveBuffer.Lines.Length &&
                       snapshot.ActiveBuffer.Lines[endLine + 1].IsWrapped) endLine++;
                controller.SetSelection(new TerminalSelection(0, startLine, snapshot.Columns, endLine));
            }
        }
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void PublishFrame(TerminalRenderFrame? frame)
    {
        int previousScrollValue = ScrollValue;
        int previousScrollMaximum = ScrollMaximum;
        int previousColumns = Columns;
        int previousRows = Rows;
        _frame = frame;
        if (previousScrollValue != ScrollValue)
        {
            RaisePropertyChanged(ScrollValueProperty, previousScrollValue, ScrollValue);
        }
        if (previousScrollMaximum != ScrollMaximum)
        {
            RaisePropertyChanged(ScrollMaximumProperty, previousScrollMaximum, ScrollMaximum);
        }
        if (previousColumns != Columns)
        {
            RaisePropertyChanged(ColumnsProperty, previousColumns, Columns);
        }
        if (previousRows != Rows)
        {
            RaisePropertyChanged(RowsProperty, previousRows, Rows);
        }
    }

    private static int CellKind(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        Rune rune = text.EnumerateRunes().First();
        return Rune.IsLetterOrDigit(rune) || rune.Value == '_' ? 1 : 2;
    }

    private TerminalModes? GetCurrentModes(Terminal terminal) =>
        terminal.Options.AllowProposedApi ? terminal.Modes : _frame?.Modes;

    private void UpdateClickCount(TerminalPoint cell)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (now - _lastClickTime <= TimeSpan.FromMilliseconds(500) && cell == _lastClickCell)
        {
            _clickCount = _clickCount % 3 + 1;
        }
        else
        {
            _clickCount = 1;
        }
        _lastClickTime = now;
        _lastClickCell = cell;
    }

    private static TerminalPoint HitCell(Point position, TerminalRenderFrame frame)
    {
        int column = Math.Clamp(
            (int)Math.Floor((position.X - frame.Viewport.Padding.Left) / frame.Metrics.CellWidth),
            0,
            Math.Max(0, frame.Columns - 1));
        int row = Math.Clamp(
            (int)Math.Floor((position.Y - frame.Viewport.Padding.Top) / frame.Metrics.CellHeight),
            0,
            Math.Max(0, frame.Rows - 1));
        return new TerminalPoint(column, frame.ViewportY + row);
    }

    private void SendMouse(
        Point position,
        TerminalMouseButton button,
        TerminalMouseAction action,
        KeyModifiers modifiers)
    {
        Terminal? terminal = Terminal;
        TerminalRenderFrame? frame = _frame;
        if (terminal is null || frame is null)
        {
            return;
        }
        TerminalPoint cell = HitCell(position, frame);
        int row = (int)cell.Y - frame.ViewportY;
        var value = new TerminalMouseEvent(
            (int)cell.X + 1,
            row + 1,
            Math.Max(0, (int)Math.Round(position.X * frame.Viewport.RenderScale)),
            Math.Max(0, (int)Math.Round(position.Y * frame.Viewport.RenderScale)),
            button,
            action,
            AvaloniaKeyMapper.MapModifiers(modifiers));
        SendWithoutThrow(terminal.SendMouseAsync(value));
    }

    private void OnBlinkTick(object? sender, EventArgs args)
    {
        _cursorPhase = !_cursorPhase;
        _blinkPhase = !_blinkPhase;
        _controller?.SetBlinkPhases(_cursorPhase, _blinkPhase);
    }

    private void SendWithoutThrow(ValueTask operation)
    {
        if (operation.IsCompletedSuccessfully)
        {
            return;
        }
        _ = ObserveAsync(operation);
    }

    private void SendWithoutThrow<T>(ValueTask<T> operation)
    {
        if (operation.IsCompletedSuccessfully)
        {
            return;
        }
        _ = ObserveAsync(operation);
    }

    private async Task ObserveAsync(ValueTask operation)
    {
        try
        {
            await operation;
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception exception)
        {
            Terminal?.Options.Logger?.Log(TerminalLogLevel.Error, "A terminal UI input operation failed.", exception);
        }
    }

    private async Task ObserveAsync<T>(ValueTask<T> operation)
    {
        try
        {
            await operation;
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception exception)
        {
            Terminal?.Options.Logger?.Log(TerminalLogLevel.Error, "A terminal UI input operation failed.", exception);
        }
    }

}

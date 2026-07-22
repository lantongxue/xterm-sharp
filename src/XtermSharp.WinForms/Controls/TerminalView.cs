using System.ComponentModel;
using System.Drawing.Imaging;
using System.Text;
using SkiaSharp;

namespace XtermSharp.WinForms.Controls;

/// <summary>Interactive Windows Forms terminal control backed by the XtermSharp Skia renderer.</summary>
[DefaultEvent(nameof(SelectionChanged))]
[DefaultProperty(nameof(Terminal))]
public sealed class TerminalView : Control
{
    private readonly System.Windows.Forms.Timer _blinkTimer;
    private readonly HashSet<Keys> _pressedKeys = [];
    private readonly HashSet<Keys> _suppressedReleases = [];
    private SkiaTerminalRenderBackend? _backend;
    private TerminalRenderController? _controller;
    private TerminalRenderFrame? _frame;
    private Bitmap? _bitmap;
    private CancellationTokenSource? _prepareCancellation;
    private CancellationTokenSource? _linkCancellation;
    private Terminal? _terminal;
    private TerminalTheme _terminalTheme = TerminalTheme.Default;
    private TerminalRenderOptions _renderOptions = new();
    private TerminalLink? _hoveredLink;
    private TerminalLink? _pressedLink;
    private TerminalLinkEvent? _lastLinkEvent;
    private Cursor? _cursorBeforeLink;
    private Point _lastPointerPosition;
    private int _pendingLinkColumn;
    private int _pendingLinkLine;
    private int _prepareScheduled;
    private int _preparing;
    private int _prepareAgain;
    private int _lastColumns;
    private int _lastRows;
    private char _pendingHighSurrogate;
    private bool _cursorPhase = true;
    private bool _blinkPhase = true;
    private bool _selecting;
    private bool _pointerInside;
    private bool _linkCursorApplied;
    private TerminalPoint _selectionAnchor;

    public TerminalView()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.Opaque |
            ControlStyles.Selectable |
            ControlStyles.UserPaint,
            true);
        DoubleBuffered = true;
        TabStop = true;
        ImeMode = ImeMode.On;
        AccessibleRole = AccessibleRole.Text;
        AccessibleName = "Terminal";
        BackColor = Color.Black;
        _blinkTimer = new System.Windows.Forms.Timer { Interval = 500 };
        _blinkTimer.Tick += OnBlinkTick;
    }

    /// <summary>Gets or sets the externally owned terminal displayed by this control.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Terminal? Terminal
    {
        get => _terminal;
        set
        {
            if (ReferenceEquals(_terminal, value))
            {
                return;
            }
            _pendingHighSurrogate = default;
            _terminal = value;
            if (IsHandleCreated)
            {
                AttachTerminal(value);
            }
        }
    }

    /// <summary>Gets or sets the terminal color theme.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TerminalTheme TerminalTheme
    {
        get => _terminalTheme;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(_terminalTheme, value))
            {
                return;
            }
            _terminalTheme = value;
            if (_controller is not null)
            {
                _controller.Theme = value;
            }
        }
    }

    /// <summary>Gets or sets backend-neutral terminal rendering options.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TerminalRenderOptions RenderOptions
    {
        get => _renderOptions;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(_renderOptions, value))
            {
                return;
            }
            _renderOptions = value;
            if (_controller is not null)
            {
                _controller.Options = value;
            }
        }
    }

    [Browsable(false)]
    public TerminalSelection? Selection => _controller?.Selection;

    [Browsable(false)]
    public bool HasSelection => HasNonEmptySelection(Selection);

    [Browsable(false)]
    public int ScrollValue => _frame?.ViewportY ?? 0;

    [Browsable(false)]
    public int ScrollMaximum => _frame?.BaseY ?? 0;

    [Browsable(false)]
    public int Columns => _frame?.Columns ?? Terminal?.Columns ?? 0;

    [Browsable(false)]
    public int Rows => _frame?.Rows ?? Terminal?.Rows ?? 0;

    public event EventHandler? SelectionChanged;

    public event EventHandler? ViewportChanged;

    public void ClearSelection() => Terminal?.ClearSelection();

    public async ValueTask SelectAllAsync(CancellationToken cancellationToken = default)
    {
        Terminal? terminal = Terminal;
        if (terminal is null || _controller is null)
        {
            return;
        }
        TerminalSnapshot snapshot = await terminal.GetSnapshotAsync(
            SnapshotScope.ActiveBuffer,
            cancellationToken).ConfigureAwait(false);
        if (snapshot.ActiveBuffer.Lines.Length == 0)
        {
            return;
        }
        terminal.SetSelection(new TerminalSelectionRange(
            0,
            0,
            snapshot.Columns,
            snapshot.ActiveBuffer.Lines.Length - 1));
    }

    public async ValueTask<string> CopySelectionAsync(CancellationToken cancellationToken = default)
    {
        TerminalRenderController? controller = _controller;
        if (controller is null)
        {
            return string.Empty;
        }
        string text = await controller.GetSelectedTextAsync(cancellationToken).ConfigureAwait(false);
        if (text.Length != 0)
        {
            await InvokeOnUiThreadAsync(
                () => System.Windows.Forms.Clipboard.SetText(text, TextDataFormat.UnicodeText),
                cancellationToken).ConfigureAwait(false);
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
            text = await InvokeOnUiThreadAsync(
                () => System.Windows.Forms.Clipboard.ContainsText(TextDataFormat.UnicodeText)
                    ? System.Windows.Forms.Clipboard.GetText(TextDataFormat.UnicodeText)
                    : string.Empty,
                cancellationToken).ConfigureAwait(false);
        }
        if (!string.IsNullOrEmpty(text))
        {
            await terminal.PasteAsync(text, cancellationToken).ConfigureAwait(false);
        }
    }

    public ValueTask ScrollToLineAsync(int line, CancellationToken cancellationToken = default) =>
        Terminal?.ScrollToLineAsync(line, cancellationToken) ?? ValueTask.CompletedTask;

    protected override bool IsInputKey(Keys keyData)
    {
        Keys keyCode = keyData & Keys.KeyCode;
        return keyCode is Keys.Tab or Keys.Left or Keys.Right or Keys.Up or Keys.Down or
            Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown or Keys.Insert or Keys.Delete ||
            base.IsInputKey(keyData);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        AttachTerminal(Terminal);
        _blinkTimer.Start();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        _blinkTimer.Stop();
        _pressedKeys.Clear();
        _suppressedReleases.Clear();
        DetachTerminal();
        base.OnHandleDestroyed(e);
    }

    protected override void OnResize(EventArgs e)
    {
        DisposeBitmap();
        SchedulePrepareFrame();
        base.OnResize(e);
    }

    protected override void OnPaddingChanged(EventArgs e)
    {
        SchedulePrepareFrame();
        base.OnPaddingChanged(e);
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        DisposeBitmap();
        SchedulePrepareFrame();
        base.OnDpiChangedAfterParent(e);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        if (_controller is not null)
        {
            _controller.IsFocused = true;
        }
        SendWithoutThrow(Terminal?.SendFocusAsync(true) ?? ValueTask.CompletedTask);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        _pressedKeys.Clear();
        _suppressedReleases.Clear();
        _pendingHighSurrogate = default;
        if (_controller is not null)
        {
            _controller.IsFocused = false;
            _controller.SetPreeditText(null);
        }
        SendWithoutThrow(Terminal?.SendFocusAsync(false) ?? ValueTask.CompletedTask);
        base.OnLostFocus(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        Keys modifiers = e.Modifiers;
        if (WinFormsKeyMapper.ShouldCopy(e.KeyCode, modifiers, HasSelection))
        {
            _suppressedReleases.Add(e.KeyCode);
            SendWithoutThrow(CopySelectionAsync());
            e.SuppressKeyPress = true;
            return;
        }
        if (WinFormsKeyMapper.ShouldPaste(e.KeyCode, modifiers))
        {
            _suppressedReleases.Add(e.KeyCode);
            SendWithoutThrow(PasteAsync());
            e.SuppressKeyPress = true;
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
        TerminalKeyEventType eventType = _pressedKeys.Add(e.KeyCode)
            ? TerminalKeyEventType.Press
            : TerminalKeyEventType.Repeat;
        string? text = WinFormsKeyMapper.GetText(e.KeyCode, modifiers);
        TerminalKeyEvent key = WinFormsKeyMapper.Create(e.KeyCode, text, modifiers, eventType);
        if (!WinFormsKeyMapper.ShouldUseTextInput(key, enhancedKeyboardMode))
        {
            SendWithoutThrow(terminal.SendKeyAsync(key));
            e.SuppressKeyPress = true;
        }
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (Terminal is not null && !char.IsControl(e.KeyChar))
        {
            SendWithoutThrow(SendCommittedCharacterAsync(e.KeyChar));
            e.Handled = true;
        }
        else
        {
            _pendingHighSurrogate = default;
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        _pressedKeys.Remove(e.KeyCode);
        if (_suppressedReleases.Remove(e.KeyCode))
        {
            e.SuppressKeyPress = true;
            return;
        }
        Terminal? terminal = Terminal;
        TerminalModes? modes = terminal is null ? null : GetCurrentModes(terminal);
        if (terminal is null || modes is null ||
            modes.KittyKeyboardFlags == TerminalKittyKeyboardFlags.None && !modes.Win32InputMode)
        {
            return;
        }
        TerminalKeyEvent key = WinFormsKeyMapper.Create(
            e.KeyCode,
            WinFormsKeyMapper.GetText(e.KeyCode, e.Modifiers),
            e.Modifiers,
            TerminalKeyEventType.Release);
        SendWithoutThrow(terminal.SendKeyAsync(key));
        e.SuppressKeyPress = true;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        Terminal? terminal = Terminal;
        TerminalRenderFrame? frame = _frame;
        if (terminal is null || frame is null)
        {
            return;
        }

        TerminalMouseButton button = MapButton(e.Button);
        PointF position = ToLogical(e.Location, frame.Viewport.RenderScale);
        TerminalPoint cell = HitCell(position, frame);
        TerminalLinkEvent linkEvent = CreateLinkEvent(
            position,
            e.Location,
            frame,
            button,
            TerminalMouseAction.Down,
            ModifierKeys);
        _lastLinkEvent = linkEvent;
        if (button == TerminalMouseButton.Left)
        {
            SetPressedLink(linkEvent, frame.Columns);
        }

        TerminalMouseTrackingMode mouseTracking = frame.Modes?.MouseTracking ?? TerminalMouseTrackingMode.None;
        bool localSelection = button == TerminalMouseButton.Left &&
            ((ModifierKeys & Keys.Shift) != 0 || mouseTracking == TerminalMouseTrackingMode.None);
        if (localSelection)
        {
            _selecting = true;
            _selectionAnchor = cell;
            Capture = true;
            SendWithoutThrow(BeginSelectionAsync(cell, Math.Clamp(e.Clicks, 1, 3)));
        }
        else if (mouseTracking != TerminalMouseTrackingMode.None)
        {
            SendMouse(position, e.Location, button, TerminalMouseAction.Down, ModifierKeys);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Terminal? terminal = Terminal;
        TerminalRenderFrame? frame = _frame;
        if (terminal is null || frame is null)
        {
            return;
        }
        _pointerInside = true;
        _lastPointerPosition = e.Location;
        PointF position = ToLogical(e.Location, frame.Viewport.RenderScale);
        QueueLinkUpdate(position, e.Location, ModifierKeys);

        TerminalMouseTrackingMode mouseTracking = frame.Modes?.MouseTracking ?? TerminalMouseTrackingMode.None;
        if (_selecting)
        {
            TerminalPoint cell = HitCell(position, frame);
            terminal.SetSelection(new TerminalSelectionRange(
                (int)_selectionAnchor.X,
                (int)_selectionAnchor.Y,
                (int)cell.X + 1,
                (int)cell.Y));
        }
        else if (mouseTracking == TerminalMouseTrackingMode.Any ||
                 mouseTracking == TerminalMouseTrackingMode.Drag && (e.Button & MouseButtons.Left) != 0)
        {
            SendMouse(position, e.Location, TerminalMouseButton.Left, TerminalMouseAction.Move, ModifierKeys);
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _pointerInside = false;
        _pressedLink = null;
        ClearHoveredLink(_lastLinkEvent);
        base.OnMouseLeave(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        TerminalRenderFrame? frame = _frame;
        PointF position = frame is null ? default : ToLogical(e.Location, frame.Viewport.RenderScale);
        TerminalMouseButton button = MapButton(e.Button);
        if (frame is not null)
        {
            TerminalLinkEvent linkEvent = CreateLinkEvent(
                position,
                e.Location,
                frame,
                button,
                TerminalMouseAction.Up,
                ModifierKeys);
            _lastLinkEvent = linkEvent;
            if (button == TerminalMouseButton.Left)
            {
                TryActivateLink(linkEvent, frame.Columns);
            }
        }
        _pressedLink = null;
        if (_selecting)
        {
            _selecting = false;
            Capture = false;
        }
        else if (frame is not null &&
                 (frame.Modes?.MouseTracking ?? TerminalMouseTrackingMode.None) != TerminalMouseTrackingMode.None)
        {
            SendMouse(position, e.Location, button, TerminalMouseAction.Up, ModifierKeys);
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        Terminal? terminal = Terminal;
        TerminalRenderFrame? frame = _frame;
        if (terminal is null || frame is null || e.Delta == 0)
        {
            return;
        }
        PointF position = ToLogical(e.Location, frame.Viewport.RenderScale);
        if ((frame.Modes?.MouseTracking ?? TerminalMouseTrackingMode.None) != TerminalMouseTrackingMode.None &&
            (ModifierKeys & Keys.Shift) == 0)
        {
            SendMouse(
                position,
                e.Location,
                TerminalMouseButton.Wheel,
                e.Delta > 0 ? TerminalMouseAction.Up : TerminalMouseAction.Down,
                ModifierKeys);
        }
        else
        {
            int lines = Math.Max(1, Math.Abs(e.Delta) * 3 / SystemInformation.MouseWheelScrollDelta);
            SendWithoutThrow(terminal.ScrollLinesAsync(e.Delta > 0 ? -lines : lines));
        }
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        TerminalRenderFrame? frame = _frame;
        SkiaTerminalRenderBackend? backend = _backend;
        if (frame is null || backend is null || ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            e.Graphics.Clear(BackColor);
            return;
        }

        EnsureBitmap();
        Bitmap bitmap = _bitmap!;
        Rectangle bounds = new(0, 0, bitmap.Width, bitmap.Height);
        BitmapData data = bitmap.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
        try
        {
            var info = new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using SKSurface surface = SKSurface.Create(info, data.Scan0, data.Stride);
            SKCanvas canvas = surface.Canvas;
            TerminalRgbaColor background = TerminalTheme.Background;
            canvas.Clear(new SKColor(background.Red, background.Green, background.Blue, background.Alpha));
            canvas.Scale((float)frame.Viewport.RenderScale);
            backend.Render(canvas, frame);
            canvas.Flush();
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
        e.Graphics.DrawImageUnscaled(bitmap, 0, 0);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _blinkTimer.Stop();
            _blinkTimer.Tick -= OnBlinkTick;
            _blinkTimer.Dispose();
            DetachTerminal();
            DisposeBitmap();
        }
        base.Dispose(disposing);
    }

    internal static bool HasNonEmptySelection(TerminalSelection? selection) =>
        selection is { IsEmpty: false };

    internal ValueTask SendCommittedCharacterAsync(
        char character,
        CancellationToken cancellationToken = default)
    {
        Terminal? terminal = Terminal;
        if (terminal is null || char.IsControl(character))
        {
            _pendingHighSurrogate = default;
            return ValueTask.CompletedTask;
        }
        if (char.IsHighSurrogate(character))
        {
            _pendingHighSurrogate = character;
            return ValueTask.CompletedTask;
        }

        string text;
        if (char.IsLowSurrogate(character))
        {
            if (_pendingHighSurrogate == default)
            {
                return ValueTask.CompletedTask;
            }
            text = char.ConvertFromUtf32(char.ConvertToUtf32(_pendingHighSurrogate, character));
        }
        else
        {
            text = character.ToString();
        }
        _pendingHighSurrogate = default;
        _controller?.SetPreeditText(null);
        return terminal.SendInputAsync(text, cancellationToken: cancellationToken);
    }

    internal void SetHoveredLink(TerminalLink? link, TerminalLinkEvent terminalEvent)
    {
        if (LinksEqual(_hoveredLink, link))
        {
            return;
        }
        if (_hoveredLink is TerminalLink previous)
        {
            InvokeLinkCallback(previous.Leave, terminalEvent, previous.Text);
        }
        _hoveredLink = link;
        if (link is null)
        {
            _controller?.SetHoveredLink(null);
            RestoreLinkCursor();
            return;
        }
        InvokeLinkCallback(link.Hover, terminalEvent, link.Text);
        _controller?.SetHoveredLink(link.Decorations.Underline ? link.Range : null);
        if (link.Decorations.PointerCursor)
        {
            if (!_linkCursorApplied)
            {
                _cursorBeforeLink = Cursor;
                _linkCursorApplied = true;
            }
            Cursor = Cursors.Hand;
        }
        else
        {
            RestoreLinkCursor();
        }
    }

    internal void ClearHoveredLink(TerminalLinkEvent? terminalEvent)
    {
        _linkCancellation?.Cancel();
        if (_hoveredLink is TerminalLink link && terminalEvent is TerminalLinkEvent value)
        {
            InvokeLinkCallback(link.Leave, value, link.Text);
        }
        _hoveredLink = null;
        _controller?.SetHoveredLink(null);
        RestoreLinkCursor();
    }

    internal void SetPressedLink(TerminalLinkEvent terminalEvent, int columns)
    {
        _pressedLink = _hoveredLink?.Range.Contains(
            terminalEvent.Column,
            terminalEvent.BufferLine,
            columns) == true
            ? _hoveredLink
            : null;
    }

    internal void TryActivateLink(TerminalLinkEvent terminalEvent, int columns)
    {
        if (_pressedLink is not TerminalLink pressed ||
            _hoveredLink is not TerminalLink hovered ||
            !LinksEqual(pressed, hovered) ||
            !hovered.Range.Contains(terminalEvent.Column, terminalEvent.BufferLine, columns))
        {
            return;
        }
        InvokeLinkCallback(hovered.Activate, terminalEvent, hovered.Text);
    }

    private void AttachTerminal(Terminal? terminal)
    {
        DetachTerminal();
        if (terminal is null || terminal.IsDisposed || !IsHandleCreated)
        {
            return;
        }
        _backend = new SkiaTerminalRenderBackend();
        _controller = new TerminalRenderController(terminal, _backend, RenderOptions, TerminalTheme);
        _controller.Invalidated += OnControllerInvalidated;
        terminal.SelectionChanged += OnTerminalSelectionChanged;
        _controller.IsFocused = Focused;
        SchedulePrepareFrame();
    }

    private void DetachTerminal()
    {
        _prepareCancellation?.Cancel();
        _linkCancellation?.Cancel();
        _pendingHighSurrogate = default;
        _selecting = false;
        Capture = false;
        _pressedLink = null;
        ClearHoveredLink(_lastLinkEvent);
        Terminal? terminal = _controller?.Terminal;
        if (terminal is not null)
        {
            terminal.SelectionChanged -= OnTerminalSelectionChanged;
        }
        if (_controller is not null)
        {
            _controller.Invalidated -= OnControllerInvalidated;
            _controller.Dispose();
            _controller = null;
        }
        _backend?.Dispose();
        _backend = null;
        PublishFrame(null);
        _lastColumns = 0;
        _lastRows = 0;
        DisposeBitmap();
        if (!IsDisposed)
        {
            Invalidate();
        }
    }

    private void OnControllerInvalidated(object? sender, EventArgs args) => SchedulePrepareFrame();

    private void OnTerminalSelectionChanged(object? sender, EventArgs args)
    {
        PostToUiThread(() => SelectionChanged?.Invoke(this, EventArgs.Empty));
    }

    private void SchedulePrepareFrame()
    {
        if (IsDisposed || Disposing || !IsHandleCreated)
        {
            return;
        }
        if (InvokeRequired)
        {
            PostToUiThread(SchedulePrepareFrame);
            return;
        }
        if (_controller is null || ClientSize.Width <= 0 || ClientSize.Height <= 0)
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
        BeginInvoke(PrepareFrameAsync);
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
        if (controller is null || terminal is null || ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            Interlocked.Exchange(ref _preparing, 0);
            return;
        }

        var cancellation = new CancellationTokenSource();
        _prepareCancellation = cancellation;
        try
        {
            double scale = Math.Max(0.01, DeviceDpi / 96d);
            double width = ClientSize.Width / scale;
            double height = ClientSize.Height / scale;
            var viewport = new TerminalViewport(
                width,
                height,
                scale,
                new TerminalThickness(Padding.Left, Padding.Top, Padding.Right, Padding.Bottom));
            TerminalRenderFrame frame = await Task.Run(
                async () => await controller.PrepareFrameAsync(viewport, cancellation.Token).ConfigureAwait(false),
                cancellation.Token);
            if (!ReferenceEquals(controller, _controller))
            {
                return;
            }
            TerminalRenderFrame? previousFrame = _frame;
            PublishFrame(frame);
            int columns = Math.Max(2, (int)Math.Floor(
                (width - Padding.Left - Padding.Right) / frame.Metrics.CellWidth));
            int rows = Math.Max(1, (int)Math.Floor(
                (height - Padding.Top - Padding.Bottom) / frame.Metrics.CellHeight));
            if (columns != _lastColumns || rows != _lastRows)
            {
                _lastColumns = columns;
                _lastRows = rows;
                await terminal.ResizeAsync(columns, rows, cancellation.Token);
            }
            if (!ReferenceEquals(previousFrame, frame) && !frame.Damage.IsEmpty)
            {
                Invalidate();
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
            terminal.Options.Logger?.Log(
                TerminalLogLevel.Error,
                "Failed to prepare a Windows Forms terminal frame.",
                exception);
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
        if (terminal is null || _controller is null)
        {
            return;
        }
        if (clickCount == 1)
        {
            terminal.SetSelection(new TerminalSelectionRange(
                (int)cell.X,
                (int)cell.Y,
                (int)cell.X,
                (int)cell.Y));
            return;
        }

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
            terminal.SetSelection(new TerminalSelectionRange(start, lineIndex, end, lineIndex));
        }
        else
        {
            int startLine = lineIndex;
            int endLine = lineIndex;
            while (startLine > 0 && snapshot.ActiveBuffer.Lines[startLine].IsWrapped) startLine--;
            while (endLine + 1 < snapshot.ActiveBuffer.Lines.Length &&
                   snapshot.ActiveBuffer.Lines[endLine + 1].IsWrapped) endLine++;
            terminal.SetSelection(new TerminalSelectionRange(0, startLine, snapshot.Columns, endLine));
        }
    }

    private void PublishFrame(TerminalRenderFrame? frame)
    {
        int previousScrollValue = ScrollValue;
        int previousScrollMaximum = ScrollMaximum;
        int previousColumns = Columns;
        int previousRows = Rows;
        TerminalRenderFrame? previousFrame = _frame;
        _frame = frame;
        if (previousScrollValue != ScrollValue || previousScrollMaximum != ScrollMaximum ||
            previousColumns != Columns || previousRows != Rows)
        {
            ViewportChanged?.Invoke(this, EventArgs.Empty);
        }
        bool linkCoordinatesChanged = frame is null || previousFrame is null ||
            previousFrame.Revision != frame.Revision ||
            previousFrame.Columns != frame.Columns ||
            previousFrame.ViewportY != frame.ViewportY;
        if (linkCoordinatesChanged)
        {
            ClearHoveredLink(_lastLinkEvent);
            if (_pointerInside && frame is not null)
            {
                PointF position = ToLogical(_lastPointerPosition, frame.Viewport.RenderScale);
                QueueLinkUpdate(position, _lastPointerPosition, ModifierKeys);
            }
        }
    }

    private void QueueLinkUpdate(PointF position, Point pixelPosition, Keys modifiers)
    {
        Terminal? terminal = Terminal;
        TerminalRenderFrame? frame = _frame;
        if (!_pointerInside || terminal is null || frame is null)
        {
            return;
        }
        TerminalLinkEvent terminalEvent = CreateLinkEvent(
            position,
            pixelPosition,
            frame,
            TerminalMouseButton.None,
            TerminalMouseAction.Move,
            modifiers);
        _lastLinkEvent = terminalEvent;
        if (_hoveredLink?.Range.Contains(terminalEvent.Column, terminalEvent.BufferLine, frame.Columns) == true)
        {
            return;
        }
        if (_linkCancellation is { IsCancellationRequested: false } &&
            _pendingLinkColumn == terminalEvent.Column && _pendingLinkLine == terminalEvent.BufferLine)
        {
            return;
        }
        ClearHoveredLink(terminalEvent);
        _linkCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _linkCancellation = cancellation;
        _pendingLinkColumn = terminalEvent.Column;
        _pendingLinkLine = terminalEvent.BufferLine;
        _ = ResolveLinkAsync(terminal, frame.Columns, terminalEvent, cancellation);
    }

    private async Task ResolveLinkAsync(
        Terminal terminal,
        int columns,
        TerminalLinkEvent terminalEvent,
        CancellationTokenSource cancellation)
    {
        try
        {
            TerminalLink? link = await terminal.GetLinkAtAsync(
                terminalEvent.Column,
                terminalEvent.BufferLine,
                cancellation.Token).ConfigureAwait(false);
            cancellation.Token.ThrowIfCancellationRequested();
            PostToUiThread(() =>
            {
                if (ReferenceEquals(terminal, Terminal) && _pointerInside &&
                    _lastLinkEvent is TerminalLinkEvent current &&
                    current.Column == terminalEvent.Column && current.BufferLine == terminalEvent.BufferLine &&
                    link?.Range.Contains(current.Column, current.BufferLine, columns) != false)
                {
                    SetHoveredLink(link, current);
                }
            });
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception exception)
        {
            terminal.Options.Logger?.Log(TerminalLogLevel.Error, "Failed to resolve a terminal link.", exception);
        }
        finally
        {
            if (ReferenceEquals(_linkCancellation, cancellation))
            {
                _linkCancellation = null;
                _pendingLinkColumn = 0;
                _pendingLinkLine = 0;
            }
            cancellation.Dispose();
        }
    }

    private void SendMouse(
        PointF position,
        Point pixelPosition,
        TerminalMouseButton button,
        TerminalMouseAction action,
        Keys modifiers)
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
            Math.Max(0, pixelPosition.X),
            Math.Max(0, pixelPosition.Y),
            button,
            action,
            WinFormsKeyMapper.MapModifiers(modifiers));
        SendWithoutThrow(terminal.SendMouseAsync(value));
    }

    private void InvokeLinkCallback(
        Action<TerminalLinkEvent, string>? callback,
        TerminalLinkEvent terminalEvent,
        string text)
    {
        if (callback is null)
        {
            return;
        }
        try
        {
            callback(terminalEvent, text);
        }
        catch (Exception exception)
        {
            Terminal?.Options.Logger?.Log(TerminalLogLevel.Error, "A terminal link callback threw an exception.", exception);
        }
    }

    private void RestoreLinkCursor()
    {
        if (!_linkCursorApplied)
        {
            return;
        }
        Cursor = _cursorBeforeLink ?? Cursors.Default;
        _cursorBeforeLink = null;
        _linkCursorApplied = false;
    }

    private void OnBlinkTick(object? sender, EventArgs args)
    {
        _cursorPhase = !_cursorPhase;
        _blinkPhase = !_blinkPhase;
        _controller?.SetBlinkPhases(_cursorPhase, _blinkPhase);
    }

    private void EnsureBitmap()
    {
        if (_bitmap?.Width == ClientSize.Width && _bitmap.Height == ClientSize.Height)
        {
            return;
        }
        DisposeBitmap();
        _bitmap = new Bitmap(ClientSize.Width, ClientSize.Height, PixelFormat.Format32bppPArgb);
    }

    private void DisposeBitmap()
    {
        _bitmap?.Dispose();
        _bitmap = null;
    }

    private void PostToUiThread(Action action)
    {
        if (IsDisposed || Disposing || !IsHandleCreated)
        {
            return;
        }
        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(action);
            }
            catch (InvalidOperationException)
            {
            }
        }
        else
        {
            action();
        }
    }

    private Task<T> InvokeOnUiThreadAsync<T>(Func<T> action, CancellationToken cancellationToken)
    {
        if (!InvokeRequired)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(action());
        }
        if (!IsHandleCreated || IsDisposed)
        {
            return Task.FromException<T>(new InvalidOperationException("The terminal view has no live UI handle."));
        }
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationTokenRegistration registration = cancellationToken.Register(
            () => completion.TrySetCanceled(cancellationToken));
        try
        {
            BeginInvoke(() =>
            {
                try
                {
                    if (!completion.Task.IsCompleted)
                    {
                        completion.TrySetResult(action());
                    }
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
                finally
                {
                    registration.Dispose();
                }
            });
        }
        catch (Exception exception)
        {
            registration.Dispose();
            completion.TrySetException(exception);
        }
        return completion.Task;
    }

    private async Task InvokeOnUiThreadAsync(Action action, CancellationToken cancellationToken)
    {
        await InvokeOnUiThreadAsync(
            () =>
            {
                action();
                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    private void SendWithoutThrow(ValueTask operation)
    {
        if (!operation.IsCompletedSuccessfully)
        {
            _ = ObserveAsync(operation);
        }
    }

    private void SendWithoutThrow<T>(ValueTask<T> operation)
    {
        if (!operation.IsCompletedSuccessfully)
        {
            _ = ObserveAsync(operation);
        }
    }

    private async Task ObserveAsync(ValueTask operation)
    {
        try
        {
            await operation.ConfigureAwait(false);
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
            await operation.ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception exception)
        {
            Terminal?.Options.Logger?.Log(TerminalLogLevel.Error, "A terminal UI input operation failed.", exception);
        }
    }

    private TerminalModes? GetCurrentModes(Terminal terminal) =>
        terminal.Options.AllowProposedApi ? terminal.Modes : _frame?.Modes;

    private static int CellKind(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        Rune rune = text.EnumerateRunes().First();
        return Rune.IsLetterOrDigit(rune) || rune.Value == '_' ? 1 : 2;
    }

    private static TerminalPoint HitCell(PointF position, TerminalRenderFrame frame)
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

    private static TerminalLinkEvent CreateLinkEvent(
        PointF position,
        Point pixelPosition,
        TerminalRenderFrame frame,
        TerminalMouseButton button,
        TerminalMouseAction action,
        Keys modifiers)
    {
        TerminalPoint cell = HitCell(position, frame);
        return new TerminalLinkEvent(
            (int)cell.X + 1,
            (int)cell.Y + 1,
            Math.Max(0, pixelPosition.X),
            Math.Max(0, pixelPosition.Y),
            button,
            action,
            WinFormsKeyMapper.MapModifiers(modifiers));
    }

    private static PointF ToLogical(Point point, double scale) =>
        new((float)(point.X / scale), (float)(point.Y / scale));

    private static TerminalMouseButton MapButton(MouseButtons button) => button switch
    {
        MouseButtons.Left => TerminalMouseButton.Left,
        MouseButtons.Middle => TerminalMouseButton.Middle,
        MouseButtons.Right => TerminalMouseButton.Right,
        MouseButtons.XButton1 => TerminalMouseButton.Auxiliary1,
        MouseButtons.XButton2 => TerminalMouseButton.Auxiliary2,
        _ => TerminalMouseButton.None
    };

    private static bool LinksEqual(TerminalLink? first, TerminalLink? second) =>
        ReferenceEquals(first, second) ||
        first is not null && second is not null && first.Text == second.Text && first.Range == second.Range;
}

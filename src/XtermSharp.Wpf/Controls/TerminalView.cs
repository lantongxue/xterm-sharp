using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SkiaSharp;

namespace XtermSharp.Wpf.Controls;

/// <summary>Interactive WPF terminal control backed by the XtermSharp Skia renderer.</summary>
[DefaultEvent(nameof(SelectionChanged))]
[DefaultProperty(nameof(Terminal))]
public sealed partial class TerminalView : Control, IDisposable
{
    public static readonly DependencyProperty TerminalProperty = DependencyProperty.Register(
        nameof(Terminal),
        typeof(Terminal),
        typeof(TerminalView),
        new FrameworkPropertyMetadata(null, OnTerminalPropertyChanged));

    public static readonly DependencyProperty TerminalThemeProperty = DependencyProperty.Register(
        nameof(TerminalTheme),
        typeof(TerminalTheme),
        typeof(TerminalView),
        new FrameworkPropertyMetadata(TerminalTheme.Default, OnTerminalThemePropertyChanged));

    public static readonly DependencyProperty RenderOptionsProperty = DependencyProperty.Register(
        nameof(RenderOptions),
        typeof(TerminalRenderOptions),
        typeof(TerminalView),
        new FrameworkPropertyMetadata(new TerminalRenderOptions(), OnRenderOptionsPropertyChanged));

    public static readonly DependencyProperty ShowRenderingDebugOverlayProperty = DependencyProperty.Register(
        nameof(ShowRenderingDebugOverlay),
        typeof(bool),
        typeof(TerminalView),
        new FrameworkPropertyMetadata(false, OnShowRenderingDebugOverlayPropertyChanged));

    private static readonly DependencyPropertyKey ActiveRenderModePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(ActiveRenderMode),
            typeof(SkiaRenderMode),
            typeof(TerminalView),
            new FrameworkPropertyMetadata(SkiaRenderMode.Unknown));

    private static readonly DependencyPropertyKey IsGpuAcceleratedPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsGpuAccelerated),
            typeof(bool),
            typeof(TerminalView),
            new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty ActiveRenderModeProperty = ActiveRenderModePropertyKey.DependencyProperty;
    public static readonly DependencyProperty IsGpuAcceleratedProperty = IsGpuAcceleratedPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey ScrollValuePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(ScrollValue),
            typeof(int),
            typeof(TerminalView),
            new FrameworkPropertyMetadata(0));

    private static readonly DependencyPropertyKey ScrollMaximumPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(ScrollMaximum),
            typeof(int),
            typeof(TerminalView),
            new FrameworkPropertyMetadata(0));

    private static readonly DependencyPropertyKey ColumnsPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(Columns),
            typeof(int),
            typeof(TerminalView),
            new FrameworkPropertyMetadata(0));

    private static readonly DependencyPropertyKey RowsPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(Rows),
            typeof(int),
            typeof(TerminalView),
            new FrameworkPropertyMetadata(0));

    public static readonly DependencyProperty ScrollValueProperty = ScrollValuePropertyKey.DependencyProperty;
    public static readonly DependencyProperty ScrollMaximumProperty = ScrollMaximumPropertyKey.DependencyProperty;
    public static readonly DependencyProperty ColumnsProperty = ColumnsPropertyKey.DependencyProperty;
    public static readonly DependencyProperty RowsProperty = RowsPropertyKey.DependencyProperty;

    private readonly DispatcherTimer _blinkTimer;
    private readonly HashSet<Key> _pressedKeys = [];
    private readonly HashSet<Key> _suppressedReleases = [];
    private SkiaTerminalRenderBackend? _backend;
    private SkiaGpuElement? _gpuElement;
    private TerminalRenderController? _controller;
    private TerminalRenderFrame? _frame;
    private WriteableBitmap? _bitmap;
    private CancellationTokenSource? _prepareCancellation;
    private CancellationTokenSource? _linkCancellation;
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
    private bool _cursorPhase = true;
    private bool _blinkPhase = true;
    private bool _selecting;
    private bool _pointerInside;
    private bool _linkCursorApplied;
    private bool _attached;
    private bool _disposed;
    private TerminalPoint _selectionAnchor;
    private SkiaRenderMode _activeRenderMode = SkiaRenderMode.Unknown;

    static TerminalView()
    {
        FocusableProperty.OverrideMetadata(typeof(TerminalView), new FrameworkPropertyMetadata(true));
        KeyboardNavigation.IsTabStopProperty.OverrideMetadata(
            typeof(TerminalView),
            new FrameworkPropertyMetadata(true));
        PaddingProperty.OverrideMetadata(
            typeof(TerminalView),
            new FrameworkPropertyMetadata(
                default(Thickness),
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                OnPaddingPropertyChanged));
    }

    public TerminalView()
    {
        Background = Brushes.Black;
        Cursor = Cursors.IBeam;
        ClipToBounds = true;
        AutomationProperties.SetName(this, "Terminal");
        InputMethod.SetIsInputMethodEnabled(this, true);
        InputMethod.SetIsInputMethodSuspended(this, false);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        TextCompositionManager.AddPreviewTextInputStartHandler(this, OnPreviewTextInputStart);
        TextCompositionManager.AddPreviewTextInputUpdateHandler(this, OnPreviewTextInputUpdate);
        _blinkTimer = new DispatcherTimer(DispatcherPriority.Render, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _blinkTimer.Tick += OnBlinkTick;
        TryEnableGpuRendering();
    }

    /// <summary>Gets or sets the externally owned terminal displayed by this control.</summary>
    public Terminal? Terminal
    {
        get => (Terminal?)GetValue(TerminalProperty);
        set => SetValue(TerminalProperty, value);
    }

    /// <summary>Gets or sets the terminal color theme.</summary>
    public TerminalTheme TerminalTheme
    {
        get => (TerminalTheme)GetValue(TerminalThemeProperty);
        set => SetValue(TerminalThemeProperty, value ?? throw new ArgumentNullException(nameof(value)));
    }

    /// <summary>Gets or sets backend-neutral terminal rendering options.</summary>
    public TerminalRenderOptions RenderOptions
    {
        get => (TerminalRenderOptions)GetValue(RenderOptionsProperty);
        set => SetValue(RenderOptionsProperty, value ?? throw new ArgumentNullException(nameof(value)));
    }

    /// <summary>Gets or sets whether rendering telemetry is drawn over the terminal.</summary>
    public bool ShowRenderingDebugOverlay
    {
        get => (bool)GetValue(ShowRenderingDebugOverlayProperty);
        set => SetValue(ShowRenderingDebugOverlayProperty, value);
    }

    /// <summary>Gets how the most recently presented frame was rendered.</summary>
    public SkiaRenderMode ActiveRenderMode => (SkiaRenderMode)GetValue(ActiveRenderModeProperty);

    /// <summary>Gets whether the most recently presented frame used a GPU-backed Skia surface.</summary>
    public bool IsGpuAccelerated => (bool)GetValue(IsGpuAcceleratedProperty);

    /// <summary>Gets the current immutable selection.</summary>
    public TerminalSelection? Selection => _controller?.Selection;

    /// <summary>Gets whether the current selection contains text cells.</summary>
    public bool HasSelection => HasNonEmptySelection(Selection);

    /// <summary>Gets the active viewport's zero-based buffer line.</summary>
    public int ScrollValue => (int)GetValue(ScrollValueProperty);

    /// <summary>Gets the largest valid viewport line.</summary>
    public int ScrollMaximum => (int)GetValue(ScrollMaximumProperty);

    /// <summary>Gets the number of rendered terminal columns.</summary>
    public int Columns => (int)GetValue(ColumnsProperty);

    /// <summary>Gets the number of rendered terminal rows.</summary>
    public int Rows => (int)GetValue(RowsProperty);

    public event EventHandler? SelectionChanged;

    public event EventHandler? ViewportChanged;

    public event EventHandler? RenderModeChanged;

    /// <summary>Clears the active terminal selection.</summary>
    public void ClearSelection() => Terminal?.ClearSelection();

    /// <summary>Selects the complete active buffer.</summary>
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

    /// <summary>Copies the selected terminal text to the WPF clipboard.</summary>
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
                () => System.Windows.Clipboard.SetText(text, TextDataFormat.UnicodeText),
                cancellationToken).ConfigureAwait(false);
        }
        return text;
    }

    /// <summary>Pastes supplied text, or current Unicode clipboard text, into the terminal.</summary>
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
                () => System.Windows.Clipboard.ContainsText(TextDataFormat.UnicodeText)
                    ? System.Windows.Clipboard.GetText(TextDataFormat.UnicodeText)
                    : string.Empty,
                cancellationToken).ConfigureAwait(false);
        }
        if (!string.IsNullOrEmpty(text))
        {
            await terminal.PasteAsync(text, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Scrolls the active viewport to an absolute buffer line.</summary>
    public ValueTask ScrollToLineAsync(int line, CancellationToken cancellationToken = default) =>
        Terminal?.ScrollToLineAsync(line, cancellationToken) ?? ValueTask.CompletedTask;

    protected override int VisualChildrenCount => _gpuElement is null ? 0 : 1;

    protected override Visual GetVisualChild(int index) =>
        index == 0 && _gpuElement is not null
            ? _gpuElement
            : throw new ArgumentOutOfRangeException(nameof(index));

    protected override Size MeasureOverride(Size constraint)
    {
        _gpuElement?.Measure(constraint);
        return base.MeasureOverride(constraint);
    }

    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        _gpuElement?.Arrange(new Rect(arrangeBounds));
        return base.ArrangeOverride(arrangeBounds);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        Rect bounds = new(0, 0, ActualWidth, ActualHeight);
        if (Background is not null)
        {
            drawingContext.DrawRectangle(Background, null, bounds);
        }

        TerminalRenderFrame? frame = _frame;
        SkiaTerminalRenderBackend? backend = _backend;
        if (frame is null || backend is null || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        if (_gpuElement?.IsGpuActive == true)
        {
            return;
        }

        EnsureBitmap();
        WriteableBitmap bitmap = _bitmap!;
        bitmap.Lock();
        try
        {
            var info = new SKImageInfo(
                bitmap.PixelWidth,
                bitmap.PixelHeight,
                SKColorType.Bgra8888,
                SKAlphaType.Premul);
            using SKSurface surface = SKSurface.Create(info, bitmap.BackBuffer, bitmap.BackBufferStride);
            SKCanvas canvas = surface.Canvas;
            TerminalRgbaColor background = TerminalTheme.Background;
            canvas.Clear(new SKColor(background.Red, background.Green, background.Blue, background.Alpha));
            canvas.Scale((float)frame.Viewport.RenderScale);
            backend.Render(canvas, frame, SkiaRenderMode.Software);
            canvas.Flush();
            bitmap.AddDirtyRect(new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
            SetActiveRenderMode(SkiaRenderMode.Software);
        }
        finally
        {
            bitmap.Unlock();
        }
        drawingContext.DrawImage(bitmap, bounds);
    }

    private void TryEnableGpuRendering()
    {
        if (_gpuElement is not null || _disposed)
        {
            return;
        }
        try
        {
            var gpuElement = new SkiaGpuElement();
            gpuElement.PaintSurface += OnGpuPaintSurface;
            gpuElement.RenderingFailed += OnGpuRenderingFailed;
            _gpuElement = gpuElement;
            AddLogicalChild(gpuElement);
            AddVisualChild(gpuElement);
            InvalidateMeasure();
            InvalidatePresentation();
        }
        catch (Exception exception)
        {
            DisableGpuRendering(exception);
        }
    }

    private void OnGpuPaintSurface(SKCanvas canvas)
    {
        TerminalRenderFrame? frame = _frame;
        SkiaTerminalRenderBackend? backend = _backend;
        if (frame is null || backend is null)
        {
            canvas.Clear(SKColors.Transparent);
            return;
        }

        TerminalRgbaColor background = TerminalTheme.Background;
        canvas.Clear(new SKColor(background.Red, background.Green, background.Blue, background.Alpha));
        canvas.Scale((float)frame.Viewport.RenderScale);
        backend.Render(canvas, frame, SkiaRenderMode.Gpu);
        SetActiveRenderMode(SkiaRenderMode.Gpu);
    }

    private void OnGpuRenderingFailed(Exception exception)
    {
        if (_disposed)
        {
            return;
        }
        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.Render,
            new Action(() => DisableGpuRendering(exception)));
    }

    private void DisableGpuRendering(Exception? exception = null)
    {
        SkiaGpuElement? gpuElement = _gpuElement;
        _gpuElement = null;
        if (gpuElement is not null)
        {
            gpuElement.PaintSurface -= OnGpuPaintSurface;
            gpuElement.RenderingFailed -= OnGpuRenderingFailed;
            RemoveLogicalChild(gpuElement);
            RemoveVisualChild(gpuElement);
            gpuElement.Dispose();
        }
        if (exception is not null)
        {
            Terminal?.Options.Logger?.Log(
                TerminalLogLevel.Warning,
                "GPU presentation failed; falling back to the software Skia surface.",
                exception);
        }
        SetActiveRenderMode(SkiaRenderMode.Unknown);
        if (!_disposed)
        {
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    private void InvalidatePresentation()
    {
        InvalidateVisual();
        _gpuElement?.InvalidateVisual();
    }

    private void SetActiveRenderMode(SkiaRenderMode mode)
    {
        if (_activeRenderMode == mode)
        {
            return;
        }
        _activeRenderMode = mode;
        SetValue(ActiveRenderModePropertyKey, mode);
        SetValue(IsGpuAcceleratedPropertyKey, mode == SkiaRenderMode.Gpu);
        RenderModeChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        _bitmap = null;
        SchedulePrepareFrame();
        InvalidatePresentation();
        base.OnRenderSizeChanged(sizeInfo);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        _ = oldDpi;
        _ = newDpi;
        _bitmap = null;
        SchedulePrepareFrame();
        InvalidatePresentation();
        base.OnDpiChanged(oldDpi, newDpi);
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        if (_controller is not null)
        {
            _controller.IsFocused = true;
        }
        SendWithoutThrow(Terminal?.SendFocusAsync(true) ?? ValueTask.CompletedTask);
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        _pressedKeys.Clear();
        _suppressedReleases.Clear();
        if (_controller is not null)
        {
            _controller.IsFocused = false;
            _controller.SetPreeditText(null);
        }
        SendWithoutThrow(Terminal?.SendFocusAsync(false) ?? ValueTask.CompletedTask);
        base.OnLostKeyboardFocus(e);
    }

    /// <summary>Detaches the control and releases renderer resources without disposing its terminal.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        TextCompositionManager.RemovePreviewTextInputStartHandler(this, OnPreviewTextInputStart);
        TextCompositionManager.RemovePreviewTextInputUpdateHandler(this, OnPreviewTextInputUpdate);
        _blinkTimer.Stop();
        _blinkTimer.Tick -= OnBlinkTick;
        DetachTerminal();
        DisableGpuRendering();
        _bitmap = null;
        GC.SuppressFinalize(this);
    }

    internal static bool HasNonEmptySelection(TerminalSelection? selection) =>
        selection is { IsEmpty: false };

    private static void OnTerminalPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var view = (TerminalView)sender;
        if (view._attached)
        {
            view.AttachTerminal((Terminal?)args.NewValue);
        }
        else
        {
            view.UpdateViewportProperties();
        }
    }

    private static void OnTerminalThemePropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var view = (TerminalView)sender;
        if (view._controller is not null)
        {
            view._controller.Theme = (TerminalTheme)args.NewValue;
        }
        view.InvalidatePresentation();
    }

    private static void OnRenderOptionsPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var view = (TerminalView)sender;
        if (view._controller is not null)
        {
            view._controller.Options = (TerminalRenderOptions)args.NewValue;
        }
    }

    private static void OnShowRenderingDebugOverlayPropertyChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs args)
    {
        var view = (TerminalView)sender;
        if (view._backend is not null)
        {
            view._backend.ShowRenderingDebugOverlay = (bool)args.NewValue;
        }
        view.InvalidatePresentation();
    }

    private static void OnPaddingPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        _ = args;
        ((TerminalView)sender).SchedulePrepareFrame();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        if (_disposed)
        {
            return;
        }
        _attached = true;
        TryEnableGpuRendering();
        AttachTerminal(Terminal);
        _blinkTimer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        _attached = false;
        _pressedKeys.Clear();
        _suppressedReleases.Clear();
        _blinkTimer.Stop();
        DetachTerminal();
    }

    private void AttachTerminal(Terminal? terminal)
    {
        DetachTerminal();
        if (terminal is null || terminal.IsDisposed || !_attached || _disposed)
        {
            UpdateViewportProperties();
            return;
        }
        _backend = new SkiaTerminalRenderBackend
        {
            ShowRenderingDebugOverlay = ShowRenderingDebugOverlay
        };
        _controller = new TerminalRenderController(terminal, _backend, RenderOptions, TerminalTheme);
        _controller.Invalidated += OnControllerInvalidated;
        terminal.SelectionChanged += OnTerminalSelectionChanged;
        _controller.IsFocused = IsKeyboardFocusWithin;
        UpdateViewportProperties();
        SchedulePrepareFrame();
    }

    private void DetachTerminal()
    {
        _prepareCancellation?.Cancel();
        _linkCancellation?.Cancel();
        _selecting = false;
        ReleaseMouseCapture();
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
        SetActiveRenderMode(SkiaRenderMode.Unknown);
        _bitmap = null;
        if (!_disposed)
        {
            InvalidatePresentation();
        }
    }

    private void OnControllerInvalidated(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;
        SchedulePrepareFrame();
    }

    private void OnTerminalSelectionChanged(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;
        PostToUiThread(() => SelectionChanged?.Invoke(this, EventArgs.Empty));
    }

    private void SchedulePrepareFrame()
    {
        if (!_attached || _disposed || _controller is null || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }
        if (!Dispatcher.CheckAccess())
        {
            PostToUiThread(SchedulePrepareFrame);
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
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(PrepareFrameAsync));
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
        if (controller is null || terminal is null || ActualWidth <= 0 || ActualHeight <= 0)
        {
            Interlocked.Exchange(ref _preparing, 0);
            return;
        }

        var cancellation = new CancellationTokenSource();
        _prepareCancellation = cancellation;
        try
        {
            Thickness padding = Padding;
            DpiScale dpi = VisualTreeHelper.GetDpi(this);
            double scale = Math.Max(0.01, dpi.DpiScaleX);
            var viewport = new TerminalViewport(
                ActualWidth,
                ActualHeight,
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
            int columns = Math.Max(2, (int)Math.Floor(
                (ActualWidth - padding.Left - padding.Right) / frame.Metrics.CellWidth));
            int rows = Math.Max(1, (int)Math.Floor(
                (ActualHeight - padding.Top - padding.Bottom) / frame.Metrics.CellHeight));
            if (columns != _lastColumns || rows != _lastRows)
            {
                _lastColumns = columns;
                _lastRows = rows;
                await terminal.ResizeAsync(columns, rows, cancellation.Token);
            }
            if (!ReferenceEquals(previousFrame, frame) && !frame.Damage.IsEmpty)
            {
                InvalidatePresentation();
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
                "Failed to prepare a WPF terminal frame.",
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
        UpdateViewportProperties();
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
                QueueLinkUpdate(_lastPointerPosition, Keyboard.Modifiers);
            }
        }
    }

    private void UpdateViewportProperties()
    {
        SetValue(ScrollValuePropertyKey, _frame?.ViewportY ?? 0);
        SetValue(ScrollMaximumPropertyKey, _frame?.BaseY ?? 0);
        SetValue(ColumnsPropertyKey, _frame?.Columns ?? Terminal?.Columns ?? 0);
        SetValue(RowsPropertyKey, _frame?.Rows ?? Terminal?.Rows ?? 0);
    }

    private void OnBlinkTick(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;
        _cursorPhase = !_cursorPhase;
        _blinkPhase = !_blinkPhase;
        _controller?.SetBlinkPhases(_cursorPhase, _blinkPhase);
    }

    private void EnsureBitmap()
    {
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        int width = Math.Max(1, (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX));
        int height = Math.Max(1, (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY));
        if (_bitmap?.PixelWidth == width && _bitmap.PixelHeight == height)
        {
            return;
        }
        _bitmap = new WriteableBitmap(
            width,
            height,
            dpi.PixelsPerInchX,
            dpi.PixelsPerInchY,
            PixelFormats.Pbgra32,
            null);
    }

    private void PostToUiThread(Action action)
    {
        if (_disposed || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }
        if (Dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Normal, action);
        }
    }

    private Task<T> InvokeOnUiThreadAsync<T>(Func<T> action, CancellationToken cancellationToken)
    {
        if (_disposed || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return Task.FromException<T>(new InvalidOperationException(
                "The terminal view has no live UI dispatcher."));
        }
        if (Dispatcher.CheckAccess())
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(action());
        }
        return Dispatcher.InvokeAsync(action, DispatcherPriority.Normal, cancellationToken).Task;
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
}

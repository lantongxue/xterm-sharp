using System.ComponentModel;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SkiaSharp;
using Windows.Foundation;

namespace XtermSharp.WinUI.Controls;

/// <summary>Interactive WinUI 3 terminal control backed by the XtermSharp Skia renderer.</summary>
[DefaultEvent(nameof(SelectionChanged))]
[DefaultProperty(nameof(Terminal))]
public sealed partial class TerminalView : Control, IDisposable
{
    public static readonly DependencyProperty TerminalProperty = DependencyProperty.Register(
        nameof(Terminal),
        typeof(Terminal),
        typeof(TerminalView),
        new PropertyMetadata(null, OnTerminalPropertyChanged));

    public static readonly DependencyProperty TerminalThemeProperty = DependencyProperty.Register(
        nameof(TerminalTheme),
        typeof(TerminalTheme),
        typeof(TerminalView),
        new PropertyMetadata(TerminalTheme.Default, OnTerminalThemePropertyChanged));

    public static readonly DependencyProperty RenderOptionsProperty = DependencyProperty.Register(
        nameof(RenderOptions),
        typeof(TerminalRenderOptions),
        typeof(TerminalView),
        new PropertyMetadata(new TerminalRenderOptions(), OnRenderOptionsPropertyChanged));

    public static readonly DependencyProperty ScrollValueProperty = DependencyProperty.Register(
        nameof(ScrollValue),
        typeof(int),
        typeof(TerminalView),
        new PropertyMetadata(0));

    public static readonly DependencyProperty ScrollMaximumProperty = DependencyProperty.Register(
        nameof(ScrollMaximum),
        typeof(int),
        typeof(TerminalView),
        new PropertyMetadata(0));

    public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(
        nameof(Columns),
        typeof(int),
        typeof(TerminalView),
        new PropertyMetadata(0));

    public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(
        nameof(Rows),
        typeof(int),
        typeof(TerminalView),
        new PropertyMetadata(0));

    private readonly DispatcherQueueTimer _blinkTimer;
    private readonly HashSet<Windows.System.VirtualKey> _pressedKeys = [];
    private readonly HashSet<Windows.System.VirtualKey> _suppressedReleases = [];
    private readonly WinUIClipboardProvider _clipboardProvider;
    private SkiaTerminalRenderBackend? _backend;
    private TerminalRenderController? _controller;
    private TerminalRenderFrame? _frame;
    private TerminalRenderFrame? _presentedFrame;
    private Image? _image;
    private WriteableBitmap? _bitmap;
    private SKBitmap? _skiaBitmap;
    private CancellationTokenSource? _prepareCancellation;
    private CancellationTokenSource? _linkCancellation;
    private TerminalLink? _hoveredLink;
    private TerminalLink? _pressedLink;
    private TerminalLinkEvent? _lastLinkEvent;
    private InputCursor? _textCursor;
    private InputCursor? _handCursor;
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
    private bool _attached;
    private bool _disposed;
    private TerminalPoint _selectionAnchor;

    public TerminalView()
    {
        DefaultStyleKey = typeof(TerminalView);
        IsTabStop = true;
        AutomationProperties.SetName(this, "Terminal");
        _clipboardProvider = new WinUIClipboardProvider(this);
        _blinkTimer = DispatcherQueue.CreateTimer();
        _blinkTimer.Interval = TimeSpan.FromMilliseconds(500);
        _blinkTimer.IsRepeating = true;
        _blinkTimer.Tick += OnBlinkTick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
        GotFocus += OnGotFocus;
        LostFocus += OnLostFocus;
        CharacterReceived += OnCharacterReceived;
        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;
        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerWheelChanged += OnPointerWheelChanged;
        _textCursor = InputSystemCursor.Create(InputSystemCursorShape.IBeam);
        _handCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
        ProtectedCursor = _textCursor;
        InitializeTextInput();
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

    /// <summary>Copies the selected terminal text to the WinUI clipboard.</summary>
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
            await _clipboardProvider.WriteTextAsync("c", text, cancellationToken).ConfigureAwait(false);
        }
        return text;
    }

    /// <summary>Pastes supplied text, or current clipboard text, into the terminal.</summary>
    public async ValueTask PasteAsync(string? text = null, CancellationToken cancellationToken = default)
    {
        Terminal? terminal = Terminal;
        if (terminal is null)
        {
            return;
        }
        text ??= await _clipboardProvider.ReadTextAsync("c", cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(text))
        {
            await terminal.PasteAsync(text, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Scrolls the active viewport to an absolute buffer line.</summary>
    public ValueTask ScrollToLineAsync(int line, CancellationToken cancellationToken = default) =>
        Terminal?.ScrollToLineAsync(line, cancellationToken) ?? ValueTask.CompletedTask;

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _image = GetTemplateChild("PART_Image") as Image;
        if (_image is not null && _bitmap is not null)
        {
            _image.Source = _bitmap;
        }
        RenderCurrentFrame();
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
        SizeChanged -= OnSizeChanged;
        GotFocus -= OnGotFocus;
        LostFocus -= OnLostFocus;
        CharacterReceived -= OnCharacterReceived;
        KeyDown -= OnKeyDown;
        KeyUp -= OnKeyUp;
        PointerEntered -= OnPointerEntered;
        PointerExited -= OnPointerExited;
        PointerPressed -= OnPointerPressed;
        PointerMoved -= OnPointerMoved;
        PointerReleased -= OnPointerReleased;
        PointerWheelChanged -= OnPointerWheelChanged;
        _blinkTimer.Stop();
        _blinkTimer.Tick -= OnBlinkTick;
        DisposeTextInput();
        DetachTerminal();
        DisposeBitmaps();
        _textCursor?.Dispose();
        _handCursor?.Dispose();
        _textCursor = null;
        _handCursor = null;
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
    }

    private static void OnRenderOptionsPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var view = (TerminalView)sender;
        if (view._controller is not null)
        {
            view._controller.Options = (TerminalRenderOptions)args.NewValue;
        }
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
        if (XamlRoot is not null)
        {
            XamlRoot.Changed += OnXamlRootChanged;
        }
        AttachTerminal(Terminal);
        _blinkTimer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        if (XamlRoot is not null)
        {
            XamlRoot.Changed -= OnXamlRootChanged;
        }
        _attached = false;
        _pressedKeys.Clear();
        _suppressedReleases.Clear();
        _blinkTimer.Stop();
        DetachTerminal();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs args)
    {
        _ = sender;
        _ = args;
        DisposeBitmaps();
        SchedulePrepareFrame();
    }

    private void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        _ = sender;
        _ = args;
        DisposeBitmaps();
        SchedulePrepareFrame();
    }

    private void AttachTerminal(Terminal? terminal)
    {
        DetachTerminal();
        if (terminal is null || terminal.IsDisposed || !_attached || _disposed)
        {
            UpdateViewportProperties();
            return;
        }
        _backend = new SkiaTerminalRenderBackend();
        _controller = new TerminalRenderController(terminal, _backend, RenderOptions, TerminalTheme);
        _controller.Invalidated += OnControllerInvalidated;
        terminal.SelectionChanged += OnTerminalSelectionChanged;
        _controller.IsFocused = FocusState != FocusState.Unfocused;
        UpdateViewportProperties();
        SchedulePrepareFrame();
    }

    private void DetachTerminal()
    {
        _prepareCancellation?.Cancel();
        _linkCancellation?.Cancel();
        _selecting = false;
        ReleasePointerCaptures();
        _pressedLink = null;
        ResetMouseMoveDeduplication();
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
        DisposeBitmaps();
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
        if (_disposed)
        {
            return;
        }

        // Terminal invalidations arrive on its processor task. Keep one dispatcher item in
        // flight for a burst instead of adding one UI work item per output event.
        Interlocked.Exchange(ref _prepareAgain, 1);
        if (Interlocked.Exchange(ref _prepareScheduled, 1) != 0)
        {
            return;
        }

        if (DispatcherQueue.HasThreadAccess)
        {
            PrepareFrameAsync();
            return;
        }

        if (!DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, PrepareFrameAsync))
        {
            Interlocked.Exchange(ref _prepareScheduled, 0);
            Interlocked.Exchange(ref _prepareAgain, 0);
        }
    }

    private async void PrepareFrameAsync()
    {
        if (Interlocked.Exchange(ref _preparing, 1) != 0)
        {
            return;
        }
        Interlocked.Exchange(ref _prepareAgain, 0);
        TerminalRenderController? controller = _controller;
        Terminal? terminal = Terminal;
        if (!_attached || controller is null || terminal is null || ActualWidth <= 0 || ActualHeight <= 0)
        {
            CompletePrepareFrame();
            return;
        }

        var cancellation = new CancellationTokenSource();
        _prepareCancellation = cancellation;
        try
        {
            Thickness padding = Padding;
            double scale = Math.Max(0.01, XamlRoot?.RasterizationScale ?? 1);
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
                RenderCurrentFrame();
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
                "Failed to prepare a WinUI terminal frame.",
                exception);
        }
        finally
        {
            if (ReferenceEquals(_prepareCancellation, cancellation))
            {
                _prepareCancellation = null;
            }
            cancellation.Dispose();
            CompletePrepareFrame();
        }
    }

    private void CompletePrepareFrame()
    {
        Interlocked.Exchange(ref _preparing, 0);
        Interlocked.Exchange(ref _prepareScheduled, 0);
        if (Interlocked.Exchange(ref _prepareAgain, 0) != 0)
        {
            SchedulePrepareFrame();
        }
    }

    private void RenderCurrentFrame()
    {
        TerminalRenderFrame? frame = _frame;
        SkiaTerminalRenderBackend? backend = _backend;
        if (_disposed || frame is null || backend is null || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }
        try
        {
            bool bitmapCreated = EnsureBitmaps();
            SKBitmap skiaBitmap = _skiaBitmap!;
            WriteableBitmap bitmap = _bitmap!;
            bool fullRedraw = bitmapCreated || IsFullDamage(frame.Damage, frame.Rows) ||
                !CanApplyDamage(_presentedFrame, frame);
            (int startRow, int endRow) = GetDamagePixelRows(frame, bitmap.PixelHeight, fullRedraw);
            if (startRow == endRow)
            {
                _presentedFrame = frame;
                return;
            }
            using var canvas = new SKCanvas(skiaBitmap);
            if (fullRedraw)
            {
                TerminalRgbaColor background = TerminalTheme.Background;
                canvas.Clear(new SKColor(background.Red, background.Green, background.Blue, background.Alpha));
                canvas.Scale((float)frame.Viewport.RenderScale);
                backend.Render(canvas, frame);
            }
            else
            {
                canvas.Save();
                canvas.Scale((float)frame.Viewport.RenderScale);
                double scale = frame.Viewport.RenderScale;
                canvas.ClipRect(new SKRect(
                    0,
                    (float)(startRow / scale),
                    (float)frame.Viewport.Width,
                    (float)(endRow / scale)));
                backend.Render(canvas, frame);
                canvas.Restore();
            }
            canvas.Flush();
            CopyBitmapRows(skiaBitmap, bitmap, startRow, endRow);
            bitmap.Invalidate();
            if (_image is not null && !ReferenceEquals(_image.Source, bitmap))
            {
                _image.Source = bitmap;
            }
            _presentedFrame = frame;
        }
        catch (Exception exception)
        {
            Terminal?.Options.Logger?.Log(
                TerminalLogLevel.Error,
                "Failed to present a WinUI terminal frame.",
                exception);
        }
    }

    private bool EnsureBitmaps()
    {
        double scale = Math.Max(0.01, XamlRoot?.RasterizationScale ?? 1);
        int width = Math.Max(1, (int)Math.Ceiling(ActualWidth * scale));
        int height = Math.Max(1, (int)Math.Ceiling(ActualHeight * scale));
        if (_bitmap?.PixelWidth == width && _bitmap.PixelHeight == height &&
            _skiaBitmap?.Width == width && _skiaBitmap.Height == height)
        {
            return false;
        }
        DisposeBitmaps();
        _bitmap = new WriteableBitmap(width, height);
        _skiaBitmap = new SKBitmap(new SKImageInfo(
            width,
            height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul));
        if (_image is not null)
        {
            _image.Source = _bitmap;
        }
        return true;
    }

    private static bool IsFullDamage(TerminalDamage damage, int rows) =>
        !damage.IsEmpty && damage.StartRow <= 0 && damage.EndRow >= rows - 1;

    private static bool CanApplyDamage(TerminalRenderFrame? previous, TerminalRenderFrame current)
    {
        if (previous is null || previous.Viewport != current.Viewport ||
            previous.Columns != current.Columns || previous.Rows != current.Rows ||
            previous.ViewportY != current.ViewportY || previous.BaseY != current.BaseY ||
            previous.DisplayList.Rows.Length != current.DisplayList.Rows.Length)
        {
            return false;
        }

        for (int row = 0; row < current.DisplayList.Rows.Length; row++)
        {
            if (row >= current.Damage.StartRow && row <= current.Damage.EndRow)
            {
                continue;
            }
            if (!ReferenceEquals(previous.DisplayList.Rows[row], current.DisplayList.Rows[row]))
            {
                return false;
            }
        }
        return true;
    }

    internal static (int Start, int End) GetDamagePixelRows(
        TerminalRenderFrame frame,
        int pixelHeight,
        bool fullRedraw)
    {
        if (pixelHeight <= 0 || !fullRedraw && frame.Damage.IsEmpty)
        {
            return (0, 0);
        }
        if (fullRedraw)
        {
            return (0, pixelHeight);
        }

        double scale = frame.Viewport.RenderScale;
        int firstRow = Math.Max(0, frame.Damage.StartRow - 1);
        int lastRow = Math.Min(frame.Rows - 1, frame.Damage.EndRow + 1);
        double top = frame.Viewport.Padding.Top + firstRow * frame.Metrics.CellHeight;
        double bottom = frame.Viewport.Padding.Top + (lastRow + 1) * frame.Metrics.CellHeight;
        int start = Math.Clamp((int)Math.Floor(top * scale), 0, pixelHeight);
        int end = Math.Clamp((int)Math.Ceiling(bottom * scale), start, pixelHeight);
        return (start, end);
    }

    private static void CopyBitmapRows(SKBitmap source, WriteableBitmap destination, int startRow, int endRow)
    {
        int destinationStride = destination.PixelWidth * sizeof(uint);
        int sourceStride = source.RowBytes;
        ReadOnlySpan<byte> pixels = source.GetPixelSpan();
        using Stream pixelStream = destination.PixelBuffer.AsStream();
        for (int row = startRow; row < endRow; row++)
        {
            pixelStream.Position = (long)row * destinationStride;
            pixelStream.Write(pixels.Slice(row * sourceStride, destinationStride));
        }
    }

    private void DisposeBitmaps()
    {
        _presentedFrame = null;
        if (_image is not null)
        {
            _image.Source = null;
        }
        _skiaBitmap?.Dispose();
        _skiaBitmap = null;
        _bitmap = null;
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
        bool mouseProtocolChanged = frame is null || previousFrame is null ||
            previousFrame.Modes?.MouseTracking != frame.Modes?.MouseTracking ||
            previousFrame.Modes?.MouseEncoding != frame.Modes?.MouseEncoding;
        if (mouseProtocolChanged)
        {
            ResetMouseMoveDeduplication();
        }
        if (linkCoordinatesChanged)
        {
            ClearHoveredLink(_lastLinkEvent);
            if (_pointerInside && frame is not null)
            {
                QueueLinkUpdate(_lastPointerPosition, WinUIKeyMapper.GetModifiers());
            }
        }
    }

    private void UpdateViewportProperties()
    {
        SetValue(ScrollValueProperty, _frame?.ViewportY ?? 0);
        SetValue(ScrollMaximumProperty, _frame?.BaseY ?? 0);
        SetValue(ColumnsProperty, _frame?.Columns ?? Terminal?.Columns ?? 0);
        SetValue(RowsProperty, _frame?.Rows ?? Terminal?.Rows ?? 0);
    }

    private void OnBlinkTick(DispatcherQueueTimer sender, object args)
    {
        _ = sender;
        _ = args;
        _cursorPhase = !_cursorPhase;
        _blinkPhase = !_blinkPhase;
        _controller?.SetBlinkPhases(_cursorPhase, _blinkPhase);
    }

    private void PostToUiThread(Action action)
    {
        if (_disposed)
        {
            return;
        }
        if (DispatcherQueue.HasThreadAccess)
        {
            action();
        }
        else
        {
            _ = DispatcherQueue.TryEnqueue(() => action());
        }
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

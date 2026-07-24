using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Graphics;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using System.Text;
using XtermSharp.Rendering.Skia.Backends;

namespace XtermSharp.Maui.Controls;

/// <summary>A .NET MAUI terminal view rendered by SkiaSharp.</summary>
public sealed class TerminalView : ContentView
{
    public static readonly BindableProperty TerminalProperty = BindableProperty.Create(
        nameof(Terminal),
        typeof(Terminal),
        typeof(TerminalView),
        default(Terminal),
        propertyChanged: static (bindable, oldValue, newValue) =>
            ((TerminalView)bindable).OnTerminalChanged((Terminal?)oldValue, (Terminal?)newValue));

    public static readonly BindableProperty TerminalThemeProperty = BindableProperty.Create(
        nameof(TerminalTheme),
        typeof(TerminalTheme),
        typeof(TerminalView),
        global::XtermSharp.Rendering.Themes.TerminalTheme.Default,
        propertyChanged: static (bindable, _, newValue) =>
            ((TerminalView)bindable).SetTheme((TerminalTheme)newValue));

    public static readonly BindableProperty RenderOptionsProperty = BindableProperty.Create(
        nameof(RenderOptions),
        typeof(TerminalRenderOptions),
        typeof(TerminalView),
        new TerminalRenderOptions(),
        propertyChanged: static (bindable, _, newValue) =>
            ((TerminalView)bindable).SetRenderOptions((TerminalRenderOptions)newValue));

    public static readonly BindableProperty ShowRenderingDebugOverlayProperty = BindableProperty.Create(
        nameof(ShowRenderingDebugOverlay),
        typeof(bool),
        typeof(TerminalView),
        false,
        propertyChanged: static (bindable, _, newValue) =>
            ((TerminalView)bindable).SetShowRenderingDebugOverlay((bool)newValue));

    public static readonly BindableProperty RequestedRenderModeProperty = BindableProperty.Create(
        nameof(RequestedRenderMode),
        typeof(SkiaRenderModePreference),
        typeof(TerminalView),
        SkiaRenderModePreference.Auto,
        propertyChanged: static (bindable, _, newValue) =>
            ((TerminalView)bindable).SetRequestedRenderMode((SkiaRenderModePreference)newValue));

    private static readonly BindablePropertyKey ActiveRenderModePropertyKey = BindableProperty.CreateReadOnly(
        nameof(ActiveRenderMode),
        typeof(SkiaRenderMode),
        typeof(TerminalView),
        SkiaRenderMode.Unknown);

    private static readonly BindablePropertyKey IsGpuAcceleratedPropertyKey = BindableProperty.CreateReadOnly(
        nameof(IsGpuAccelerated),
        typeof(bool),
        typeof(TerminalView),
        false);

    public static readonly BindableProperty ActiveRenderModeProperty = ActiveRenderModePropertyKey.BindableProperty;
    public static readonly BindableProperty IsGpuAcceleratedProperty = IsGpuAcceleratedPropertyKey.BindableProperty;

    private readonly SKCanvasView _canvasView;
    private readonly SKGLView _gpuView;
    private readonly Entry _inputEntry;
    private SkiaTerminalRenderBackend? _backend;
    private TerminalRenderController? _controller;
    private TerminalRenderFrame? _frame;
    private CancellationTokenSource? _prepareCancellation;
    private IDispatcherTimer? _blinkTimer;
    private TerminalPoint _interactionAnchor;
    private PointF _interactionStart;
    private long? _activeTouchId;
    private bool _interactionDragged;
    private bool _updatingInput;
    private bool _attached;
    private bool _cursorPhase = true;
    private bool _blinkPhase = true;
    private int _lastColumns;
    private int _lastRows;
    private int _prepareScheduled;
    private int _preparing;
    private int _prepareAgain;
    private float _surfaceScaleX = 1;
    private float _surfaceScaleY = 1;
    private bool _gpuFailed;

    public TerminalView()
    {
        _canvasView = new SKCanvasView
        {
            EnableTouchEvents = true,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        _gpuView = new SKGLView
        {
            EnableTouchEvents = true,
            HasRenderLoop = false,
            Opacity = 0,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        _inputEntry = new Entry
        {
            Text = MauiTextInputTranslator.Sentinel,
            Keyboard = Keyboard.Plain,
            ReturnType = ReturnType.Default,
            IsTextPredictionEnabled = false,
            IsSpellCheckEnabled = false,
            Opacity = 0.01,
            WidthRequest = 1,
            HeightRequest = 1,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.End
        };
        SemanticProperties.SetDescription(_inputEntry, "Terminal input");

        var layout = new Grid();
        layout.Add(_canvasView);
        layout.Add(_gpuView);
        layout.Add(_inputEntry);
        Content = layout;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += (_, _) => SchedulePrepareFrame();
        _canvasView.PaintSurface += OnPaintSurface;
        _canvasView.Touch += OnTouch;
        _gpuView.PaintSurface += OnGpuPaintSurface;
        _gpuView.Touch += OnTouch;
        _inputEntry.TextChanged += OnInputTextChanged;
        _inputEntry.Completed += OnInputCompleted;
        _inputEntry.Focused += OnInputFocused;
        _inputEntry.Unfocused += OnInputUnfocused;
    }

    public Terminal? Terminal
    {
        get => (Terminal?)GetValue(TerminalProperty);
        set => SetValue(TerminalProperty, value);
    }

    public TerminalTheme TerminalTheme
    {
        get => (TerminalTheme)GetValue(TerminalThemeProperty);
        set => SetValue(TerminalThemeProperty, value);
    }

    public TerminalRenderOptions RenderOptions
    {
        get => (TerminalRenderOptions)GetValue(RenderOptionsProperty);
        set => SetValue(RenderOptionsProperty, value);
    }

    /// <summary>Gets or sets whether rendering telemetry is drawn over the terminal.</summary>
    public bool ShowRenderingDebugOverlay
    {
        get => (bool)GetValue(ShowRenderingDebugOverlayProperty);
        set => SetValue(ShowRenderingDebugOverlayProperty, value);
    }

    /// <summary>Gets or sets the preferred CPU/GPU presentation path.</summary>
    public SkiaRenderModePreference RequestedRenderMode
    {
        get => (SkiaRenderModePreference)GetValue(RequestedRenderModeProperty);
        set => SetValue(RequestedRenderModeProperty, value);
    }

    /// <summary>Gets how the most recently presented frame was rendered.</summary>
    public SkiaRenderMode ActiveRenderMode => (SkiaRenderMode)GetValue(ActiveRenderModeProperty);

    /// <summary>Gets whether the most recently presented frame used a GPU-backed Skia surface.</summary>
    public bool IsGpuAccelerated => (bool)GetValue(IsGpuAcceleratedProperty);

    public TerminalSelection? Selection => _controller?.Selection;
    public bool HasSelection => Selection is { IsEmpty: false };
    public int ScrollValue => _frame?.ViewportY ?? 0;
    public int ScrollMaximum => _frame?.BaseY ?? 0;
    public int Columns => _frame?.Columns ?? Terminal?.Columns ?? 0;
    public int Rows => _frame?.Rows ?? Terminal?.Rows ?? 0;

    public event EventHandler? SelectionChanged;

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs args)
    {
        _ = sender;
        SKCanvas canvas = args.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        UpdateSurfaceScale(args.Info.Width, args.Info.Height, _canvasView);
        float scaleX = Volatile.Read(ref _surfaceScaleX);
        float scaleY = Volatile.Read(ref _surfaceScaleY);

        SkiaTerminalRenderBackend? backend = _backend;
        TerminalRenderFrame? frame = _frame;
        if (backend is not null && frame is not null)
        {
            canvas.Save();
            try
            {
                canvas.Scale(scaleX, scaleY);
                backend.Render(canvas, frame, SkiaRenderMode.Software);
                SetActiveRenderMode(SkiaRenderMode.Software);
            }
            finally
            {
                canvas.Restore();
            }
        }

    }

    private void OnGpuPaintSurface(object? sender, SKPaintGLSurfaceEventArgs args)
    {
        _ = sender;
        if (_gpuFailed || RequestedRenderMode == SkiaRenderModePreference.Software)
        {
            return;
        }

        SKCanvas canvas = args.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        UpdateSurfaceScale(args.BackendRenderTarget.Width, args.BackendRenderTarget.Height, _gpuView);

        SkiaTerminalRenderBackend? backend = _backend;
        TerminalRenderFrame? frame = _frame;
        if (backend is null || frame is null)
        {
            return;
        }

        try
        {
            canvas.Save();
            try
            {
                canvas.Scale(
                    Math.Max(0.01f, Volatile.Read(ref _surfaceScaleX)),
                    Math.Max(0.01f, Volatile.Read(ref _surfaceScaleY)));
                backend.Render(canvas, frame, SkiaRenderMode.Gpu);
            }
            finally
            {
                canvas.Restore();
            }
            _gpuView.Opacity = 1;
            _canvasView.IsVisible = false;
            SetActiveRenderMode(SkiaRenderMode.Gpu);
        }
        catch (Exception exception)
        {
            DisableGpuRendering(exception);
        }
    }

    private void UpdateSurfaceScale(int pixelWidth, int pixelHeight, VisualElement view)
    {
        float scaleX = view.Width > 0 ? pixelWidth / (float)view.Width : 1;
        float scaleY = view.Height > 0 ? pixelHeight / (float)view.Height : 1;
        scaleX = float.IsFinite(scaleX) && scaleX > 0 ? scaleX : 1;
        scaleY = float.IsFinite(scaleY) && scaleY > 0 ? scaleY : 1;
        float previousScaleX = Volatile.Read(ref _surfaceScaleX);
        float previousScaleY = Volatile.Read(ref _surfaceScaleY);
        Volatile.Write(ref _surfaceScaleX, scaleX);
        Volatile.Write(ref _surfaceScaleY, scaleY);
        if (Math.Abs(previousScaleX - scaleX) > 0.01f || Math.Abs(previousScaleY - scaleY) > 0.01f)
        {
            SchedulePrepareFrame();
        }
    }

    private void DisableGpuRendering(Exception exception)
    {
        _gpuFailed = true;
        Terminal?.Options.Logger?.Log(
            TerminalLogLevel.Warning,
            "GPU presentation failed; falling back to the software Skia surface.",
            exception);
        _gpuView.IsVisible = false;
        _canvasView.IsVisible = true;
        _canvasView.InvalidateSurface();
    }

    private void SetRequestedRenderMode(SkiaRenderModePreference mode)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        if (mode == SkiaRenderModePreference.Software)
        {
            _gpuView.Opacity = 0;
            _gpuView.IsVisible = false;
            _canvasView.IsVisible = true;
            SetActiveRenderMode(SkiaRenderMode.Unknown);
            _canvasView.InvalidateSurface();
            return;
        }

        _gpuFailed = false;
        _gpuView.Opacity = 0;
        _gpuView.IsVisible = true;
        _canvasView.IsVisible = true;
        _canvasView.InvalidateSurface();
        _gpuView.InvalidateSurface();
    }

    private void SetActiveRenderMode(SkiaRenderMode mode)
    {
        if (ActiveRenderMode == mode)
        {
            return;
        }
        SetValue(ActiveRenderModePropertyKey, mode);
        SetValue(IsGpuAcceleratedPropertyKey, mode == SkiaRenderMode.Gpu);
    }

    private void OnTouch(object? sender, SKTouchEventArgs args)
    {
        VisualElement surface = sender as VisualElement ?? _canvasView;
        float scaleX = Math.Max(0.01f, Volatile.Read(ref _surfaceScaleX));
        float scaleY = Math.Max(0.01f, Volatile.Read(ref _surfaceScaleY));
        var position = new PointF(args.Location.X / scaleX, args.Location.Y / scaleY);
        switch (args.ActionType)
        {
            case SKTouchAction.Pressed when _activeTouchId is null:
                _activeTouchId = args.Id;
                OnStartInteraction(position);
                args.Handled = true;
                break;
            case SKTouchAction.Moved when _activeTouchId == args.Id:
                OnDragInteraction(position);
                args.Handled = true;
                break;
            case SKTouchAction.Released when _activeTouchId == args.Id:
                _activeTouchId = null;
                OnEndInteraction(position, IsInsideSurface(position, surface));
                args.Handled = true;
                break;
            case SKTouchAction.Cancelled when _activeTouchId == args.Id:
                _activeTouchId = null;
                OnEndInteraction(position, false);
                args.Handled = true;
                break;
        }
    }

    private static bool IsInsideSurface(PointF position, VisualElement surface) =>
        position.X >= 0 && position.Y >= 0 &&
        position.X <= surface.Width && position.Y <= surface.Height;

    public void ClearSelection() => Terminal?.ClearSelection();

    public async ValueTask SelectAllAsync(CancellationToken cancellationToken = default)
    {
        Terminal? terminal = Terminal;
        if (terminal is null)
        {
            return;
        }
        TerminalSnapshot snapshot = await terminal.GetSnapshotAsync(
            SnapshotScope.ActiveBuffer,
            cancellationToken);
        if (snapshot.ActiveBuffer.Lines.Length != 0)
        {
            terminal.SetSelection(new TerminalSelectionRange(
                0,
                0,
                snapshot.Columns,
                snapshot.ActiveBuffer.Lines.Length - 1));
        }
    }

    public async ValueTask<string> CopySelectionAsync(CancellationToken cancellationToken = default)
    {
        if (_controller is null)
        {
            return string.Empty;
        }
        string text = await _controller.GetSelectedTextAsync(cancellationToken);
        if (text.Length != 0)
        {
            await global::Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard.Default.SetTextAsync(text);
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
        text ??= await global::Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard.Default.GetTextAsync();
        if (!string.IsNullOrEmpty(text))
        {
            await terminal.PasteAsync(text, cancellationToken);
        }
    }

    public ValueTask ScrollToLineAsync(int line, CancellationToken cancellationToken = default) =>
        Terminal?.ScrollToLineAsync(line, cancellationToken) ?? ValueTask.CompletedTask;

    public ValueTask ScrollLinesAsync(int lines, CancellationToken cancellationToken = default) =>
        Terminal?.ScrollLinesAsync(lines, cancellationToken) ?? ValueTask.CompletedTask;

    /// <summary>Scrolls the active buffer by a platform mouse-wheel delta.</summary>
    public ValueTask ScrollWheelAsync(int delta, CancellationToken cancellationToken = default)
    {
        int lines = MauiTerminalInput.GetWheelLines(delta);
        return lines == 0
            ? ValueTask.CompletedTask
            : ScrollLinesAsync(delta > 0 ? -lines : lines, cancellationToken);
    }

    public ValueTask SendKeyAsync(TerminalKeyEvent key, CancellationToken cancellationToken = default) =>
        Terminal?.SendKeyAsync(key, cancellationToken) ?? ValueTask.CompletedTask;

    public new bool Focus() => _inputEntry.Focus();

    private void OnLoaded(object? sender, EventArgs args)
    {
        if (_attached)
        {
            return;
        }
        _attached = true;
        _gpuFailed = false;
        _gpuView.IsVisible = RequestedRenderMode != SkiaRenderModePreference.Software;
        _gpuView.Opacity = 0;
        _canvasView.IsVisible = true;
        AttachTerminal(Terminal);
        _blinkTimer = Dispatcher.CreateTimer();
        _blinkTimer.Interval = TimeSpan.FromMilliseconds(500);
        _blinkTimer.IsRepeating = true;
        _blinkTimer.Tick += OnBlinkTick;
        _blinkTimer.Start();
    }

    private void OnUnloaded(object? sender, EventArgs args)
    {
        if (!_attached)
        {
            return;
        }
        _attached = false;
        if (_blinkTimer is not null)
        {
            _blinkTimer.Stop();
            _blinkTimer.Tick -= OnBlinkTick;
            _blinkTimer = null;
        }
        DetachTerminal();
    }

    private void OnTerminalChanged(Terminal? oldValue, Terminal? newValue)
    {
        _ = oldValue;
        if (_attached)
        {
            AttachTerminal(newValue);
        }
    }

    private void AttachTerminal(Terminal? terminal)
    {
        DetachTerminal();
        if (terminal is null || terminal.IsDisposed)
        {
            return;
        }
        _backend = new SkiaTerminalRenderBackend
        {
            ShowRenderingDebugOverlay = ShowRenderingDebugOverlay
        };
        _controller = new TerminalRenderController(terminal, _backend, RenderOptions, TerminalTheme)
        {
            IsFocused = _inputEntry.IsFocused
        };
        _controller.Invalidated += OnControllerInvalidated;
        terminal.SelectionChanged += OnTerminalSelectionChanged;
        SchedulePrepareFrame();
    }

    private void DetachTerminal()
    {
        _prepareCancellation?.Cancel();
        _prepareCancellation = null;
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
        if (_backend is not null)
        {
            _backend.Dispose();
            _backend = null;
        }
        PublishFrame(null);
        _lastColumns = 0;
        _lastRows = 0;
        SetActiveRenderMode(SkiaRenderMode.Unknown);
        _canvasView.IsVisible = true;
        _gpuView.Opacity = 0;
        _gpuView.IsVisible = RequestedRenderMode != SkiaRenderModePreference.Software;
        _canvasView.InvalidateSurface();
        if (!_gpuFailed && RequestedRenderMode != SkiaRenderModePreference.Software)
        {
            _gpuView.InvalidateSurface();
        }
    }

    private void SetTheme(TerminalTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        if (_controller is not null)
        {
            _controller.Theme = theme;
        }
    }

    private void SetRenderOptions(TerminalRenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (_controller is not null)
        {
            _controller.Options = options;
        }
    }

    private void SetShowRenderingDebugOverlay(bool value)
    {
        if (_backend is not null)
        {
            _backend.ShowRenderingDebugOverlay = value;
        }
        InvalidatePresentation();
    }

    private void OnControllerInvalidated(object? sender, EventArgs args) => SchedulePrepareFrame();

    private void OnTerminalSelectionChanged(object? sender, EventArgs args) =>
        Dispatch(() => SelectionChanged?.Invoke(this, EventArgs.Empty));

    private void SchedulePrepareFrame()
    {
        if (!_attached || _controller is null || Width <= 0 || Height <= 0)
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
        Dispatch(PrepareFrame);
    }

    private void PrepareFrame() => _ = PrepareFrameAsync();

    private async Task PrepareFrameAsync()
    {
        Interlocked.Exchange(ref _prepareScheduled, 0);
        if (Interlocked.Exchange(ref _preparing, 1) != 0)
        {
            Interlocked.Exchange(ref _prepareAgain, 1);
            return;
        }
        TerminalRenderController? controller = _controller;
        Terminal? terminal = Terminal;
        if (controller is null || terminal is null || Width <= 0 || Height <= 0)
        {
            Interlocked.Exchange(ref _preparing, 0);
            return;
        }

        var cancellation = new CancellationTokenSource();
        _prepareCancellation = cancellation;
        try
        {
            Thickness padding = Padding;
            double renderScale = Math.Max(
                1,
                (Volatile.Read(ref _surfaceScaleX) + Volatile.Read(ref _surfaceScaleY)) / 2);
            var viewport = new TerminalViewport(
                Width,
                Height,
                renderScale,
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
            int columns = Math.Max(
                2,
                (int)Math.Floor((Width - padding.Left - padding.Right) / frame.Metrics.CellWidth));
            int rows = Math.Max(
                1,
                (int)Math.Floor((Height - padding.Top - padding.Bottom) / frame.Metrics.CellHeight));
            if (columns != _lastColumns || rows != _lastRows)
            {
                _lastColumns = columns;
                _lastRows = rows;
                await terminal.ResizeAsync(columns, rows, cancellation.Token);
            }
            if (!ReferenceEquals(previousFrame, frame) && !frame.Damage.IsEmpty)
            {
                Dispatch(InvalidatePresentation);
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
                "Failed to prepare a .NET MAUI terminal frame.",
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

    private void PublishFrame(TerminalRenderFrame? frame)
    {
        int previousScrollValue = ScrollValue;
        int previousScrollMaximum = ScrollMaximum;
        int previousColumns = Columns;
        int previousRows = Rows;
        _frame = frame;
        if (previousScrollValue != ScrollValue) OnPropertyChanged(nameof(ScrollValue));
        if (previousScrollMaximum != ScrollMaximum) OnPropertyChanged(nameof(ScrollMaximum));
        if (previousColumns != Columns) OnPropertyChanged(nameof(Columns));
        if (previousRows != Rows) OnPropertyChanged(nameof(Rows));
    }

    private void InvalidatePresentation()
    {
        if (!_gpuFailed && RequestedRenderMode != SkiaRenderModePreference.Software)
        {
            _gpuView.InvalidateSurface();
        }
        _canvasView.InvalidateSurface();
    }

    private void OnStartInteraction(PointF position)
    {
        if (_frame is null || Terminal is null)
        {
            return;
        }
        _inputEntry.Focus();
        _interactionStart = position;
        _interactionAnchor = HitCell(_interactionStart, _frame);
        _interactionDragged = false;
        if ((_frame.Modes?.MouseTracking ?? TerminalMouseTrackingMode.None) != TerminalMouseTrackingMode.None)
        {
            SendMouse(_interactionStart, TerminalMouseAction.Down);
        }
    }

    private void OnDragInteraction(PointF position)
    {
        if (_frame is null || Terminal is null)
        {
            return;
        }
        if ((_frame.Modes?.MouseTracking ?? TerminalMouseTrackingMode.None) != TerminalMouseTrackingMode.None)
        {
            SendMouse(position, TerminalMouseAction.Move);
            return;
        }
        if (!_interactionDragged && DistanceSquared(_interactionStart, position) < 16)
        {
            return;
        }
        _interactionDragged = true;
        TerminalPoint cell = HitCell(position, _frame);
        Terminal.SetSelection(new TerminalSelectionRange(
            (int)_interactionAnchor.X,
            (int)_interactionAnchor.Y,
            (int)cell.X + 1,
            (int)cell.Y));
    }

    private void OnEndInteraction(PointF position, bool isInsideBounds)
    {
        TerminalRenderFrame? frame = _frame;
        if (frame is null || Terminal is null)
        {
            return;
        }
        if ((frame.Modes?.MouseTracking ?? TerminalMouseTrackingMode.None) != TerminalMouseTrackingMode.None)
        {
            SendMouse(position, TerminalMouseAction.Up);
        }
        else if (!_interactionDragged && isInsideBounds)
        {
            Terminal.ClearSelection();
            _ = ActivateLinkAsync(position, frame);
        }
    }

    private async Task ActivateLinkAsync(PointF position, TerminalRenderFrame frame)
    {
        Terminal? terminal = Terminal;
        if (terminal is null)
        {
            return;
        }
        TerminalPoint cell = HitCell(position, frame);
        try
        {
            TerminalLink? link = await terminal.GetLinkAtAsync((int)cell.X + 1, (int)cell.Y + 1);
            if (link is null || !ReferenceEquals(terminal, Terminal))
            {
                return;
            }
            var terminalEvent = new TerminalLinkEvent(
                (int)cell.X + 1,
                (int)cell.Y + 1,
                Math.Max(0, (int)Math.Round(position.X)),
                Math.Max(0, (int)Math.Round(position.Y)),
                TerminalMouseButton.Left,
                TerminalMouseAction.Up);
            link.Activate(terminalEvent, link.Text);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception exception)
        {
            terminal.Options.Logger?.Log(TerminalLogLevel.Error, "Failed to activate a terminal link.", exception);
        }
    }

    private void SendMouse(PointF position, TerminalMouseAction action)
    {
        Terminal? terminal = Terminal;
        TerminalRenderFrame? frame = _frame;
        if (terminal is null || frame is null)
        {
            return;
        }
        TerminalPoint cell = HitCell(position, frame);
        var value = new TerminalMouseEvent(
            (int)cell.X + 1,
            (int)cell.Y - frame.ViewportY + 1,
            Math.Max(0, (int)Math.Round(position.X)),
            Math.Max(0, (int)Math.Round(position.Y)),
            TerminalMouseButton.Left,
            action);
        Observe(terminal.SendMouseAsync(value));
    }

    private void OnInputTextChanged(object? sender, TextChangedEventArgs args)
    {
        if (_updatingInput)
        {
            return;
        }
        MauiTextInput input = MauiTextInputTranslator.Translate(args.NewTextValue);
        switch (input.Kind)
        {
            case MauiTextInputKind.Text when Terminal is not null:
                Observe(Terminal.SendInputAsync(input.Text));
                break;
            case MauiTextInputKind.Backspace when Terminal is not null:
                Observe(Terminal.SendKeyAsync(new TerminalKeyEvent("Backspace", "Backspace", 8)));
                break;
        }
        ResetInputEntry();
    }

    private void OnInputCompleted(object? sender, EventArgs args)
    {
        if (Terminal is not null)
        {
            Observe(Terminal.SendKeyAsync(new TerminalKeyEvent("Enter", "Enter", 13)));
        }
        ResetInputEntry();
        _inputEntry.Focus();
    }

    private void ResetInputEntry()
    {
        _updatingInput = true;
        _inputEntry.Text = MauiTextInputTranslator.Sentinel;
        _inputEntry.CursorPosition = MauiTextInputTranslator.Sentinel.Length;
        _updatingInput = false;
    }

    private void OnInputFocused(object? sender, FocusEventArgs args)
    {
        ResetInputEntry();
        if (_controller is not null)
        {
            _controller.IsFocused = true;
        }
        Observe(Terminal?.SendFocusAsync(true) ?? ValueTask.CompletedTask);
    }

    private void OnInputUnfocused(object? sender, FocusEventArgs args)
    {
        if (_controller is not null)
        {
            _controller.IsFocused = false;
        }
        Observe(Terminal?.SendFocusAsync(false) ?? ValueTask.CompletedTask);
    }

    private void OnBlinkTick(object? sender, EventArgs args)
    {
        _cursorPhase = !_cursorPhase;
        _blinkPhase = !_blinkPhase;
        _controller?.SetBlinkPhases(_cursorPhase, _blinkPhase);
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

    private static float DistanceSquared(PointF first, PointF second)
    {
        float x = first.X - second.X;
        float y = first.Y - second.Y;
        return x * x + y * y;
    }

    private void Dispatch(Action action)
    {
        if (Dispatcher.IsDispatchRequired)
        {
            Dispatcher.Dispatch(action);
        }
        else
        {
            action();
        }
    }

    private void Observe(ValueTask operation)
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

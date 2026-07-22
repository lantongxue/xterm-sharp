using System.Runtime.InteropServices;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;

namespace XtermSharp.WinUI.Controls;

public sealed partial class TerminalView
{
    private long _lastClickTime;
    private TerminalPoint _lastClickCell;
    private int _clickCount;

    private void OnPointerEntered(object sender, PointerRoutedEventArgs args)
    {
        _ = sender;
        _pointerInside = true;
        PointerPoint point = args.GetCurrentPoint(this);
        _lastPointerPosition = point.Position;
        QueueLinkUpdate(point.Position, WinUIKeyMapper.GetModifiers());
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        _pointerInside = false;
        _pressedLink = null;
        ClearHoveredLink(_lastLinkEvent);
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs args)
    {
        _ = sender;
        _ = Focus(FocusState.Pointer);
        Terminal? terminal = Terminal;
        TerminalRenderFrame? frame = _frame;
        if (terminal is null || frame is null)
        {
            return;
        }

        PointerPoint point = args.GetCurrentPoint(this);
        Point position = point.Position;
        TerminalPoint cell = HitCell(position, frame);
        TerminalMouseButton button = MapButton(point, pressed: true);
        TerminalModifiers modifiers = WinUIKeyMapper.GetModifiers();
        TerminalLinkEvent linkEvent = CreateLinkEvent(
            position,
            frame,
            button,
            TerminalMouseAction.Down,
            modifiers);
        _lastLinkEvent = linkEvent;
        if (button == TerminalMouseButton.Left)
        {
            SetPressedLink(linkEvent, frame.Columns);
        }

        TerminalMouseTrackingMode mouseTracking = frame.Modes?.MouseTracking ?? TerminalMouseTrackingMode.None;
        bool localSelection = button == TerminalMouseButton.Left &&
            (modifiers.HasFlag(TerminalModifiers.Shift) ||
             mouseTracking == TerminalMouseTrackingMode.None);
        if (localSelection)
        {
            _selecting = true;
            _selectionAnchor = cell;
            _ = CapturePointer(args.Pointer);
            SendWithoutThrow(BeginSelectionAsync(cell, GetClickCount(cell)));
            args.Handled = true;
        }
        else if (mouseTracking != TerminalMouseTrackingMode.None)
        {
            SendMouse(position, button, TerminalMouseAction.Down, modifiers);
            args.Handled = true;
        }
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs args)
    {
        _ = sender;
        Terminal? terminal = Terminal;
        TerminalRenderFrame? frame = _frame;
        if (terminal is null || frame is null)
        {
            return;
        }
        _pointerInside = true;
        PointerPoint point = args.GetCurrentPoint(this);
        Point position = point.Position;
        _lastPointerPosition = position;
        TerminalModifiers modifiers = WinUIKeyMapper.GetModifiers();
        QueueLinkUpdate(position, modifiers);

        TerminalMouseTrackingMode mouseTracking = frame.Modes?.MouseTracking ?? TerminalMouseTrackingMode.None;
        if (_selecting)
        {
            TerminalPoint cell = HitCell(position, frame);
            terminal.SetSelection(new TerminalSelectionRange(
                (int)_selectionAnchor.X,
                (int)_selectionAnchor.Y,
                (int)cell.X + 1,
                (int)cell.Y));
            args.Handled = true;
        }
        else
        {
            TerminalMouseButton button = PressedButton(point);
            if (mouseTracking == TerminalMouseTrackingMode.Any ||
                mouseTracking == TerminalMouseTrackingMode.Drag && button != TerminalMouseButton.None)
            {
                SendMouse(position, button, TerminalMouseAction.Move, modifiers);
                args.Handled = true;
            }
        }
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs args)
    {
        _ = sender;
        TerminalRenderFrame? frame = _frame;
        PointerPoint point = args.GetCurrentPoint(this);
        Point position = point.Position;
        TerminalMouseButton button = MapButton(point, pressed: false);
        TerminalModifiers modifiers = WinUIKeyMapper.GetModifiers();
        if (frame is not null)
        {
            TerminalLinkEvent linkEvent = CreateLinkEvent(
                position,
                frame,
                button,
                TerminalMouseAction.Up,
                modifiers);
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
            ReleasePointerCapture(args.Pointer);
            args.Handled = true;
        }
        else if (frame is not null &&
                 (frame.Modes?.MouseTracking ?? TerminalMouseTrackingMode.None) != TerminalMouseTrackingMode.None)
        {
            SendMouse(position, button, TerminalMouseAction.Up, modifiers);
            args.Handled = true;
        }
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs args)
    {
        _ = sender;
        Terminal? terminal = Terminal;
        TerminalRenderFrame? frame = _frame;
        PointerPoint point = args.GetCurrentPoint(this);
        int delta = point.Properties.MouseWheelDelta;
        if (terminal is null || frame is null || delta == 0)
        {
            return;
        }
        TerminalModifiers modifiers = WinUIKeyMapper.GetModifiers();
        if ((frame.Modes?.MouseTracking ?? TerminalMouseTrackingMode.None) != TerminalMouseTrackingMode.None &&
            !modifiers.HasFlag(TerminalModifiers.Shift))
        {
            SendMouse(
                point.Position,
                TerminalMouseButton.Wheel,
                delta > 0 ? TerminalMouseAction.Up : TerminalMouseAction.Down,
                modifiers);
        }
        else
        {
            int lines = Math.Max(1, Math.Abs(delta) * 3 / 120);
            SendWithoutThrow(terminal.ScrollLinesAsync(delta > 0 ? -lines : lines));
        }
        args.Handled = true;
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
            ProtectedCursor = _textCursor;
            return;
        }
        InvokeLinkCallback(link.Hover, terminalEvent, link.Text);
        _controller?.SetHoveredLink(link.Decorations.Underline ? link.Range : null);
        ProtectedCursor = link.Decorations.PointerCursor ? _handCursor : _textCursor;
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
        ProtectedCursor = _textCursor;
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

    private void QueueLinkUpdate(Point position, TerminalModifiers modifiers)
    {
        Terminal? terminal = Terminal;
        TerminalRenderFrame? frame = _frame;
        if (!_pointerInside || terminal is null || frame is null)
        {
            return;
        }
        TerminalLinkEvent terminalEvent = CreateLinkEvent(
            position,
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
        Point position,
        TerminalMouseButton button,
        TerminalMouseAction action,
        TerminalModifiers modifiers)
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
            Math.Max(0, (int)Math.Round(position.X)),
            Math.Max(0, (int)Math.Round(position.Y)),
            button,
            action,
            modifiers);
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
            Terminal?.Options.Logger?.Log(
                TerminalLogLevel.Error,
                "A terminal link callback threw an exception.",
                exception);
        }
    }

    private int GetClickCount(TerminalPoint cell)
    {
        long now = Environment.TickCount64;
        if (cell == _lastClickCell && now - _lastClickTime <= GetDoubleClickTime())
        {
            _clickCount = _clickCount % 3 + 1;
        }
        else
        {
            _clickCount = 1;
        }
        _lastClickCell = cell;
        _lastClickTime = now;
        return _clickCount;
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

    private static TerminalLinkEvent CreateLinkEvent(
        Point position,
        TerminalRenderFrame frame,
        TerminalMouseButton button,
        TerminalMouseAction action,
        TerminalModifiers modifiers)
    {
        TerminalPoint cell = HitCell(position, frame);
        return new TerminalLinkEvent(
            (int)cell.X + 1,
            (int)cell.Y + 1,
            Math.Max(0, (int)Math.Round(position.X)),
            Math.Max(0, (int)Math.Round(position.Y)),
            button,
            action,
            modifiers);
    }

    private static TerminalMouseButton MapButton(PointerPoint point, bool pressed)
    {
        PointerUpdateKind update = point.Properties.PointerUpdateKind;
        return update switch
        {
            PointerUpdateKind.LeftButtonPressed or PointerUpdateKind.LeftButtonReleased => TerminalMouseButton.Left,
            PointerUpdateKind.MiddleButtonPressed or PointerUpdateKind.MiddleButtonReleased => TerminalMouseButton.Middle,
            PointerUpdateKind.RightButtonPressed or PointerUpdateKind.RightButtonReleased => TerminalMouseButton.Right,
            PointerUpdateKind.XButton1Pressed or PointerUpdateKind.XButton1Released => TerminalMouseButton.Auxiliary1,
            PointerUpdateKind.XButton2Pressed or PointerUpdateKind.XButton2Released => TerminalMouseButton.Auxiliary2,
            _ when pressed && point.IsInContact => TerminalMouseButton.Left,
            _ => TerminalMouseButton.None
        };
    }

    private static TerminalMouseButton PressedButton(PointerPoint point)
    {
        PointerPointProperties properties = point.Properties;
        if (properties.IsLeftButtonPressed) return TerminalMouseButton.Left;
        if (properties.IsMiddleButtonPressed) return TerminalMouseButton.Middle;
        if (properties.IsRightButtonPressed) return TerminalMouseButton.Right;
        if (properties.IsXButton1Pressed) return TerminalMouseButton.Auxiliary1;
        if (properties.IsXButton2Pressed) return TerminalMouseButton.Auxiliary2;
        return TerminalMouseButton.None;
    }

    private static bool LinksEqual(TerminalLink? first, TerminalLink? second) =>
        ReferenceEquals(first, second) ||
        first is not null && second is not null && first.Text == second.Text && first.Range == second.Range;

    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();
}

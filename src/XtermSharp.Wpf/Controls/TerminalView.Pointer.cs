using System.Windows;
using System.Windows.Input;

namespace XtermSharp.Wpf.Controls;

public sealed partial class TerminalView
{
    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        _ = Focus();
        Terminal? terminal = Terminal;
        TerminalRenderFrame? frame = _frame;
        if (terminal is null || frame is null)
        {
            return;
        }

        Point position = e.GetPosition(this);
        TerminalPoint cell = HitCell(position, frame);
        TerminalMouseButton button = MapButton(e.ChangedButton);
        TerminalLinkEvent linkEvent = CreateLinkEvent(
            position,
            frame,
            button,
            TerminalMouseAction.Down,
            Keyboard.Modifiers);
        _lastLinkEvent = linkEvent;
        if (button == TerminalMouseButton.Left)
        {
            SetPressedLink(linkEvent, frame.Columns);
        }

        TerminalMouseTrackingMode mouseTracking = frame.Modes?.MouseTracking ?? TerminalMouseTrackingMode.None;
        bool localSelection = button == TerminalMouseButton.Left &&
            (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ||
             mouseTracking == TerminalMouseTrackingMode.None);
        if (localSelection)
        {
            _selecting = true;
            _selectionAnchor = cell;
            _ = CaptureMouse();
            SendWithoutThrow(BeginSelectionAsync(cell, Math.Clamp(e.ClickCount, 1, 3)));
            e.Handled = true;
        }
        else if (mouseTracking != TerminalMouseTrackingMode.None)
        {
            SendMouse(position, button, TerminalMouseAction.Down, Keyboard.Modifiers);
            e.Handled = true;
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
        Point position = e.GetPosition(this);
        _lastPointerPosition = position;
        QueueLinkUpdate(position, Keyboard.Modifiers);

        TerminalMouseTrackingMode mouseTracking = frame.Modes?.MouseTracking ?? TerminalMouseTrackingMode.None;
        if (_selecting)
        {
            TerminalPoint cell = HitCell(position, frame);
            terminal.SetSelection(new TerminalSelectionRange(
                (int)_selectionAnchor.X,
                (int)_selectionAnchor.Y,
                (int)cell.X + 1,
                (int)cell.Y));
            e.Handled = true;
        }
        else if (mouseTracking == TerminalMouseTrackingMode.Any ||
                 mouseTracking == TerminalMouseTrackingMode.Drag && e.LeftButton == MouseButtonState.Pressed)
        {
            SendMouse(position, TerminalMouseButton.Left, TerminalMouseAction.Move, Keyboard.Modifiers);
            e.Handled = true;
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        _pointerInside = false;
        _pressedLink = null;
        ClearHoveredLink(_lastLinkEvent);
        base.OnMouseLeave(e);
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        TerminalRenderFrame? frame = _frame;
        Point position = e.GetPosition(this);
        TerminalMouseButton button = MapButton(e.ChangedButton);
        if (frame is not null)
        {
            TerminalLinkEvent linkEvent = CreateLinkEvent(
                position,
                frame,
                button,
                TerminalMouseAction.Up,
                Keyboard.Modifiers);
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
            ReleaseMouseCapture();
            e.Handled = true;
        }
        else if (frame is not null &&
                 (frame.Modes?.MouseTracking ?? TerminalMouseTrackingMode.None) != TerminalMouseTrackingMode.None)
        {
            SendMouse(position, button, TerminalMouseAction.Up, Keyboard.Modifiers);
            e.Handled = true;
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        Terminal? terminal = Terminal;
        TerminalRenderFrame? frame = _frame;
        if (terminal is null || frame is null || e.Delta == 0)
        {
            return;
        }
        Point position = e.GetPosition(this);
        if ((frame.Modes?.MouseTracking ?? TerminalMouseTrackingMode.None) != TerminalMouseTrackingMode.None &&
            !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            SendMouse(
                position,
                TerminalMouseButton.Wheel,
                e.Delta > 0 ? TerminalMouseAction.Up : TerminalMouseAction.Down,
                Keyboard.Modifiers);
        }
        else
        {
            int lines = Math.Max(1, Math.Abs(e.Delta) * 3 / 120);
            SendWithoutThrow(terminal.ScrollLinesAsync(e.Delta > 0 ? -lines : lines));
        }
        e.Handled = true;
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

    private void QueueLinkUpdate(Point position, ModifierKeys modifiers)
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
        ModifierKeys modifiers)
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
            WpfKeyMapper.MapModifiers(modifiers));
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

    private void RestoreLinkCursor()
    {
        if (!_linkCursorApplied)
        {
            return;
        }
        Cursor = _cursorBeforeLink ?? Cursors.IBeam;
        _cursorBeforeLink = null;
        _linkCursorApplied = false;
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
        ModifierKeys modifiers)
    {
        TerminalPoint cell = HitCell(position, frame);
        return new TerminalLinkEvent(
            (int)cell.X + 1,
            (int)cell.Y + 1,
            Math.Max(0, (int)Math.Round(position.X)),
            Math.Max(0, (int)Math.Round(position.Y)),
            button,
            action,
            WpfKeyMapper.MapModifiers(modifiers));
    }

    private static TerminalMouseButton MapButton(MouseButton button) => button switch
    {
        MouseButton.Left => TerminalMouseButton.Left,
        MouseButton.Middle => TerminalMouseButton.Middle,
        MouseButton.Right => TerminalMouseButton.Right,
        MouseButton.XButton1 => TerminalMouseButton.Auxiliary1,
        MouseButton.XButton2 => TerminalMouseButton.Auxiliary2,
        _ => TerminalMouseButton.None
    };

    private static bool LinksEqual(TerminalLink? first, TerminalLink? second) =>
        ReferenceEquals(first, second) ||
        first is not null && second is not null && first.Text == second.Text && first.Range == second.Range;
}

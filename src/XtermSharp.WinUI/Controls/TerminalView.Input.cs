using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Text.Core;

namespace XtermSharp.WinUI.Controls;

public sealed partial class TerminalView
{
    private CoreTextEditContext? _editContext;
    private string _textStore = string.Empty;
    private CoreTextRange _textSelection;
    private bool _composing;

    private void OnKeyDown(object sender, KeyRoutedEventArgs args)
    {
        _ = sender;
        VirtualKey keyCode = args.Key;
        TerminalModifiers modifiers = WinUIKeyMapper.GetModifiers();
        if (WinUIKeyMapper.ShouldCopy(keyCode, modifiers, HasSelection))
        {
            _suppressedReleases.Add(keyCode);
            SendWithoutThrow(CopySelectionAsync());
            args.Handled = true;
            return;
        }
        if (WinUIKeyMapper.ShouldPaste(keyCode, modifiers))
        {
            _suppressedReleases.Add(keyCode);
            SendWithoutThrow(PasteAsync());
            args.Handled = true;
            return;
        }
        if (WinUIKeyMapper.ShouldSelectAll(keyCode, modifiers))
        {
            _suppressedReleases.Add(keyCode);
            SendWithoutThrow(SelectAllAsync());
            args.Handled = true;
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
        TerminalKeyEventType eventType = !_pressedKeys.Add(keyCode)
            ? TerminalKeyEventType.Repeat
            : TerminalKeyEventType.Press;
        string? text = WinUIKeyMapper.GetText(keyCode, modifiers);
        TerminalKeyEvent key = WinUIKeyMapper.Create(keyCode, text, modifiers, eventType);
        if (!WinUIKeyMapper.ShouldUseTextInput(key, enhancedKeyboardMode))
        {
            SendWithoutThrow(terminal.SendKeyAsync(key));
            args.Handled = true;
        }
    }

    private void OnKeyUp(object sender, KeyRoutedEventArgs args)
    {
        _ = sender;
        VirtualKey keyCode = args.Key;
        _pressedKeys.Remove(keyCode);
        if (_suppressedReleases.Remove(keyCode))
        {
            args.Handled = true;
            return;
        }
        Terminal? terminal = Terminal;
        TerminalModes? modes = terminal is null ? null : GetCurrentModes(terminal);
        if (terminal is null || modes is null ||
            modes.KittyKeyboardFlags == TerminalKittyKeyboardFlags.None && !modes.Win32InputMode)
        {
            return;
        }
        TerminalModifiers modifiers = WinUIKeyMapper.GetModifiers();
        TerminalKeyEvent key = WinUIKeyMapper.Create(
            keyCode,
            WinUIKeyMapper.GetText(keyCode, modifiers),
            modifiers,
            TerminalKeyEventType.Release);
        SendWithoutThrow(terminal.SendKeyAsync(key));
        args.Handled = true;
    }

    private void OnCharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs args)
    {
        _ = sender;
        if (_editContext is not null || Terminal is null || args.Character == '\0')
        {
            return;
        }
        SendWithoutThrow(Terminal.SendInputAsync(args.Character.ToString()));
        args.Handled = true;
    }

    private void OnGotFocus(object sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        if (_controller is not null)
        {
            _controller.IsFocused = true;
        }
        _editContext?.NotifyFocusEnter();
        SendWithoutThrow(Terminal?.SendFocusAsync(true) ?? ValueTask.CompletedTask);
    }

    private void OnLostFocus(object sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        _pressedKeys.Clear();
        _suppressedReleases.Clear();
        _composing = false;
        _textStore = string.Empty;
        _textSelection = CreateRange(0, 0);
        _editContext?.NotifyFocusLeave();
        if (_controller is not null)
        {
            _controller.IsFocused = false;
            _controller.SetPreeditText(null);
        }
        SendWithoutThrow(Terminal?.SendFocusAsync(false) ?? ValueTask.CompletedTask);
    }

    private void InitializeTextInput()
    {
        try
        {
            _editContext = CoreTextServicesManager.GetForCurrentView().CreateEditContext();
            _editContext.Name = "Terminal";
            _editContext.InputScope = CoreTextInputScope.Text;
            _editContext.TextRequested += OnTextRequested;
            _editContext.SelectionRequested += OnSelectionRequested;
            _editContext.TextUpdating += OnTextUpdating;
            _editContext.SelectionUpdating += OnSelectionUpdating;
            _editContext.CompositionStarted += OnCompositionStarted;
            _editContext.CompositionCompleted += OnCompositionCompleted;
            _editContext.LayoutRequested += OnLayoutRequested;
        }
        catch (InvalidOperationException)
        {
            _editContext = null;
        }
        catch (COMException)
        {
            _editContext = null;
        }
    }

    private void DisposeTextInput()
    {
        if (_editContext is null)
        {
            return;
        }
        _editContext.TextRequested -= OnTextRequested;
        _editContext.SelectionRequested -= OnSelectionRequested;
        _editContext.TextUpdating -= OnTextUpdating;
        _editContext.SelectionUpdating -= OnSelectionUpdating;
        _editContext.CompositionStarted -= OnCompositionStarted;
        _editContext.CompositionCompleted -= OnCompositionCompleted;
        _editContext.LayoutRequested -= OnLayoutRequested;
        _editContext = null;
    }

    private void OnTextRequested(CoreTextEditContext sender, CoreTextTextRequestedEventArgs args)
    {
        _ = sender;
        CoreTextRange requested = args.Request.Range;
        int start = Math.Clamp(requested.StartCaretPosition, 0, _textStore.Length);
        int end = Math.Clamp(requested.EndCaretPosition, start, _textStore.Length);
        args.Request.Text = _textStore[start..end];
    }

    private void OnSelectionRequested(CoreTextEditContext sender, CoreTextSelectionRequestedEventArgs args)
    {
        _ = sender;
        args.Request.Selection = _textSelection;
    }

    private void OnTextUpdating(CoreTextEditContext sender, CoreTextTextUpdatingEventArgs args)
    {
        _ = sender;
        int start = Math.Clamp(args.Range.StartCaretPosition, 0, _textStore.Length);
        int end = Math.Clamp(args.Range.EndCaretPosition, start, _textStore.Length);
        _textStore = string.Concat(_textStore.AsSpan(0, start), args.Text, _textStore.AsSpan(end));
        _textSelection = ClampRange(args.NewSelection, _textStore.Length);
        args.Result = CoreTextTextUpdatingResult.Succeeded;
        if (_composing)
        {
            _controller?.SetPreeditText(_textStore.Length == 0 ? null : _textStore);
        }
        else
        {
            CommitTextStore();
        }
    }

    private void OnSelectionUpdating(CoreTextEditContext sender, CoreTextSelectionUpdatingEventArgs args)
    {
        _ = sender;
        _textSelection = ClampRange(args.Selection, _textStore.Length);
        args.Result = CoreTextSelectionUpdatingResult.Succeeded;
    }

    private void OnCompositionStarted(CoreTextEditContext sender, CoreTextCompositionStartedEventArgs args)
    {
        _ = sender;
        _ = args;
        _composing = true;
    }

    private void OnCompositionCompleted(CoreTextEditContext sender, CoreTextCompositionCompletedEventArgs args)
    {
        _ = sender;
        _ = args;
        _composing = false;
        CommitTextStore();
    }

    private void OnLayoutRequested(CoreTextEditContext sender, CoreTextLayoutRequestedEventArgs args)
    {
        _ = sender;
        Rect bounds = TransformToVisual(null).TransformBounds(new Rect(0, 0, ActualWidth, ActualHeight));
        TerminalRenderFrame? frame = _frame;
        double left = bounds.X + (frame?.Viewport.Padding.Left ?? Padding.Left);
        double top = bounds.Y + (frame?.Viewport.Padding.Top ?? Padding.Top);
        double height = frame?.Metrics.CellHeight ?? Math.Max(1, ActualHeight);
        var textBounds = new Rect(left, top, Math.Max(1, ActualWidth - Padding.Left - Padding.Right), height);
        CoreTextLayoutBounds layoutBounds = args.Request.LayoutBounds;
        layoutBounds.ControlBounds = bounds;
        layoutBounds.TextBounds = textBounds;
    }

    private void CommitTextStore()
    {
        string text = _textStore;
        _textStore = string.Empty;
        _textSelection = CreateRange(0, 0);
        _controller?.SetPreeditText(null);
        if (Terminal is not null && text.Length != 0)
        {
            SendWithoutThrow(Terminal.SendInputAsync(text));
        }
    }

    private static CoreTextRange ClampRange(CoreTextRange range, int length) => CreateRange(
        Math.Clamp(range.StartCaretPosition, 0, length),
        Math.Clamp(range.EndCaretPosition, 0, length));

    private static CoreTextRange CreateRange(int start, int end) => new()
    {
        StartCaretPosition = start,
        EndCaretPosition = end
    };
}

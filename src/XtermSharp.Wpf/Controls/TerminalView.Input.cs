using System.Windows.Input;

namespace XtermSharp.Wpf.Controls;

public sealed partial class TerminalView
{
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        Key keyCode = WpfKeyMapper.EffectiveKey(e);
        ModifierKeys modifiers = Keyboard.Modifiers;
        if (WpfKeyMapper.ShouldCopy(keyCode, modifiers, HasSelection))
        {
            _suppressedReleases.Add(keyCode);
            SendWithoutThrow(CopySelectionAsync());
            e.Handled = true;
            return;
        }
        if (WpfKeyMapper.ShouldPaste(keyCode, modifiers))
        {
            _suppressedReleases.Add(keyCode);
            SendWithoutThrow(PasteAsync());
            e.Handled = true;
            return;
        }
        if (WpfKeyMapper.ShouldSelectAll(keyCode, modifiers))
        {
            _suppressedReleases.Add(keyCode);
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
        TerminalKeyEventType eventType = e.IsRepeat || !_pressedKeys.Add(keyCode)
            ? TerminalKeyEventType.Repeat
            : TerminalKeyEventType.Press;
        string? text = WpfKeyMapper.GetText(keyCode, modifiers);
        TerminalKeyEvent key = WpfKeyMapper.Create(keyCode, text, modifiers, eventType);
        if (!WpfKeyMapper.ShouldUseTextInput(key, enhancedKeyboardMode))
        {
            SendWithoutThrow(terminal.SendKeyAsync(key));
            e.Handled = true;
        }
    }

    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        base.OnPreviewKeyUp(e);
        Key keyCode = WpfKeyMapper.EffectiveKey(e);
        _pressedKeys.Remove(keyCode);
        if (_suppressedReleases.Remove(keyCode))
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
        ModifierKeys modifiers = Keyboard.Modifiers;
        TerminalKeyEvent key = WpfKeyMapper.Create(
            keyCode,
            WpfKeyMapper.GetText(keyCode, modifiers),
            modifiers,
            TerminalKeyEventType.Release);
        SendWithoutThrow(terminal.SendKeyAsync(key));
        e.Handled = true;
    }

    protected override void OnPreviewTextInput(TextCompositionEventArgs e)
    {
        base.OnPreviewTextInput(e);
        if (Terminal is not null && !string.IsNullOrEmpty(e.Text))
        {
            _controller?.SetPreeditText(null);
            SendWithoutThrow(Terminal.SendInputAsync(e.Text));
            e.Handled = true;
        }
    }

    private void OnPreviewTextInputStart(object sender, TextCompositionEventArgs e)
    {
        _ = sender;
        string text = e.TextComposition.CompositionText;
        _controller?.SetPreeditText(string.IsNullOrEmpty(text) ? null : text);
    }

    private void OnPreviewTextInputUpdate(object sender, TextCompositionEventArgs e)
    {
        _ = sender;
        string text = e.TextComposition.CompositionText;
        _controller?.SetPreeditText(string.IsNullOrEmpty(text) ? null : text);
    }
}

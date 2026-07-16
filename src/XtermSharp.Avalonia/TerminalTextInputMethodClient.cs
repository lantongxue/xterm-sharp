using Avalonia;
using Avalonia.Input.TextInput;
using Avalonia.VisualTree;

namespace XtermSharp.Avalonia;

internal sealed class TerminalTextInputMethodClient(TerminalView view) : TextInputMethodClient
{
    private string _preeditText = string.Empty;
    private TextSelection _selection;

    public override Visual TextViewVisual => view;
    public override bool SupportsPreedit => true;
    public override bool SupportsSurroundingText => false;
    public override string SurroundingText => string.Empty;
    public override Rect CursorRectangle => view.GetCursorRectangle();
    public override TextSelection Selection
    {
        get => _selection;
        set => _selection = value;
    }

    public override void SetPreeditText(string? text)
    {
        SetPreeditText(text, null);
    }

    public override void SetPreeditText(string? text, int? cursorPos)
    {
        _preeditText = text ?? string.Empty;
        view.SetPreeditText(_preeditText);
        _selection = new TextSelection(cursorPos ?? _preeditText.Length, cursorPos ?? _preeditText.Length);
    }

    public void NotifyCursorChanged() => RaiseCursorRectangleChanged();
}

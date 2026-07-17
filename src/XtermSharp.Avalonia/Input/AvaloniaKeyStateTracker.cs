using Avalonia.Input;

namespace XtermSharp.Avalonia.Input;

internal sealed class AvaloniaKeyStateTracker
{
    private readonly HashSet<(PhysicalKey PhysicalKey, Key Key)> _pressedKeys = [];
    private readonly HashSet<(PhysicalKey PhysicalKey, Key Key)> _suppressedReleases = [];

    public TerminalKeyEventType KeyDown(PhysicalKey physicalKey, Key key) =>
        _pressedKeys.Add((physicalKey, key))
            ? TerminalKeyEventType.Press
            : TerminalKeyEventType.Repeat;

    public void SuppressRelease(PhysicalKey physicalKey, Key key) =>
        _suppressedReleases.Add((physicalKey, key));

    public bool KeyUp(PhysicalKey physicalKey, Key key)
    {
        _pressedKeys.Remove((physicalKey, key));
        return _suppressedReleases.Remove((physicalKey, key));
    }

    public void Clear()
    {
        _pressedKeys.Clear();
        _suppressedReleases.Clear();
    }
}

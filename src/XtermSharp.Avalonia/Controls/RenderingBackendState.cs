namespace XtermSharp.Avalonia.Controls;

internal sealed class RenderingBackendState
{
    private int _activeMode;

    public TerminalRenderMode ActiveMode =>
        (TerminalRenderMode)Volatile.Read(ref _activeMode);

    public event Action<RenderingBackendState, TerminalRenderMode>? Changed;

    public void Record(TerminalRenderMode mode)
    {
        if (mode == TerminalRenderMode.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }
        int value = (int)mode;
        if (Interlocked.Exchange(ref _activeMode, value) == value)
        {
            return;
        }
        Changed?.Invoke(this, mode);
    }
}

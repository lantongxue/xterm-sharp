namespace XtermSharp.Maui.Input;

internal sealed class TerminalInputEntry : Entry
{
    public event Action<int>? BackspaceRequested;

    public void RequestBackspace(int count)
    {
        if (count > 0)
        {
            BackspaceRequested?.Invoke(count);
        }
    }
}

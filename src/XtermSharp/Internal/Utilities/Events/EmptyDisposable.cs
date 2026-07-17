namespace XtermSharp.Internal.Utilities.Events;

internal sealed class EmptyDisposable : IDisposable
{
    public static EmptyDisposable Instance { get; } = new();

    private EmptyDisposable()
    {
    }

    public void Dispose()
    {
    }
}

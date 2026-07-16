namespace XtermSharp.Internal;

internal sealed class BufferSet : IDisposable
{
    private readonly int _columns;
    private readonly int _rows;
    private readonly int _scrollback;
    private readonly CellStyle _eraseStyle;
    private bool _disposed;

    public BufferSet(int columns, int rows, int scrollback, CellStyle eraseStyle)
    {
        _columns = columns;
        _rows = rows;
        _scrollback = scrollback;
        _eraseStyle = eraseStyle;
        Normal = CreateNormal();
        Alternate = CreateAlternate();
        Active = Normal;
    }

    public TerminalBuffer Normal { get; private set; }
    public TerminalBuffer Alternate { get; private set; }
    public TerminalBuffer Active { get; private set; }

    public void ActivateNormalBuffer()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (ReferenceEquals(Active, Normal))
        {
            return;
        }
        Normal.CursorX = Alternate.CursorX;
        Normal.CursorY = Alternate.CursorY;
        Alternate.ClearAllMarkers();
        Alternate.Clear(_eraseStyle);
        Active = Normal;
    }

    public void ActivateAlternateBuffer()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (ReferenceEquals(Active, Alternate))
        {
            return;
        }
        Alternate.FillViewportRows(_eraseStyle);
        Alternate.CursorX = Normal.CursorX;
        Alternate.CursorY = Normal.CursorY;
        Active = Alternate;
    }

    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        TerminalBuffer oldNormal = Normal;
        TerminalBuffer oldAlternate = Alternate;
        Normal = CreateNormal();
        Alternate = CreateAlternate();
        Active = Normal;
        oldNormal.Dispose();
        oldAlternate.Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        Normal.Dispose();
        Alternate.Dispose();
    }

    private TerminalBuffer CreateNormal() =>
        new(TerminalBufferKind.Normal, _columns, _rows, _scrollback, _eraseStyle);

    private TerminalBuffer CreateAlternate() =>
        new(TerminalBufferKind.Alternate, _columns, _rows, 0, _eraseStyle);
}

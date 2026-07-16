using XtermSharp.Internal.Utilities;

namespace XtermSharp.Internal;

/// <summary>Coordinates the active buffer's viewport scrolling state.</summary>
internal sealed class BufferService : IDisposable
{
    private readonly Emitter<int> _scrolled = new();

    public BufferService(OptionsService optionsService)
    {
        ArgumentNullException.ThrowIfNull(optionsService);
        TerminalOptions options = optionsService.Options;
        Columns = Math.Max(options.Columns, TerminalDimensions.MinimumColumns);
        Rows = Math.Max(options.Rows, TerminalDimensions.MinimumRows);
        Buffers = new BufferSet(Columns, Rows, options.Scrollback, CellStyle.Default);
    }

    public int Columns { get; }

    public int Rows { get; }

    public BufferSet Buffers { get; }

    public TerminalBuffer Buffer => Buffers.Active;

    public bool IsUserScrolling { get; set; }

    public XtermEvent<int> OnScroll => _scrolled.Event;

    public void Scroll(CellStyle eraseStyle, bool isWrapped = false)
    {
        TerminalBuffer buffer = Buffer;
        int previousBase = buffer.YBase;
        int previousDisplay = buffer.YDisp;
        bool full = buffer.LineCount >= buffer.MaximumLineCount;
        bool wholeViewport = buffer.ScrollTop == 0 && buffer.ScrollBottom == Rows - 1;

        buffer.ScrollUp(1, eraseStyle);
        if (wholeViewport)
        {
            if (IsUserScrolling)
            {
                buffer.YDisp = full ? Math.Max(previousDisplay - 1, 0) : previousDisplay;
                if (full)
                {
                    buffer.YBase = previousBase;
                }
            }
            else
            {
                buffer.YDisp = buffer.YBase;
            }
        }
        else if (!IsUserScrolling)
        {
            buffer.YDisp = buffer.YBase;
        }

        if (isWrapped)
        {
            buffer.GetLine(buffer.YBase + buffer.ScrollBottom).IsWrapped = true;
        }
        _scrolled.Fire(buffer.YDisp);
    }

    public void ScrollLines(int amount, bool suppressScrollEvent = false)
    {
        TerminalBuffer buffer = Buffer;
        if (amount < 0)
        {
            if (buffer.YDisp == 0)
            {
                return;
            }
            IsUserScrolling = true;
        }
        else if (amount + buffer.YDisp >= buffer.YBase)
        {
            IsUserScrolling = false;
        }

        int previousDisplay = buffer.YDisp;
        buffer.YDisp = Math.Clamp(buffer.YDisp + amount, 0, buffer.YBase);
        if (buffer.YDisp != previousDisplay && !suppressScrollEvent)
        {
            _scrolled.Fire(buffer.YDisp);
        }
    }

    public void Reset()
    {
        Buffers.Reset();
        IsUserScrolling = false;
    }

    public void Dispose()
    {
        _scrolled.Dispose();
        Buffers.Dispose();
    }
}

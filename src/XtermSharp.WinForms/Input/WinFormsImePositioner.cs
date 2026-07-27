using System.Runtime.InteropServices;

namespace XtermSharp.WinForms.Input;

internal static class WinFormsImePositioner
{
    private const int WmImeStartComposition = 0x010D;
    private const int WmImeComposition = 0x010F;
    private const int WmImeSetContext = 0x0281;
    private const int WmImeNotify = 0x0282;
    private const uint CfsPoint = 0x0002;
    private const uint CfsExclude = 0x0080;

    public static bool IsPositioningMessage(int message) => message is
        WmImeStartComposition or WmImeComposition or WmImeSetContext or WmImeNotify;

    public static Rectangle GetCursorBounds(TerminalRenderFrame frame)
    {
        double scale = Math.Max(0.01, frame.Viewport.RenderScale);
        int column = Math.Clamp(frame.CursorColumn, 0, Math.Max(0, frame.Columns - 1));
        int row = Math.Clamp(frame.CursorRow, 0, Math.Max(0, frame.Rows - 1));
        double left = frame.Viewport.Padding.Left + column * frame.Metrics.CellWidth;
        double top = frame.Viewport.Padding.Top + row * frame.Metrics.CellHeight;
        int pixelLeft = ToPixel(left, scale);
        int pixelTop = ToPixel(top, scale);
        int pixelRight = Math.Max(pixelLeft + 1, ToPixel(left + frame.Metrics.CellWidth, scale));
        int pixelBottom = Math.Max(pixelTop + 1, ToPixel(top + frame.Metrics.CellHeight, scale));
        return Rectangle.FromLTRB(pixelLeft, pixelTop, pixelRight, pixelBottom);
    }

    public static bool CreateNativeCaret(nint window, int height) =>
        CreateCaret(window, 0, 1, Math.Max(1, height));

    public static void DestroyNativeCaret() => _ = DestroyCaret();

    public static void SetNativeCaretPosition(Point position) => _ = SetCaretPos(position.X, position.Y);

    public static void UpdateImeWindows(nint window, Rectangle cursorBounds)
    {
        nint inputContext = ImmGetContext(window);
        if (inputContext == 0)
        {
            return;
        }

        try
        {
            var composition = new CompositionForm
            {
                Style = CfsPoint,
                CurrentPosition = new NativePoint(cursorBounds.Left, cursorBounds.Top),
                Area = NativeRectangle.FromRectangle(cursorBounds)
            };
            _ = ImmSetCompositionWindow(inputContext, ref composition);

            var candidate = new CandidateForm
            {
                Index = 0,
                Style = CfsExclude,
                CurrentPosition = new NativePoint(cursorBounds.Left, cursorBounds.Bottom),
                Area = NativeRectangle.FromRectangle(cursorBounds)
            };
            _ = ImmSetCandidateWindow(inputContext, ref candidate);
        }
        finally
        {
            _ = ImmReleaseContext(window, inputContext);
        }
    }

    private static int ToPixel(double value, double scale) =>
        Math.Max(0, (int)Math.Round(value * scale, MidpointRounding.AwayFromZero));

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint(int x, int y)
    {
        public int X = x;
        public int Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle(int left, int top, int right, int bottom)
    {
        public int Left = left;
        public int Top = top;
        public int Right = right;
        public int Bottom = bottom;

        public static NativeRectangle FromRectangle(Rectangle value) =>
            new(value.Left, value.Top, value.Right, value.Bottom);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CompositionForm
    {
        public uint Style;
        public NativePoint CurrentPosition;
        public NativeRectangle Area;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CandidateForm
    {
        public uint Index;
        public uint Style;
        public NativePoint CurrentPosition;
        public NativeRectangle Area;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateCaret(nint window, nint bitmap, int width, int height);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyCaret();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCaretPos(int x, int y);

    [DllImport("imm32.dll")]
    private static extern nint ImmGetContext(nint window);

    [DllImport("imm32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ImmReleaseContext(nint window, nint inputContext);

    [DllImport("imm32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ImmSetCompositionWindow(nint inputContext, ref CompositionForm compositionForm);

    [DllImport("imm32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ImmSetCandidateWindow(nint inputContext, ref CandidateForm candidateForm);
}

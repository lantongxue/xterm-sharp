using Windows.System;
using XtermSharp.Rendering.Display;
using XtermSharp.Rendering.Geometry;

namespace XtermSharp.WinUI.Tests;

public sealed class TerminalViewTests
{
    [Fact]
    public void PublicControlSurfaceSupportsWinUIBindingAndExternalOwnership()
    {
        Type type = typeof(TerminalView);

        Assert.NotNull(type.GetProperty(nameof(TerminalView.Terminal))?.SetMethod);
        Assert.NotNull(type.GetProperty(nameof(TerminalView.TerminalTheme))?.SetMethod);
        Assert.NotNull(type.GetProperty(nameof(TerminalView.RenderOptions))?.SetMethod);
        Assert.NotNull(type.GetProperty(nameof(TerminalView.ShowRenderingDebugOverlay))?.SetMethod);
        Assert.NotNull(type.GetProperty(nameof(TerminalView.ActiveRenderMode)));
        Assert.NotNull(type.GetProperty(nameof(TerminalView.IsGpuAccelerated)));
        Assert.NotNull(TerminalView.ShowRenderingDebugOverlayProperty);
        Assert.Null(type.GetProperty(nameof(TerminalView.Columns))?.SetMethod);
        Assert.Null(type.GetProperty(nameof(TerminalView.Rows))?.SetMethod);
        Assert.NotNull(type.GetMethod(nameof(TerminalView.CopySelectionAsync)));
        Assert.NotNull(type.GetMethod(nameof(TerminalView.PasteAsync)));
    }

    [Fact]
    public void KeyMapperProducesBrowserCompatibleCoordinates()
    {
        TerminalKeyEvent arrow = WinUIKeyMapper.Create(
            VirtualKey.Left,
            null,
            TerminalModifiers.Control | TerminalModifiers.Shift,
            TerminalKeyEventType.Press);
        TerminalKeyEvent letter = WinUIKeyMapper.Create(
            VirtualKey.A,
            "a",
            TerminalModifiers.None,
            TerminalKeyEventType.Press);
        TerminalKeyEvent numpad = WinUIKeyMapper.Create(
            VirtualKey.NumberPad7,
            "7",
            TerminalModifiers.None,
            TerminalKeyEventType.Press);

        Assert.Equal("ArrowLeft", arrow.Key);
        Assert.Equal("ArrowLeft", arrow.Code);
        Assert.Equal(37, arrow.KeyCode);
        Assert.Equal(TerminalModifiers.Control | TerminalModifiers.Shift, arrow.Modifiers);
        Assert.Equal("a", letter.Key);
        Assert.Equal("KeyA", letter.Code);
        Assert.Equal(65, letter.KeyCode);
        Assert.Equal("7", numpad.Key);
        Assert.Equal("Numpad7", numpad.Code);
        Assert.Equal(103, numpad.KeyCode);
    }

    [Fact]
    public void KeyMapperDefersTextAndRecognizesClipboardShortcuts()
    {
        TerminalKeyEvent deadKey = WinUIKeyMapper.Create(
            (VirtualKey)0xDE,
            WinUIKeyMapper.DeadKeyMarker,
            TerminalModifiers.None,
            TerminalKeyEventType.Press);
        TerminalKeyEvent backspace = WinUIKeyMapper.Create(
            VirtualKey.Back,
            null,
            TerminalModifiers.None,
            TerminalKeyEventType.Press);
        TerminalKeyEvent altGraph = WinUIKeyMapper.Create(
            VirtualKey.Q,
            "@",
            TerminalModifiers.Control | TerminalModifiers.Alt,
            TerminalKeyEventType.Press);

        Assert.Equal("Dead", deadKey.Key);
        Assert.Null(deadKey.Text);
        Assert.True(WinUIKeyMapper.ShouldUseTextInput(deadKey, enhancedKeyboardMode: false));
        Assert.False(WinUIKeyMapper.ShouldUseTextInput(backspace, enhancedKeyboardMode: false));
        Assert.True(WinUIKeyMapper.ShouldUseTextInput(altGraph, enhancedKeyboardMode: false));
        Assert.True(WinUIKeyMapper.ShouldCopy(VirtualKey.C, TerminalModifiers.Control, hasSelection: true));
        Assert.False(WinUIKeyMapper.ShouldCopy(VirtualKey.C, TerminalModifiers.Control, hasSelection: false));
        Assert.True(WinUIKeyMapper.ShouldPaste(VirtualKey.V, TerminalModifiers.Control));
        Assert.True(WinUIKeyMapper.ShouldPaste(VirtualKey.Insert, TerminalModifiers.Shift));
        Assert.True(WinUIKeyMapper.ShouldSelectAll(VirtualKey.A, TerminalModifiers.Control));
    }

    [Fact]
    public void KeyMapperForwardsCommittedCharactersOutsideImeComposition()
    {
        Assert.True(WinUIKeyMapper.ShouldForwardCommittedCharacter('a', isComposing: false));
        Assert.False(WinUIKeyMapper.ShouldForwardCommittedCharacter('\0', isComposing: false));
        Assert.False(WinUIKeyMapper.ShouldForwardCommittedCharacter('a', isComposing: true));
    }

    [Fact]
    public void DamagePixelRowsCoverAdjacentRowsForRasterSafety()
    {
        var frame = new TerminalRenderFrame(
            1,
            new TerminalViewport(100, 100, 2, new TerminalThickness(0, 5, 0, 0)),
            new TerminalFontMetrics(8, 10, 8, 9, 1, 5),
            12,
            8,
            0,
            0,
            TerminalDisplayList.Empty,
            new TerminalDamage(3, 3));

        Assert.Equal((50, 110), TerminalView.GetDamagePixelRows(frame, 200, fullRedraw: false));
        Assert.Equal((0, 200), TerminalView.GetDamagePixelRows(frame, 200, fullRedraw: true));
    }

    [Fact]
    public void PointerMoveDeduplicationUsesTheActiveMouseProtocolCoordinates()
    {
        var first = new TerminalMouseEvent(
            Column: 4,
            Row: 3,
            PixelX: 40,
            PixelY: 48,
            Button: TerminalMouseButton.None,
            Action: TerminalMouseAction.Move,
            Modifiers: TerminalModifiers.Shift);
        TerminalMouseEvent sameCell = first with { PixelX = 47, PixelY = 55 };

        Assert.True(TerminalView.IsDuplicateMouseMove(first, sameCell, pixelCoordinates: false));
        Assert.False(TerminalView.IsDuplicateMouseMove(first, sameCell, pixelCoordinates: true));
        Assert.True(TerminalView.IsDuplicateMouseMove(first, first, pixelCoordinates: true));
        Assert.False(TerminalView.IsDuplicateMouseMove(
            first,
            first with { Modifiers = TerminalModifiers.None },
            pixelCoordinates: false));
        Assert.False(TerminalView.IsDuplicateMouseMove(
            first,
            first with { Action = TerminalMouseAction.Down },
            pixelCoordinates: false));
    }

    [Fact]
    public async Task ClipboardProviderDispatchesConfiguredOperations()
    {
        Microsoft.UI.Dispatching.DispatcherQueueController controller =
            Microsoft.UI.Dispatching.DispatcherQueueController.CreateOnDedicatedThread();
        try
        {
            string clipboard = "initial";
            int dispatcherThread = 0;
            var provider = new WinUIClipboardProvider(
                controller.DispatcherQueue,
                _ =>
                {
                    dispatcherThread = Environment.CurrentManagedThreadId;
                    return Task.FromResult(clipboard);
                },
                (text, _) =>
                {
                    Assert.Equal(dispatcherThread, Environment.CurrentManagedThreadId);
                    clipboard = text;
                    return Task.CompletedTask;
                });

            Assert.Equal(
                "initial",
                await provider.ReadTextAsync("c", TestContext.Current.CancellationToken));
            await provider.WriteTextAsync("c", "updated", TestContext.Current.CancellationToken);

            Assert.NotEqual(Environment.CurrentManagedThreadId, dispatcherThread);
            Assert.Equal("updated", clipboard);
        }
        finally
        {
            await controller.ShutdownQueueAsync();
        }
    }
}

using Avalonia.Input;
using SkiaSharp;
using XtermSharp.Rendering;

namespace XtermSharp.Avalonia.Tests;

public sealed class TerminalViewTests
{
    [Fact]
    public void TerminalPropertyDoesNotTransferOwnership()
    {
        using var terminal = new Terminal();
        var view = new TerminalView { Terminal = terminal };

        view.Terminal = null;

        Assert.False(terminal.IsDisposed);
    }

    [Fact]
    public async Task OscHyperlinkUsesTerminalViewHoverLeaveAndActivationPipeline()
    {
        await using var terminal = new Terminal(new TerminalOptions { Columns = 8, Rows = 2 });
        await terminal.WriteAsync(
            "\x1b]8;id=view;https://example.com\x1b\\link\x1b]8;;\x1b\\",
            TestContext.Current.CancellationToken);
        TerminalLink link = Assert.IsType<TerminalLink>(await terminal.GetLinkAtAsync(
            2,
            1,
            TestContext.Current.CancellationToken));
        link.Decorations.PointerCursor = false;
        var interactions = new List<string>();
        terminal.HyperlinkHovered += (_, args) => interactions.Add($"hover:{args.Hyperlink.Id}");
        terminal.HyperlinkActivated += (_, args) => interactions.Add($"activate:{args.Hyperlink.Id}");
        terminal.HyperlinkLeft += (_, args) => interactions.Add($"leave:{args.Hyperlink.Id}");
        var view = new TerminalView { Terminal = terminal };
        var move = new TerminalLinkEvent(
            2,
            1,
            10,
            10,
            TerminalMouseButton.None,
            TerminalMouseAction.Move);
        var release = move with
        {
            Button = TerminalMouseButton.Left,
            Action = TerminalMouseAction.Up
        };

        view.SetHoveredLink(link, move);
        view.SetPressedLink(move, terminal.Columns);
        view.TryActivateLink(release, terminal.Columns);
        view.ClearHoveredLink(release);

        Assert.Equal(["hover:view", "activate:view", "leave:view"], interactions);
    }

    [Fact]
    public void RenderingDebugOverlayUsesRollingFrameIntervals()
    {
        var view = new TerminalView();
        var metrics = new RenderingDebugMetrics();

        Assert.False(view.ShowRenderingDebugOverlay);
        view.ShowRenderingDebugOverlay = true;
        RenderingDebugSnapshot snapshot = metrics.RecordFrameTime(10);
        snapshot = metrics.RecordFrameTime(20);
        snapshot = metrics.RecordFrameTime(30);

        Assert.True(view.ShowRenderingDebugOverlay);
        Assert.Equal(3, snapshot.SampleCount);
        Assert.Equal(50, snapshot.FramesPerSecond, 3);
        Assert.Equal(20, snapshot.AverageFrameTimeMilliseconds, 3);
        Assert.Equal(30, snapshot.MaximumFrameTimeMilliseconds, 3);
        Assert.Equal(10, snapshot.MinimumFrameTimeMilliseconds, 3);

        using var bitmap = new SKBitmap(300, 150);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        RenderingDebugOverlay.Draw(canvas, new SKRect(0, 0, 300, 150), snapshot);
        canvas.Flush();

        Assert.True(bitmap.GetPixel(290, 10).Alpha > 0);
        Assert.Equal(0, bitmap.GetPixel(10, 140).Alpha);
    }

    [Fact]
    public void KeyMapperProducesTerminalProtocolCoordinates()
    {
        TerminalKeyEvent key = AvaloniaKeyMapper.Create(
            Key.Left,
            PhysicalKey.ArrowLeft,
            null,
            KeyModifiers.Control | KeyModifiers.Shift,
            TerminalKeyEventType.Press);

        Assert.Equal("ArrowLeft", key.Key);
        Assert.Equal(37, key.KeyCode);
        Assert.Equal(TerminalModifiers.Control | TerminalModifiers.Shift, key.Modifiers);
    }

    [Fact]
    public async Task BackspaceKeyProducesLegacyDelInput()
    {
        using var terminal = new Terminal();
        var data = new List<string>();
        terminal.Data += (_, args) => data.Add(args.Data);
        TerminalKeyEvent key = AvaloniaKeyMapper.Create(
            Key.Back,
            PhysicalKey.Backspace,
            null,
            KeyModifiers.None,
            TerminalKeyEventType.Press);

        await terminal.SendKeyAsync(key, TestContext.Current.CancellationToken);

        Assert.Equal("Backspace", key.Key);
        Assert.Equal(8, key.KeyCode);
        Assert.Equal(["\x7f"], data);
        Assert.False(AvaloniaKeyMapper.ShouldUseTextInput(
            key,
            enhancedKeyboardMode: false,
            isMac: false,
            isWindows: false,
            macOptionIsMeta: false));
    }

    [Fact]
    public async Task CtrlCWithoutTextSelectionProducesEtxInput()
    {
        Assert.False(TerminalView.HasNonEmptySelection(null));
        Assert.False(TerminalView.HasNonEmptySelection(new TerminalSelection(4, 2, 4, 2)));
        Assert.True(TerminalView.HasNonEmptySelection(new TerminalSelection(4, 2, 5, 2)));

        using var terminal = new Terminal();
        var data = new List<string>();
        terminal.Data += (_, args) => data.Add(args.Data);
        TerminalKeyEvent key = AvaloniaKeyMapper.Create(
            Key.C,
            PhysicalKey.C,
            "c",
            KeyModifiers.Control,
            TerminalKeyEventType.Press);

        await terminal.SendKeyAsync(key, TestContext.Current.CancellationToken);

        Assert.Equal("KeyC", key.Code);
        Assert.Equal(67, key.KeyCode);
        Assert.Equal(["\x03"], data);

        data.Clear();
        TerminalKeyEvent alternateLayoutKey = AvaloniaKeyMapper.Create(
            Key.C,
            PhysicalKey.X,
            "c",
            KeyModifiers.Control,
            TerminalKeyEventType.Press);
        await terminal.SendKeyAsync(alternateLayoutKey, TestContext.Current.CancellationToken);

        Assert.Equal("KeyX", alternateLayoutKey.Code);
        Assert.Equal(67, alternateLayoutKey.KeyCode);
        Assert.Equal(["\x03"], data);
    }

    [Fact]
    public void MapperProducesDomCompatibleKeyCodeAndLegacyKeyCodeValues()
    {
        TerminalKeyEvent letter = Map(Key.A, PhysicalKey.A, "a");
        Assert.Equal("a", letter.Key);
        Assert.Equal("KeyA", letter.Code);
        Assert.Equal(65, letter.KeyCode);

        TerminalKeyEvent numpad = Map(Key.NumPad7, PhysicalKey.NumPad7, "7");
        Assert.Equal("7", numpad.Key);
        Assert.Equal("Numpad7", numpad.Code);
        Assert.Equal(103, numpad.KeyCode);

        TerminalKeyEvent modifier = Map(
            Key.RightCtrl,
            PhysicalKey.ControlRight,
            null,
            KeyModifiers.Control);
        Assert.Equal("Control", modifier.Key);
        Assert.Equal("ControlRight", modifier.Code);
        Assert.Equal(17, modifier.KeyCode);

        TerminalKeyEvent media = Map(Key.MediaNextTrack, PhysicalKey.MediaTrackNext, null);
        Assert.Equal("MediaTrackNext", media.Key);
        Assert.Equal("MediaTrackNext", media.Code);
        Assert.Equal(176, media.KeyCode);

        TerminalKeyEvent function = Map(Key.F12, PhysicalKey.F12, null);
        Assert.Equal("F12", function.Key);
        Assert.Equal("F12", function.Code);
        Assert.Equal(123, function.KeyCode);

        TerminalKeyEvent punctuation = Map(Key.OemSemicolon, PhysicalKey.Semicolon, ";");
        Assert.Equal(";", punctuation.Key);
        Assert.Equal("Semicolon", punctuation.Code);
        Assert.Equal(186, punctuation.KeyCode);

        TerminalKeyEvent numpadEnter = Map(Key.Return, PhysicalKey.NumPadEnter, null);
        Assert.Equal("Enter", numpadEnter.Key);
        Assert.Equal("NumpadEnter", numpadEnter.Code);
        Assert.Equal(13, numpadEnter.KeyCode);
    }

    [Fact]
    public void ClipboardAndSelectAllShortcutsMatchBrowserPlatformConventions()
    {
        Assert.True(AvaloniaKeyMapper.ShouldCopy(
            Key.C,
            KeyModifiers.Control,
            isMac: false,
            hasSelection: true));
        Assert.False(AvaloniaKeyMapper.ShouldCopy(
            Key.C,
            KeyModifiers.Control,
            isMac: true,
            hasSelection: true));
        Assert.True(AvaloniaKeyMapper.ShouldCopy(
            Key.C,
            KeyModifiers.Meta,
            isMac: true,
            hasSelection: true));
        Assert.False(AvaloniaKeyMapper.ShouldCopy(
            Key.C,
            KeyModifiers.Meta,
            isMac: true,
            hasSelection: false));
        Assert.True(AvaloniaKeyMapper.ShouldCopy(
            Key.Insert,
            KeyModifiers.Control,
            isMac: false,
            hasSelection: true));

        Assert.True(AvaloniaKeyMapper.ShouldPaste(Key.V, KeyModifiers.Control, isMac: false));
        Assert.True(AvaloniaKeyMapper.ShouldPaste(Key.V, KeyModifiers.Meta, isMac: true));
        Assert.False(AvaloniaKeyMapper.ShouldPaste(Key.V, KeyModifiers.Control, isMac: true));
        Assert.True(AvaloniaKeyMapper.ShouldPaste(Key.Insert, KeyModifiers.Shift, isMac: false));

        Assert.True(AvaloniaKeyMapper.ShouldSelectAll(Key.A, KeyModifiers.Meta, isMac: true));
        Assert.False(AvaloniaKeyMapper.ShouldSelectAll(Key.A, KeyModifiers.Control, isMac: false));
        Assert.False(AvaloniaKeyMapper.ShouldSelectAll(
            Key.A,
            KeyModifiers.Meta | KeyModifiers.Shift,
            isMac: true));
    }

    [Fact]
    public void TextInputRoutingMatchesUpstreamCompositionAndThirdLevelShiftRules()
    {
        TerminalKeyEvent letter = Map(Key.A, PhysicalKey.A, "a");
        Assert.True(UsesTextInput(letter));
        Assert.False(UsesTextInput(letter, enhancedKeyboardMode: true));

        TerminalKeyEvent macOptionLetter = Map(
            Key.E,
            PhysicalKey.E,
            null,
            KeyModifiers.Alt);
        Assert.True(UsesTextInput(macOptionLetter, isMac: true));
        Assert.True(UsesTextInput(macOptionLetter, enhancedKeyboardMode: true, isMac: true));
        Assert.False(UsesTextInput(macOptionLetter, isMac: true, macOptionIsMeta: true));

        TerminalKeyEvent macOptionArrow = Map(
            Key.Left,
            PhysicalKey.ArrowLeft,
            null,
            KeyModifiers.Alt);
        Assert.False(UsesTextInput(macOptionArrow, isMac: true));

        TerminalKeyEvent windowsAltGraphLetter = Map(
            Key.Q,
            PhysicalKey.Q,
            "@",
            KeyModifiers.Control | KeyModifiers.Alt);
        Assert.True(UsesTextInput(windowsAltGraphLetter, isWindows: true));
        Assert.False(UsesTextInput(windowsAltGraphLetter));

        TerminalKeyEvent deadKey = Map(Key.DeadCharProcessed, PhysicalKey.E, null);
        Assert.Equal("Dead", deadKey.Key);
        Assert.True(UsesTextInput(deadKey));
    }

    [Fact]
    public async Task DomCompatibleCodesFeedKittyAndWin32KeyboardProtocols()
    {
        using var terminal = new Terminal();
        var data = new List<string>();
        terminal.Data += (_, args) => data.Add(args.Data);
        await terminal.WriteAsync("\x1b[>3u", TestContext.Current.CancellationToken);

        await terminal.SendKeyAsync(
            Map(Key.NumPad5, PhysicalKey.NumPad5, "5"),
            TestContext.Current.CancellationToken);
        await terminal.SendKeyAsync(
            Map(Key.NumPad5, PhysicalKey.NumPad5, "5", eventType: TerminalKeyEventType.Repeat),
            TestContext.Current.CancellationToken);

        Assert.Equal(["\x1b[57404u", "\x1b[57404;1:2u"], data);

        data.Clear();
        await terminal.WriteAsync("\x1b[?9001h", TestContext.Current.CancellationToken);
        await terminal.SendKeyAsync(
            Map(
                Key.RightCtrl,
                PhysicalKey.ControlRight,
                null,
                KeyModifiers.Control),
            TestContext.Current.CancellationToken);
        await terminal.SendKeyAsync(
            Map(
                Key.RightCtrl,
                PhysicalKey.ControlRight,
                null,
                KeyModifiers.Control,
                TerminalKeyEventType.Release),
            TestContext.Current.CancellationToken);

        Assert.Equal(
            ["\x1b[17;29;0;1;260;1_", "\x1b[17;29;0;0;260;1_"],
            data);
    }

    [Fact]
    public async Task ControlLettersProduceCompleteC0ControlRange()
    {
        using var terminal = new Terminal();
        var data = new List<string>();
        terminal.Data += (_, args) => data.Add(args.Data);

        for (int offset = 0; offset < 26; offset++)
        {
            await terminal.SendKeyAsync(
                Map(
                    (Key)((int)Key.A + offset),
                    (PhysicalKey)((int)PhysicalKey.A + offset),
                    ((char)('a' + offset)).ToString(),
                    KeyModifiers.Control),
                TestContext.Current.CancellationToken);
        }

        Assert.Equal(
            Enumerable.Range(1, 26).Select(value => ((char)value).ToString()),
            data);

        data.Clear();
        (TerminalKeyEvent Key, string Expected)[] controlKeys =
        [
            (Map(Key.Space, PhysicalKey.Space, " ", KeyModifiers.Control), "\0"),
            (Map(Key.D3, PhysicalKey.Digit3, "3", KeyModifiers.Control), "\x1b"),
            (Map(Key.D4, PhysicalKey.Digit4, "4", KeyModifiers.Control), "\x1c"),
            (Map(Key.D5, PhysicalKey.Digit5, "5", KeyModifiers.Control), "\x1d"),
            (Map(Key.D6, PhysicalKey.Digit6, "6", KeyModifiers.Control), "\x1e"),
            (Map(Key.D7, PhysicalKey.Digit7, "7", KeyModifiers.Control), "\x1f"),
            (Map(Key.D8, PhysicalKey.Digit8, "8", KeyModifiers.Control), "\x7f"),
            (Map(Key.OemQuestion, PhysicalKey.Slash, "/", KeyModifiers.Control), "\x1f"),
            (Map(Key.OemOpenBrackets, PhysicalKey.BracketLeft, "[", KeyModifiers.Control), "\x1b"),
            (Map(Key.OemPipe, PhysicalKey.Backslash, "\\", KeyModifiers.Control), "\x1c"),
            (Map(Key.OemCloseBrackets, PhysicalKey.BracketRight, "]", KeyModifiers.Control), "\x1d")
        ];
        foreach ((TerminalKeyEvent key, _) in controlKeys)
        {
            await terminal.SendKeyAsync(key, TestContext.Current.CancellationToken);
        }

        Assert.Equal(controlKeys.Select(value => value.Expected), data);
    }

    [Fact]
    public async Task ShiftPageKeysScrollByOneLessThanTheViewportHeight()
    {
        using var terminal = new Terminal(new TerminalOptions
        {
            Columns = 10,
            Rows = 4,
            Scrollback = 100
        });
        await terminal.WriteAsync(
            string.Join("\r\n", Enumerable.Range(0, 12)) + "\r\n",
            TestContext.Current.CancellationToken);
        int baseY = (await terminal.GetSnapshotAsync(
            SnapshotScope.ActiveBuffer,
            TestContext.Current.CancellationToken)).ActiveBuffer.BaseY;

        await terminal.SendKeyAsync(
            Map(
                Key.PageUp,
                PhysicalKey.PageUp,
                null,
                KeyModifiers.Shift),
            TestContext.Current.CancellationToken);
        TerminalSnapshot pageUp = await terminal.GetSnapshotAsync(
            SnapshotScope.ActiveBuffer,
            TestContext.Current.CancellationToken);
        Assert.Equal(Math.Max(0, baseY - (terminal.Rows - 1)), pageUp.ActiveBuffer.ViewportY);

        await terminal.SendKeyAsync(
            Map(
                Key.PageDown,
                PhysicalKey.PageDown,
                null,
                KeyModifiers.Shift),
            TestContext.Current.CancellationToken);
        TerminalSnapshot pageDown = await terminal.GetSnapshotAsync(
            SnapshotScope.ActiveBuffer,
            TestContext.Current.CancellationToken);
        Assert.Equal(baseY, pageDown.ActiveBuffer.ViewportY);
    }

    [Fact]
    public async Task Win32ModifierOnlyEventsDoNotScrollToBottom()
    {
        using var terminal = new Terminal(new TerminalOptions
        {
            Columns = 10,
            Rows = 4,
            Scrollback = 100
        });
        await terminal.WriteAsync(
            string.Join("\r\n", Enumerable.Range(0, 12)) + "\r\n\x1b[?9001h",
            TestContext.Current.CancellationToken);
        await terminal.ScrollLinesAsync(-2, TestContext.Current.CancellationToken);
        int viewportY = (await terminal.GetSnapshotAsync(
            SnapshotScope.ActiveBuffer,
            TestContext.Current.CancellationToken)).ActiveBuffer.ViewportY;

        await terminal.SendKeyAsync(
            Map(
                Key.RightCtrl,
                PhysicalKey.ControlRight,
                null,
                KeyModifiers.Control),
            TestContext.Current.CancellationToken);
        TerminalSnapshot modifier = await terminal.GetSnapshotAsync(
            SnapshotScope.ActiveBuffer,
            TestContext.Current.CancellationToken);
        Assert.Equal(viewportY, modifier.ActiveBuffer.ViewportY);

        await terminal.SendKeyAsync(
            Map(Key.A, PhysicalKey.A, "a"),
            TestContext.Current.CancellationToken);
        TerminalSnapshot character = await terminal.GetSnapshotAsync(
            SnapshotScope.ActiveBuffer,
            TestContext.Current.CancellationToken);
        Assert.Equal(character.ActiveBuffer.BaseY, character.ActiveBuffer.ViewportY);
    }

    [Fact]
    public void KeyStateTrackerDistinguishesPressRepeatAndRelease()
    {
        var tracker = new AvaloniaKeyStateTracker();

        Assert.Equal(TerminalKeyEventType.Press, tracker.KeyDown(PhysicalKey.A, Key.A));
        Assert.Equal(TerminalKeyEventType.Repeat, tracker.KeyDown(PhysicalKey.A, Key.A));
        Assert.False(tracker.KeyUp(PhysicalKey.A, Key.A));
        Assert.Equal(TerminalKeyEventType.Press, tracker.KeyDown(PhysicalKey.A, Key.A));
        tracker.SuppressRelease(PhysicalKey.C, Key.C);
        Assert.True(tracker.KeyUp(PhysicalKey.C, Key.C));
        Assert.False(tracker.KeyUp(PhysicalKey.C, Key.C));
        tracker.Clear();
        Assert.Equal(TerminalKeyEventType.Press, tracker.KeyDown(PhysicalKey.A, Key.A));
    }

    private static TerminalKeyEvent Map(
        Key key,
        PhysicalKey physicalKey,
        string? symbol,
        KeyModifiers modifiers = KeyModifiers.None,
        TerminalKeyEventType eventType = TerminalKeyEventType.Press) =>
        AvaloniaKeyMapper.Create(key, physicalKey, symbol, modifiers, eventType);

    private static bool UsesTextInput(
        TerminalKeyEvent key,
        bool enhancedKeyboardMode = false,
        bool isMac = false,
        bool isWindows = false,
        bool macOptionIsMeta = false) =>
        AvaloniaKeyMapper.ShouldUseTextInput(
            key,
            enhancedKeyboardMode,
            isMac,
            isWindows,
            macOptionIsMeta);
}

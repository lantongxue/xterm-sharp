namespace XtermSharp.WinForms.Tests;

public sealed class TerminalViewTests
{
    public TerminalViewTests()
    {
        WindowsFormsSynchronizationContext.AutoInstall = false;
    }

    [Fact]
    public void TerminalPropertyDoesNotTransferOwnership()
    {
        using var terminal = new Terminal();
        using var view = new TerminalView { Terminal = terminal };

        view.Terminal = null;

        Assert.False(terminal.IsDisposed);
        Assert.False(TerminalView.HasNonEmptySelection(null));
        Assert.True(TerminalView.HasNonEmptySelection(new TerminalSelection(1, 1, 2, 1)));
    }

    [Fact]
    public async Task OscHyperlinkUsesWinFormsHoverLeaveAndActivationPipeline()
    {
        using var terminal = new Terminal(new TerminalOptions { Columns = 8, Rows = 2 });
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
        using var view = new TerminalView { Terminal = terminal };
        var move = new TerminalLinkEvent(
            2,
            1,
            10,
            10,
            TerminalMouseButton.None,
            TerminalMouseAction.Move);
        TerminalLinkEvent release = move with
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
    public void KeyMapperProducesBrowserCompatibleCoordinates()
    {
        TerminalKeyEvent arrow = WinFormsKeyMapper.Create(
            Keys.Left,
            null,
            Keys.Control | Keys.Shift,
            TerminalKeyEventType.Press);
        TerminalKeyEvent letter = WinFormsKeyMapper.Create(
            Keys.A,
            "a",
            Keys.None,
            TerminalKeyEventType.Press);
        TerminalKeyEvent numpad = WinFormsKeyMapper.Create(
            Keys.NumPad7,
            "7",
            Keys.None,
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
    public void DeadKeysAreDeferredToCommittedTextInput()
    {
        TerminalKeyEvent deadKey = WinFormsKeyMapper.Create(
            Keys.OemQuotes,
            WinFormsKeyMapper.DeadKeyMarker,
            Keys.None,
            TerminalKeyEventType.Press);

        Assert.Equal("Dead", deadKey.Key);
        Assert.Null(deadKey.Text);
        Assert.True(WinFormsKeyMapper.ShouldUseTextInput(deadKey, enhancedKeyboardMode: false));
        Assert.True(WinFormsKeyMapper.ShouldUseTextInput(deadKey, enhancedKeyboardMode: true));
    }

    [Fact]
    public async Task BackspaceAndCtrlCProduceLegacyTerminalInput()
    {
        using var terminal = new Terminal();
        var data = new List<string>();
        terminal.Data += (_, args) => data.Add(args.Data);
        TerminalKeyEvent backspace = WinFormsKeyMapper.Create(
            Keys.Back,
            null,
            Keys.None,
            TerminalKeyEventType.Press);
        TerminalKeyEvent controlC = WinFormsKeyMapper.Create(
            Keys.C,
            "c",
            Keys.Control,
            TerminalKeyEventType.Press);

        await terminal.SendKeyAsync(backspace, TestContext.Current.CancellationToken);
        await terminal.SendKeyAsync(controlC, TestContext.Current.CancellationToken);

        Assert.Equal(["\x7f", "\x03"], data);
        Assert.False(WinFormsKeyMapper.ShouldUseTextInput(backspace, enhancedKeyboardMode: false));
        Assert.False(WinFormsKeyMapper.ShouldUseTextInput(controlC, enhancedKeyboardMode: false));
        Assert.True(WinFormsKeyMapper.ShouldCopy(
            Keys.C,
            Keys.Control,
            hasSelection: true));
        Assert.False(WinFormsKeyMapper.ShouldCopy(
            Keys.C,
            Keys.Control,
            hasSelection: false));
        Assert.True(WinFormsKeyMapper.ShouldPaste(Keys.V, Keys.Control));
        Assert.True(WinFormsKeyMapper.ShouldPaste(Keys.Insert, Keys.Shift));
    }

    [Fact]
    public async Task CommittedEmojiSurrogatesProduceOneTerminalDataEvent()
    {
        using var terminal = new Terminal();
        using var view = new TerminalView { Terminal = terminal };
        var data = new List<string>();
        terminal.Data += (_, args) => data.Add(args.Data);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await view.SendCommittedCharacterAsync('\ud83d', cancellationToken);
        Assert.Empty(data);

        await view.SendCommittedCharacterAsync('\ude00', cancellationToken);

        Assert.Equal(["\ud83d\ude00"], data);
    }

    [Fact]
    public async Task ClipboardProviderUsesConfiguredDispatcherOperations()
    {
        using var dispatcher = new Control();
        string clipboard = "initial";
        var provider = new WinFormsClipboardProvider(
            dispatcher,
            () => clipboard,
            text => clipboard = text);

        Assert.Equal(
            "initial",
            await provider.ReadTextAsync("c", TestContext.Current.CancellationToken));
        await provider.WriteTextAsync("c", "updated", TestContext.Current.CancellationToken);

        Assert.Equal("updated", clipboard);
    }

    [Fact]
    public void TerminalViewPreparesAndPaintsARealSkiaFrame()
    {
        RunOnStaThread(() =>
        {
            using var terminal = new Terminal(new TerminalOptions
            {
                Columns = 8,
                Rows = 2,
                FontFamily = "Cascadia Mono, Consolas, monospace",
                FontSize = 15
            });
            terminal.WriteAsync("\x1b[32mWinForms\x1b[0m")
                .AsTask().GetAwaiter().GetResult();
            using var form = new Form
            {
                ClientSize = new Size(480, 180),
                FormBorderStyle = FormBorderStyle.FixedToolWindow,
                Location = new Point(-32_000, -32_000),
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual
            };
            using var view = new TerminalView
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                Terminal = terminal
            };
            form.Controls.Add(view);
            form.Show();
            var timeout = System.Diagnostics.Stopwatch.StartNew();
            while (view.Columns == 8 && timeout.Elapsed < TimeSpan.FromSeconds(5))
            {
                Application.DoEvents();
                Thread.Sleep(10);
            }
            Application.DoEvents();

            using var bitmap = new Bitmap(view.ClientSize.Width, view.ClientSize.Height);
            view.DrawToBitmap(bitmap, view.ClientRectangle);

            Assert.NotEqual(8, view.Columns);
            Assert.True(ContainsForegroundPixel(bitmap));
        });
    }

    private static bool ContainsForegroundPixel(Bitmap bitmap)
    {
        for (int y = 0; y < bitmap.Height; y += 2)
        {
            for (int x = 0; x < bitmap.Width; x += 2)
            {
                Color pixel = bitmap.GetPixel(x, y);
                if (pixel.R > 20 || pixel.G > 20 || pixel.B > 20)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "The Windows Forms smoke test timed out.");
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}

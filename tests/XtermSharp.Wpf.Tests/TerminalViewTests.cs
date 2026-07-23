using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace XtermSharp.Wpf.Tests;

public sealed class TerminalViewTests
{
    [Fact]
    public void TerminalPropertyDoesNotTransferOwnership()
    {
        RunOnStaThread(() =>
        {
            using var terminal = new Terminal();
            using var view = new TerminalView { Terminal = terminal };

            view.Terminal = null;

            Assert.False(terminal.IsDisposed);
            Assert.Equal(0, view.Columns);
            Assert.False(TerminalView.HasNonEmptySelection(null));
            Assert.True(TerminalView.HasNonEmptySelection(new TerminalSelection(1, 1, 2, 1)));
        });
    }

    [Fact]
    public void TerminalPropertiesAreBindableDependencyProperties()
    {
        RunOnStaThread(() =>
        {
            using var terminal = new Terminal(new TerminalOptions { Columns = 90, Rows = 30 });
            using var view = new TerminalView { Terminal = terminal };

            Assert.Same(terminal, view.GetValue(TerminalView.TerminalProperty));
            Assert.Equal(90, view.GetValue(TerminalView.ColumnsProperty));
            Assert.Equal(30, view.GetValue(TerminalView.RowsProperty));
            Assert.False(view.ShowRenderingDebugOverlay);
            Assert.Equal(SkiaRenderMode.Unknown, view.ActiveRenderMode);
            Assert.False(view.IsGpuAccelerated);
            view.ShowRenderingDebugOverlay = true;
            Assert.True((bool)view.GetValue(TerminalView.ShowRenderingDebugOverlayProperty));
        });
    }

    [Fact]
    public async Task OscHyperlinkUsesWpfHoverLeaveAndActivationPipeline()
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

        RunOnStaThread(() =>
        {
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
        });

        Assert.Equal(["hover:view", "activate:view", "leave:view"], interactions);
    }

    [Fact]
    public void KeyMapperProducesBrowserCompatibleCoordinates()
    {
        RunOnStaThread(() =>
        {
            TerminalKeyEvent arrow = WpfKeyMapper.Create(
                Key.Left,
                null,
                ModifierKeys.Control | ModifierKeys.Shift,
                TerminalKeyEventType.Press);
            TerminalKeyEvent letter = WpfKeyMapper.Create(
                Key.A,
                "a",
                ModifierKeys.None,
                TerminalKeyEventType.Press);
            TerminalKeyEvent numpad = WpfKeyMapper.Create(
                Key.NumPad7,
                "7",
                ModifierKeys.None,
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
        });
    }

    [Fact]
    public void KeyMapperDefersTextAndRecognizesClipboardShortcuts()
    {
        RunOnStaThread(() =>
        {
            TerminalKeyEvent deadKey = WpfKeyMapper.Create(
                Key.Oem7,
                WpfKeyMapper.DeadKeyMarker,
                ModifierKeys.None,
                TerminalKeyEventType.Press);
            TerminalKeyEvent backspace = WpfKeyMapper.Create(
                Key.Back,
                null,
                ModifierKeys.None,
                TerminalKeyEventType.Press);

            Assert.Equal("Dead", deadKey.Key);
            Assert.Null(deadKey.Text);
            Assert.True(WpfKeyMapper.ShouldUseTextInput(deadKey, enhancedKeyboardMode: false));
            Assert.False(WpfKeyMapper.ShouldUseTextInput(backspace, enhancedKeyboardMode: false));
            Assert.True(WpfKeyMapper.ShouldCopy(Key.C, ModifierKeys.Control, hasSelection: true));
            Assert.False(WpfKeyMapper.ShouldCopy(Key.C, ModifierKeys.Control, hasSelection: false));
            Assert.True(WpfKeyMapper.ShouldPaste(Key.V, ModifierKeys.Control));
            Assert.True(WpfKeyMapper.ShouldPaste(Key.Insert, ModifierKeys.Shift));
            Assert.True(WpfKeyMapper.ShouldSelectAll(Key.A, ModifierKeys.Control));
        });
    }

    [Fact]
    public void ClipboardProviderUsesConfiguredDispatcherOperations()
    {
        RunOnStaThread(() =>
        {
            string clipboard = "initial";
            var provider = new WpfClipboardProvider(
                Dispatcher.CurrentDispatcher,
                () => clipboard,
                text => clipboard = text);

            Assert.Equal(
                "initial",
                provider.ReadTextAsync("c").AsTask().GetAwaiter().GetResult());
            provider.WriteTextAsync("c", "updated").AsTask().GetAwaiter().GetResult();

            Assert.Equal("updated", clipboard);
        });
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
            terminal.WriteAsync("\x1b[32mWPF\x1b[0m")
                .AsTask().GetAwaiter().GetResult();
            using var view = new TerminalView
            {
                Padding = new Thickness(8),
                Terminal = terminal
            };
            var window = new Window
            {
                Width = 480,
                Height = 180,
                WindowStyle = WindowStyle.ToolWindow,
                Left = -32_000,
                Top = -32_000,
                ShowInTaskbar = false,
                Content = view
            };
            window.Show();
            try
            {
                var timeout = System.Diagnostics.Stopwatch.StartNew();
                while (view.Columns == 8 && timeout.Elapsed < TimeSpan.FromSeconds(5))
                {
                    PumpDispatcher();
                    Thread.Sleep(10);
                }
                PumpDispatcher();

                int width = Math.Max(1, (int)Math.Ceiling(view.ActualWidth));
                int height = Math.Max(1, (int)Math.Ceiling(view.ActualHeight));
                var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(view);

                Assert.NotEqual(8, view.Columns);
                Assert.True(ContainsForegroundPixel(bitmap));
            }
            finally
            {
                window.Content = null;
                window.Close();
            }
        });
    }

    private static bool ContainsForegroundPixel(BitmapSource bitmap)
    {
        int stride = bitmap.PixelWidth * 4;
        byte[] pixels = GC.AllocateUninitializedArray<byte>(stride * bitmap.PixelHeight);
        bitmap.CopyPixels(pixels, stride, 0);
        for (int index = 0; index < pixels.Length; index += 8)
        {
            if (pixels[index] > 20 || pixels[index + 1] > 20 || pixels[index + 2] > 20)
            {
                return true;
            }
        }
        return false;
    }

    private static void PumpDispatcher()
    {
        var frame = new DispatcherFrame();
        _ = Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
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
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "The WPF smoke test timed out.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}

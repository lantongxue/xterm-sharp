namespace XtermSharp.Tests.Headless;

public sealed class TerminalOptionPlumbingTests
{
    public static TheoryData<bool, TerminalWindowsPtyBackend?, int?, bool> WindowsPtyReflowCases { get; } = new()
    {
        { false, null, null, true },
        { true, TerminalWindowsPtyBackend.ConPty, 21375, false },
        { true, TerminalWindowsPtyBackend.ConPty, 21376, true },
        { true, TerminalWindowsPtyBackend.WinPty, 22621, false },
        { true, TerminalWindowsPtyBackend.WinPty, null, true },
        { true, null, 22621, false }
    };

    [Fact]
    public void Options_DefaultsAndConstructionMatchUpstreamAndCloneNestedModels()
    {
        var windowsPty = new TerminalWindowsPtyOptions
        {
            Backend = TerminalWindowsPtyBackend.ConPty,
            BuildNumber = 22621
        };
        var vtExtensions = new TerminalVtExtensions { KittySgrBoldFaintControl = false };
        using var configured = new Terminal(new TerminalOptions
        {
            ReflowCursorLine = true,
            WindowsPty = windowsPty,
            DisableStdin = true,
            VtExtensions = vtExtensions
        });

        Assert.True(configured.Options.ReflowCursorLine);
        Assert.Equal(TerminalWindowsPtyBackend.ConPty, configured.Options.WindowsPty.Backend);
        Assert.Equal(22621, configured.Options.WindowsPty.BuildNumber);
        Assert.True(configured.Options.DisableStdin);
        Assert.False(configured.Options.VtExtensions.KittySgrBoldFaintControl);
        Assert.NotSame(windowsPty, configured.Options.WindowsPty);
        Assert.NotSame(vtExtensions, configured.Options.VtExtensions);

        using var defaults = new Terminal();
        Assert.False(defaults.Options.ReflowCursorLine);
        Assert.Null(defaults.Options.WindowsPty.Backend);
        Assert.Null(defaults.Options.WindowsPty.BuildNumber);
        Assert.False(defaults.Options.DisableStdin);
        Assert.True(defaults.Options.VtExtensions.KittySgrBoldFaintControl);
    }

    [Fact]
    public async Task Options_RuntimeUpdateRaisesOneCommittedEventAndClonesNestedModels()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var initialWindowsPty = new TerminalWindowsPtyOptions
        {
            Backend = TerminalWindowsPtyBackend.ConPty,
            BuildNumber = 19000
        };
        var initialVtExtensions = new TerminalVtExtensions { KittySgrBoldFaintControl = false };
        await using var terminal = new Terminal(new TerminalOptions
        {
            ReflowCursorLine = true,
            WindowsPty = initialWindowsPty,
            VtExtensions = initialVtExtensions
        });
        var events = new List<TerminalOptionsChangedEventArgs>();
        terminal.OptionsChanged += (_, args) => events.Add(args);
        var updatedWindowsPty = new TerminalWindowsPtyOptions
        {
            Backend = TerminalWindowsPtyBackend.ConPty,
            BuildNumber = 22621
        };
        var updatedVtExtensions = new TerminalVtExtensions { KittySgrBoldFaintControl = true };

        await terminal.UpdateOptionsAsync(new TerminalOptionsUpdate
        {
            ReflowCursorLine = false,
            WindowsPty = updatedWindowsPty,
            DisableStdin = true,
            VtExtensions = updatedVtExtensions
        }, cancellationToken);

        TerminalOptionsChangedEventArgs changed = Assert.Single(events);
        Assert.True(changed.Previous.ReflowCursorLine);
        Assert.Equal(19000, changed.Previous.WindowsPty.BuildNumber);
        Assert.False(changed.Previous.DisableStdin);
        Assert.False(changed.Previous.VtExtensions.KittySgrBoldFaintControl);
        Assert.False(changed.Current.ReflowCursorLine);
        Assert.Equal(TerminalWindowsPtyBackend.ConPty, changed.Current.WindowsPty.Backend);
        Assert.Equal(22621, changed.Current.WindowsPty.BuildNumber);
        Assert.True(changed.Current.DisableStdin);
        Assert.True(changed.Current.VtExtensions.KittySgrBoldFaintControl);
        Assert.Equal(terminal.Revision, changed.Revision);
        Assert.Same(changed.Current, terminal.Options);
        Assert.NotSame(initialWindowsPty, changed.Previous.WindowsPty);
        Assert.NotSame(initialVtExtensions, changed.Previous.VtExtensions);
        Assert.NotSame(updatedWindowsPty, changed.Current.WindowsPty);
        Assert.NotSame(updatedVtExtensions, changed.Current.VtExtensions);
        Assert.NotSame(changed.Previous.WindowsPty, changed.Current.WindowsPty);
        Assert.NotSame(changed.Previous.VtExtensions, changed.Current.VtExtensions);
    }

    [Fact]
    public async Task Resize_ReflowCursorLineControlsTheProductionCursorLinePath()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using var disabled = new Terminal(new TerminalOptions { Columns = 6, Rows = 3 });
        await using var enabled = new Terminal(new TerminalOptions
        {
            Columns = 6,
            Rows = 3,
            ReflowCursorLine = true
        });
        await disabled.WriteAsync("abcdef", cancellationToken);
        await enabled.WriteAsync("abcdef", cancellationToken);

        await disabled.ResizeAsync(4, 3, cancellationToken);
        await enabled.ResizeAsync(4, 3, cancellationToken);

        Assert.Equal("abcd", Line(disabled, 0));
        Assert.Equal(string.Empty, Line(disabled, 1));
        Assert.False(disabled.Buffer.Active.GetLine(1)!.IsWrapped);
        Assert.Equal("abcd", Line(enabled, 0));
        Assert.Equal("ef", Line(enabled, 1));
        Assert.True(enabled.Buffer.Active.GetLine(1)!.IsWrapped);
        Assert.Equal(1, enabled.Buffer.Active.CursorY);
    }

    [Theory]
    [MemberData(nameof(WindowsPtyReflowCases))]
    public async Task Resize_WindowsPtyBackendAndBuildGateProductionReflow(
        bool configureWindowsPty,
        TerminalWindowsPtyBackend? backend,
        int? buildNumber,
        bool expectReflow)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var options = new TerminalOptions { Columns = 5, Rows = 4 };
        if (configureWindowsPty)
        {
            options = new TerminalOptions
            {
                Columns = 5,
                Rows = 4,
                WindowsPty = new TerminalWindowsPtyOptions
                {
                    Backend = backend,
                    BuildNumber = buildNumber
                }
            };
        }
        await using var terminal = new Terminal(options);
        await terminal.WriteAsync("abcde\r\n", cancellationToken);

        await terminal.ResizeAsync(2, 4, cancellationToken);

        Assert.Equal("ab", Line(terminal, 0));
        Assert.Equal(expectReflow ? "cd" : string.Empty, Line(terminal, 1));
        Assert.Equal(expectReflow, terminal.Buffer.Active.GetLine(1)!.IsWrapped);
    }

    [Fact]
    public async Task Resize_ConfiguredWindowsPtyPreservesScrollbackWhenRowsGrow()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using var normal = new Terminal(new TerminalOptions { Columns = 5, Rows = 3 });
        await using var windows = new Terminal(new TerminalOptions
        {
            Columns = 5,
            Rows = 3,
            WindowsPty = new TerminalWindowsPtyOptions { Backend = TerminalWindowsPtyBackend.WinPty }
        });
        await FillFiveLinesAsync(normal, cancellationToken);
        await FillFiveLinesAsync(windows, cancellationToken);

        await normal.ResizeAsync(5, 5, cancellationToken);
        await windows.ResizeAsync(5, 5, cancellationToken);

        Assert.Equal(0, normal.Buffer.Active.BaseY);
        Assert.Equal(5, normal.Buffer.Active.Length);
        Assert.Equal(4, normal.Buffer.Active.CursorY);
        Assert.Equal(2, windows.Buffer.Active.BaseY);
        Assert.Equal(7, windows.Buffer.Active.Length);
        Assert.Equal(2, windows.Buffer.Active.CursorY);
    }

    [Fact]
    public async Task DisableStdinBlocksAllPtyInputButNotTerminalOutputWrites()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using var terminal = new Terminal(new TerminalOptions { Columns = 10, Rows = 2 });
        var data = new List<string>();
        terminal.Data += (_, args) => data.Add(args.Data);
        await terminal.WriteAsync("\x1b[?1004h\x1b[?1000h", cancellationToken);
        var mouse = new TerminalMouseEvent(
            1,
            1,
            0,
            0,
            TerminalMouseButton.Left,
            TerminalMouseAction.Down);

        await terminal.SendInputAsync("direct", cancellationToken: cancellationToken);
        await terminal.SendKeyAsync(new TerminalKeyEvent("a", "KeyA", 65), cancellationToken);
        await terminal.PasteAsync("paste", cancellationToken);
        await terminal.SendFocusAsync(true, cancellationToken);
        await terminal.SendMouseAsync(mouse, cancellationToken);
        await terminal.WriteAsync("\x1b[5n", cancellationToken);

        Assert.Equal(6, data.Count);
        Assert.Equal("direct", data[0]);
        Assert.Equal("a", data[1]);
        Assert.Equal("paste", data[2]);
        Assert.Equal("\x1b[I", data[3]);
        Assert.Equal("\x1b[0n", data[5]);

        await terminal.UpdateOptionsAsync(new TerminalOptionsUpdate { DisableStdin = true }, cancellationToken);
        int enabledCount = data.Count;
        await terminal.SendInputAsync("blocked", cancellationToken: cancellationToken);
        await terminal.SendKeyAsync(new TerminalKeyEvent("b", "KeyB", 66), cancellationToken);
        await terminal.PasteAsync("blocked", cancellationToken);
        await terminal.SendFocusAsync(false, cancellationToken);
        await terminal.SendMouseAsync(mouse, cancellationToken);
        await terminal.WriteAsync("\x1b[5nOUTPUT", cancellationToken);

        Assert.Equal(enabledCount, data.Count);
        Assert.Equal("OUTPUT", Line(terminal, 0));

        await terminal.UpdateOptionsAsync(new TerminalOptionsUpdate { DisableStdin = false }, cancellationToken);
        await terminal.SendInputAsync("restored", cancellationToken: cancellationToken);
        Assert.Equal(enabledCount + 1, data.Count);
        Assert.Equal("restored", data[^1]);
    }

    [Fact]
    public async Task KittySgrBoldFaintControlCanBeChangedAtRuntime()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using var terminal = new Terminal(new TerminalOptions
        {
            Columns = 10,
            Rows = 2,
            VtExtensions = new TerminalVtExtensions { KittySgrBoldFaintControl = false }
        });
        await terminal.WriteAsync("\x1b[1;2mA\x1b[221mB\x1b[222mC", cancellationToken);

        await terminal.UpdateOptionsAsync(new TerminalOptionsUpdate
        {
            VtExtensions = new TerminalVtExtensions { KittySgrBoldFaintControl = true }
        }, cancellationToken);
        await terminal.WriteAsync("\x1b[0;1;2mD\x1b[221mE\x1b[1m\x1b[222mF", cancellationToken);

        TerminalCellSnapshot[] cells = terminal.Buffer.Active.GetLine(0)!.Cells.Take(6).ToArray();
        AssertBoldAndDim(cells[0], bold: true, dim: true);
        AssertBoldAndDim(cells[1], bold: true, dim: true);
        AssertBoldAndDim(cells[2], bold: true, dim: true);
        AssertBoldAndDim(cells[3], bold: true, dim: true);
        AssertBoldAndDim(cells[4], bold: false, dim: true);
        AssertBoldAndDim(cells[5], bold: true, dim: false);
    }

    private static async Task FillFiveLinesAsync(Terminal terminal, CancellationToken cancellationToken) =>
        await terminal.WriteAsync("0\r\n1\r\n2\r\n3\r\n4", cancellationToken);

    private static string Line(Terminal terminal, int row) =>
        terminal.Buffer.Active.GetLine(row)!.TranslateToString(trimRight: true);

    private static void AssertBoldAndDim(TerminalCellSnapshot cell, bool bold, bool dim)
    {
        Assert.Equal(bold, cell.Attributes.HasFlag(CellAttributes.Bold));
        Assert.Equal(dim, cell.Attributes.HasFlag(CellAttributes.Dim));
    }
}

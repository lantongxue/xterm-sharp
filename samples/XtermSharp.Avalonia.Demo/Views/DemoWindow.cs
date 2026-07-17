using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using System.Text;

namespace XtermSharp.Avalonia.Demo;

internal sealed class DemoWindow : Window
{
    private readonly Terminal _terminal = new(new TerminalOptions
    {
        Columns = 80,
        Rows = 24,
        Scrollback = 2000,
        FontFamily = "Cascadia Mono, Menlo, DejaVu Sans Mono, monospace",
        FontSize = 15
    });
    private readonly StringBuilder _inputLine = new();

    public DemoWindow()
    {
        Title = "XtermSharp Avalonia Demo";
        Width = 960;
        Height = 600;
        var terminalView = new TerminalView
        {
            Terminal = _terminal,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Padding = new Thickness(8)
        };
        Content = terminalView;
        Opened += async (_, _) =>
        {
            _terminal.Data += OnTerminalData;
            await _terminal.WriteAsync(
                "\x1b[1;34mXtermSharp\x1b[0m Avalonia + Skia renderer\r\n" +
                "\x1b[2mType below; this demo echoes input without a PTY.\x1b[0m\r\n\r\n$ ");
            terminalView.Focus();
        };
        Closed += async (_, _) =>
        {
            _terminal.Data -= OnTerminalData;
            await _terminal.DisposeAsync();
        };
    }

    private void OnTerminalData(object? sender, TerminalDataEventArgs args)
    {
        _ = EchoAsync(args.Data);
    }

    private async Task EchoAsync(string data)
    {
        try
        {
            if (data == "\r")
            {
                _inputLine.Clear();
                await _terminal.WriteAsync("\r\n$ ");
                return;
            }
            if (data is "\x7f" or "\b")
            {
                if (_inputLine.Length != 0)
                {
                    int remove = char.IsLowSurrogate(_inputLine[^1]) && _inputLine.Length > 1 &&
                        char.IsHighSurrogate(_inputLine[^2]) ? 2 : 1;
                    _inputLine.Remove(_inputLine.Length - remove, remove);
                    await _terminal.WriteAsync("\b \b");
                }
                return;
            }
            if (data.StartsWith('\x1b'))
            {
                return;
            }
            _inputLine.Append(data);
            await _terminal.WriteAsync(data);
        }
        catch (ObjectDisposedException)
        {
        }
    }
}

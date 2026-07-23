using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System.Text;

namespace XtermSharp.Avalonia.Demo.Views;

internal sealed class DemoWindow : Window
{
    private static readonly UnicodeV15Provider LocalEchoUnicode = new();

    private readonly Terminal _terminal = new(new TerminalOptions
    {
        Columns = 80,
        Rows = 24,
        Scrollback = 2000,
        FontFamily = "Cascadia Mono, Menlo, DejaVu Sans Mono, monospace",
        FontSize = 15,
        UnicodeVersion = UnicodeV15Provider.GraphemeVersionName
    });
    private readonly SearchAddon _searchAddon = new();
    private readonly StringBuilder _inputLine = new();
    private readonly SemaphoreSlim _echoGate = new(1, 1);
    private readonly TerminalView _terminalView;
    private readonly TextBox _searchBox;
    private readonly ComboBox _renderingModeCombo;
    private readonly CheckBox _caseSensitive;
    private readonly CheckBox _wholeWord;
    private readonly CheckBox _regex;
    private readonly TextBlock _searchStatus;

    public DemoWindow()
    {
        Title = "XtermSharp Avalonia Demo";
        Width = 960;
        Height = 600;
        _terminal.LoadAddon(new WebLinksAddon());
        _terminal.LoadAddon(_searchAddon);
        _searchAddon.ResultsChanged += OnSearchResultsChanged;

        _terminalView = new TerminalView
        {
            Terminal = _terminal,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Padding = new Thickness(8),
            ShowRenderingDebugOverlay = true
        };
        _searchBox = new TextBox
        {
            MinWidth = 240,
            PlaceholderText = "Search terminal buffer"
        };
        _renderingModeCombo = new ComboBox
        {
            ItemsSource = Enum.GetValues<SkiaRenderModePreference>(),
            SelectedItem = SkiaRenderModePreference.Auto,
            MinWidth = 110
        };
        _renderingModeCombo.SelectionChanged += (_, _) =>
        {
            if (_renderingModeCombo.SelectedItem is SkiaRenderModePreference mode)
            {
                _terminalView.RequestedRenderMode = mode;
            }
        };
        _caseSensitive = new CheckBox { Content = "Case" };
        _wholeWord = new CheckBox { Content = "Whole word" };
        _regex = new CheckBox { Content = "Regex" };
        _caseSensitive.Click += (_, _) => ClearSearch(focusTerminal: false, clearText: false);
        _wholeWord.Click += (_, _) => ClearSearch(focusTerminal: false, clearText: false);
        _regex.Click += (_, _) => ClearSearch(focusTerminal: false, clearText: false);
        _searchStatus = new TextBlock
        {
            MinWidth = 72,
            VerticalAlignment = VerticalAlignment.Center
        };

        var previousButton = new Button { Content = "Previous" };
        var nextButton = new Button { Content = "Next" };
        var clearButton = new Button { Content = "Clear" };
        previousButton.Click += (_, _) => Search(forward: false);
        nextButton.Click += (_, _) => Search(forward: true);
        clearButton.Click += (_, _) => ClearSearch(focusTerminal: true, clearText: true);
        _searchBox.KeyDown += OnSearchBoxKeyDown;
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);

        var searchBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(8),
            Children =
            {
                new TextBlock
                {
                    Text = "Find",
                    VerticalAlignment = VerticalAlignment.Center
                },
                _searchBox,
                new TextBlock
                {
                    Text = "Rendering",
                    VerticalAlignment = VerticalAlignment.Center
                },
                _renderingModeCombo,
                _caseSensitive,
                _wholeWord,
                _regex,
                previousButton,
                nextButton,
                clearButton,
                _searchStatus
            }
        };
        var searchBorder = new Border
        {
            BorderBrush = Brushes.DimGray,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = searchBar
        };
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));
        root.Children.Add(searchBorder);
        Grid.SetRow(_terminalView, 1);
        root.Children.Add(_terminalView);
        Content = root;

        Opened += async (_, _) =>
        {
            _terminal.Data += OnTerminalData;
            await _terminal.WriteAsync(
                "\x1b[1;34mXtermSharp\x1b[0m Avalonia + Skia renderer\r\n" +
                "\x1b[2mThis demo loads addon-web-links and addon-search.\x1b[0m\r\n" +
                "Links: https://github.com/xtermjs/xterm.js and https://github.com\r\n" +
                "Search sample: warning error warning success error\r\n" +
                "Use Ctrl/Command+F, Enter for next and Shift+Enter for previous.\r\n" +
                "Type below; input is echoed locally without a PTY.\r\n\r\n$ ");
            _terminalView.Focus();
        };
        Closed += async (_, _) =>
        {
            _terminal.Data -= OnTerminalData;
            _searchAddon.ResultsChanged -= OnSearchResultsChanged;
            await _terminal.DisposeAsync();
        };
    }

    private void Search(bool forward)
    {
        try
        {
            SearchOptions options = CreateSearchOptions();
            bool found = forward
                ? _searchAddon.FindNext(_searchBox.Text ?? string.Empty, options)
                : _searchAddon.FindPrevious(_searchBox.Text ?? string.Empty, options);
            if (!found)
            {
                _searchStatus.Text = "No matches";
            }
        }
        catch (ArgumentException)
        {
            _searchStatus.Text = "Invalid regex";
        }
    }

    private SearchOptions CreateSearchOptions() => new()
    {
        CaseSensitive = _caseSensitive.IsChecked == true,
        WholeWord = _wholeWord.IsChecked == true,
        Regex = _regex.IsChecked == true,
        Incremental = true,
        Decorations = new SearchDecorationOptions
        {
            MatchBackground = TerminalColor.Rgb(92, 67, 12),
            MatchBorder = TerminalColor.Rgb(214, 162, 48),
            MatchOverviewRuler = TerminalColor.Rgb(214, 162, 48),
            ActiveMatchBackground = TerminalColor.Rgb(21, 94, 135),
            ActiveMatchBorder = TerminalColor.Rgb(125, 211, 252),
            ActiveMatchOverviewRuler = TerminalColor.Rgb(125, 211, 252)
        }
    };

    private void ClearSearch(bool focusTerminal, bool clearText)
    {
        _searchAddon.ClearDecorations();
        _terminal.ClearSelection();
        _searchStatus.Text = string.Empty;
        if (clearText)
        {
            _searchBox.Text = string.Empty;
        }
        if (focusTerminal)
        {
            _terminalView.Focus();
        }
    }

    private void OnSearchBoxKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key == Key.Enter)
        {
            Search(forward: !args.KeyModifiers.HasFlag(KeyModifiers.Shift));
            args.Handled = true;
        }
        else if (args.Key == Key.Escape)
        {
            ClearSearch(focusTerminal: true, clearText: true);
            args.Handled = true;
        }
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key == Key.F &&
            (args.KeyModifiers.HasFlag(KeyModifiers.Control) ||
             args.KeyModifiers.HasFlag(KeyModifiers.Meta)))
        {
            _searchBox.Focus();
            _searchBox.SelectAll();
            args.Handled = true;
        }
    }

    private void OnSearchResultsChanged(object? sender, SearchResultChangedEventArgs args)
    {
        void UpdateStatus()
        {
            _searchStatus.Text = args.ResultCount == 0
                ? "No matches"
                : args.ResultIndex >= 0
                    ? $"{args.ResultIndex + 1} / {args.ResultCount}"
                    : $"{args.ResultCount} matches";
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            UpdateStatus();
        }
        else
        {
            Dispatcher.UIThread.Post(UpdateStatus);
        }
    }

    private void OnTerminalData(object? sender, TerminalDataEventArgs args)
    {
        _ = EchoAsync(args.Data);
    }

    private async Task EchoAsync(string data)
    {
        await _echoGate.WaitAsync();
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
                    string input = _inputLine.ToString();
                    int start = GetLastGraphemeStart(input, out int width);
                    _inputLine.Remove(start, _inputLine.Length - start);
                    string backspaces = new('\b', width);
                    await _terminal.WriteAsync(
                        string.Concat(backspaces, new string(' ', width), backspaces));
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
        finally
        {
            _echoGate.Release();
        }
    }

    private static int GetLastGraphemeStart(string value, out int width)
    {
        int offset = 0;
        int clusterStart = 0;
        int clusterWidth = 1;
        Rune? precedingRune = null;
        UnicodeCharacterProperties preceding = default;
        foreach (Rune rune in value.EnumerateRunes())
        {
            UnicodeCharacterProperties properties =
                LocalEchoUnicode.GetProperties(rune, preceding, precedingRune);
            if (!properties.JoinPrevious)
            {
                clusterStart = offset;
            }
            clusterWidth = Math.Max(1, properties.Width);
            offset += rune.Utf16SequenceLength;
            preceding = properties;
            precedingRune = rune;
        }
        width = clusterWidth;
        return clusterStart;
    }
}

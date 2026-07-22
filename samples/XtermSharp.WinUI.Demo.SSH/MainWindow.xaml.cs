using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace XtermSharp.WinUI.Demo.SSH;

public sealed partial class MainWindow : Window
{
    private const string DefaultTitle = "XtermSharp WinUI SSH Demo";

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Resize(new SizeInt32(1180, 780));
        RootFrame.Navigate(typeof(MainPage));
    }

    public void SetTerminalTitle(string? title)
    {
        string value = string.IsNullOrWhiteSpace(title) ? DefaultTitle : $"{title} - XtermSharp SSH";
        Title = value;
        AppTitleBar.Title = value;
    }
}

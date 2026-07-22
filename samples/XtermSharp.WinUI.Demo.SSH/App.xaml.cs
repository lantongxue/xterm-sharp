using Microsoft.UI.Xaml;

namespace XtermSharp.WinUI.Demo.SSH;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    public MainWindow MainWindow { get; private set; } = null!;

    public static new App Current => (App)Application.Current;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _ = args;
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }
}

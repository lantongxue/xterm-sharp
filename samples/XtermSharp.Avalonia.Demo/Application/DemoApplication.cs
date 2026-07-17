using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace XtermSharp.Avalonia.Demo.Application;

internal sealed class DemoApplication : global::Avalonia.Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new DemoWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }
}

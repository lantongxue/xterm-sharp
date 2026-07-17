using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using System.Text;

namespace XtermSharp.Avalonia.Demo.Application;

internal sealed class DemoApplication : global::Avalonia.Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new DemoWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }
}

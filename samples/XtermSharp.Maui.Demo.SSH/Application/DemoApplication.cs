using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using XtermSharp.Maui.Demo.SSH.Pages;

namespace XtermSharp.Maui.Demo.SSH.Application;

public sealed class DemoApplication : global::Microsoft.Maui.Controls.Application
{
    protected override Window CreateWindow(IActivationState? activationState)
    {
        _ = activationState;
        var page = new SshDemoPage();
        var window = new Window(page)
        {
            Title = "XtermSharp MAUI SSH Demo"
        };
        if (DeviceInfo.Platform == DevicePlatform.MacCatalyst || DeviceInfo.Platform == DevicePlatform.WinUI)
        {
            window.Width = 1280;
            window.Height = 760;
            window.MinimumWidth = 720;
            window.MinimumHeight = 520;
        }
        window.Destroying += (_, _) => _ = page.DisposeAsync();
        return window;
    }
}

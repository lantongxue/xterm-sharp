using Microsoft.Maui;
using Microsoft.Maui.Controls;
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
            Title = "XtermSharp MAUI SSH Demo",
            Width = 1280,
            Height = 760,
            MinimumWidth = 720,
            MinimumHeight = 520
        };
        window.Destroying += (_, _) => _ = page.DisposeAsync();
        return window;
    }
}

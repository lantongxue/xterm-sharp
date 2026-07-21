using Microsoft.Maui.Hosting;
using XtermSharp.Maui.Hosting;
using XtermSharp.Maui.Demo.SSH.Application;

namespace XtermSharp.Maui.Demo.SSH;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp() => MauiApp.CreateBuilder()
        .UseMauiApp<DemoApplication>()
        .UseXtermSharpMaui()
        .Build();
}

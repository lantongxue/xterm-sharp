namespace XtermSharp.Maui.Hosting;

public static partial class MauiAppBuilderExtensions
{
    static partial void RegisterPlatformInput(MauiAppBuilder builder)
    {
        _ = builder;
        WindowsTerminalInput.Configure();
    }
}

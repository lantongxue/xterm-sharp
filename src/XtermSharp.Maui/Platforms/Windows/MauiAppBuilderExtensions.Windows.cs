namespace XtermSharp.Maui.Hosting;

public static partial class MauiAppBuilderExtensions
{
    static partial void RegisterPlatformInput() => WindowsTerminalInput.Configure();
}

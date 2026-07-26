using Microsoft.Maui.Hosting;
using XtermSharp.Maui.Input;

namespace XtermSharp.Maui.Hosting;

public static partial class MauiAppBuilderExtensions
{
    static partial void RegisterPlatformInput(MauiAppBuilder builder)
    {
        builder.ConfigureMauiHandlers(handlers =>
            handlers.AddHandler<TerminalInputEntry, IosTerminalInputEntryHandler>());
    }
}

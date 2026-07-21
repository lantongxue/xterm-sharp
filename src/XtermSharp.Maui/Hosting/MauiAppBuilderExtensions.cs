using Microsoft.Maui.Hosting;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace XtermSharp.Maui.Hosting;

/// <summary>Registers the handlers required by XtermSharp's MAUI controls.</summary>
public static class MauiAppBuilderExtensions
{
    /// <summary>Registers the SkiaSharp view handler used by <c>TerminalView</c>.</summary>
    public static MauiAppBuilder UseXtermSharpMaui(this MauiAppBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UseSkiaSharp();
    }
}

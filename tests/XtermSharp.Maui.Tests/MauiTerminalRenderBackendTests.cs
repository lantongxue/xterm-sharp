namespace XtermSharp.Maui.Tests;

public sealed class MauiSkiaIntegrationTests
{
    [Fact]
    public void BackendMeasuresFontsWithoutAPlatformCanvas()
    {
        using var backend = new SkiaTerminalRenderBackend();
        var configuration = new TerminalRenderConfiguration(
            "Missing Font, Cascadia Mono, monospace",
            15,
            1.2,
            1,
            0,
            true,
            TerminalCursorStyle.Block,
            true,
            true,
            1,
            1);

        var metrics = backend.MeasureFont(configuration);

        Assert.True(metrics.CellWidth > 0);
        Assert.True(metrics.CellHeight > 0);
        Assert.True(metrics.Baseline > 0);
    }

    [Fact]
    public void MauiPackageReferencesSkiaRenderingAndViewAssemblies()
    {
        string[] references = typeof(TerminalView).Assembly
            .GetReferencedAssemblies()
            .Select(static assembly => assembly.Name ?? string.Empty)
            .ToArray();

        Assert.Contains("XtermSharp.Rendering.Skia", references);
        Assert.Contains(
            references,
            static name => name.StartsWith("SkiaSharp.Views.Maui", StringComparison.Ordinal));
    }
}

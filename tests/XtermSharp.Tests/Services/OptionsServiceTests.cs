using XtermSharp.Internal;

namespace XtermSharp.Tests.Services;

public sealed class OptionsServiceTests
{
    [UpstreamFact("XTJS-1272", "OptionsService constructor uses default value if invalid constructor option values passed for cols/rows")]
    public void Constructor_UsesDefaultsForInvalidColumnsAndRows()
    {
        using var service = new OptionsService(new TerminalOptions { Columns = 0, Rows = 0 });
        var defaults = new TerminalOptions();

        Assert.Equal(defaults.Columns, service.Options.Columns);
        Assert.Equal(defaults.Rows, service.Options.Rows);
    }

    [UpstreamFact("XTJS-1273", "OptionsService constructor uses values from constructor option values if correctly passed")]
    public void Constructor_UsesValidColumnsAndRows()
    {
        using var service = new OptionsService(new TerminalOptions { Columns = 80, Rows = 25 });

        Assert.Equal(80, service.Options.Columns);
        Assert.Equal(25, service.Options.Rows);
    }

    [UpstreamFact("XTJS-1274", "OptionsService constructor uses default value if invalid constructor option value passed")]
    public void Constructor_UsesDefaultForInvalidTabStopWidth()
    {
        using var service = new OptionsService(new TerminalOptions { TabStopWidth = 0 });

        Assert.Equal(new TerminalOptions().TabStopWidth, service.Options.TabStopWidth);
    }

    [UpstreamFact("XTJS-1275", "OptionsService constructor object.keys return the correct number of options")]
    public void Constructor_ExposesAllMutableOptionNames()
    {
        using var service = new OptionsService(new TerminalOptions { Columns = 80, Rows = 25 });

        Assert.NotEmpty(service.OptionNames);
        Assert.Equal(Enum.GetValues<TerminalOption>(), service.OptionNames);
    }

    [UpstreamFact("XTJS-1278", "OptionsService onOptionChange should fire on any option change")]
    public void OnOptionChange_FiresForEveryChangedOption()
    {
        using var service = new OptionsService();
        var firstChanges = new List<TerminalOption>();
        IDisposable first = service.OnOptionChange(firstChanges.Add);
        service.Update(new TerminalOptionsUpdate { TabStopWidth = 10 });
        first.Dispose();

        var secondChanges = new List<TerminalOption>();
        using IDisposable second = service.OnOptionChange(secondChanges.Add);
        service.Update(new TerminalOptionsUpdate { Scrollback = 20 });

        Assert.Equal([TerminalOption.TabStopWidth], firstChanges);
        Assert.Equal([TerminalOption.Scrollback], secondChanges);
    }

    [UpstreamFact("XTJS-1279", "OptionsService onSpecificOptionChange should fire only on a specific option change")]
    public void OnSpecificOptionChange_IgnoresOtherOptions()
    {
        using var service = new OptionsService();
        var values = new List<int>();
        using IDisposable subscription = service.OnSpecificOptionChange<int>(TerminalOption.Scrollback, values.Add);

        service.Update(new TerminalOptionsUpdate { TabStopWidth = 10 });
        Assert.Empty(values);
        service.Update(new TerminalOptionsUpdate { Scrollback = 20 });

        Assert.Equal([20], values);
    }

    [UpstreamFact("XTJS-1280", "OptionsService onSpecificOptionChange should fire only on a specific option change")]
    public void OnSpecificOptionChange_ReportsTheNewTypedValue()
    {
        using var service = new OptionsService();
        int? reported = null;
        using IDisposable subscription = service.OnSpecificOptionChange<int>(
            TerminalOption.Scrollback,
            value => reported = value);

        service.Update(new TerminalOptionsUpdate { TabStopWidth = 10 });
        Assert.Null(reported);
        service.Update(new TerminalOptionsUpdate { Scrollback = 20 });

        Assert.Equal(20, reported);
    }

    [UpstreamFact("XTJS-1281", "OptionsService onMultipleOptionChange should fire only for specific options")]
    public void OnMultipleOptionChange_FiresOnlyForSelectedOptions()
    {
        using var service = new OptionsService();
        int calls = 0;
        using IDisposable subscription = service.OnMultipleOptionChange(
            [TerminalOption.Scrollback],
            () => calls++);

        service.Update(new TerminalOptionsUpdate { TabStopWidth = 10 });
        Assert.Equal(0, calls);
        service.Update(new TerminalOptionsUpdate { Scrollback = 20 });

        Assert.Equal(1, calls);
    }
}

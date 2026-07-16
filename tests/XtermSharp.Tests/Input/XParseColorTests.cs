using XtermSharp.Internal;

namespace XtermSharp.Tests.Input;

public sealed class XParseColorTests
{
    [UpstreamFact("XTJS-0791", "XParseColor parseColor rgb:<r>/<g>/<b> scheme in 4/8/12/16 bit")]
    public void Parse_rgb_scheme_at_supported_depths()
    {
        AssertColor("rgb:0/0/0", 0, 0, 0);
        AssertColor("rgb:f/f/f", 255, 255, 255);
        AssertColor("rgb:1/2/3", 17, 34, 51);
        AssertColor("rgb:00/00/00", 0, 0, 0);
        AssertColor("rgb:ff/ff/ff", 255, 255, 255);
        AssertColor("rgb:11/22/33", 17, 34, 51);
        AssertColor("rgb:000/000/000", 0, 0, 0);
        AssertColor("rgb:fff/fff/fff", 255, 255, 255);
        AssertColor("rgb:111/222/333", 17, 34, 51);
        AssertColor("rgb:0000/0000/0000", 0, 0, 0);
        AssertColor("rgb:ffff/ffff/ffff", 255, 255, 255);
        AssertColor("rgb:1111/2222/3333", 17, 34, 51);
    }

    [UpstreamFact("XTJS-0792", "XParseColor parseColor #RGB scheme in 4/8/12/16 bit")]
    public void Parse_hash_scheme_at_supported_depths()
    {
        AssertColor("#000", 0, 0, 0);
        AssertColor("#fff", 240, 240, 240);
        AssertColor("#123", 16, 32, 48);
        AssertColor("#000000", 0, 0, 0);
        AssertColor("#ffffff", 255, 255, 255);
        AssertColor("#112233", 17, 34, 51);
        AssertColor("#000000000", 0, 0, 0);
        AssertColor("#fffffffff", 255, 255, 255);
        AssertColor("#111222333", 17, 34, 51);
        AssertColor("#000000000000", 0, 0, 0);
        AssertColor("#ffffffffffff", 255, 255, 255);
        AssertColor("#111122223333", 17, 34, 51);
    }

    [UpstreamFact("XTJS-0793", "XParseColor parseColor supports upper case")]
    public void Parse_supports_uppercase()
    {
        AssertColor("RGB:0/A/F", 0, 170, 255);
        AssertColor("#FFF", 240, 240, 240);
    }

    [UpstreamFact("XTJS-0794", "XParseColor parseColor does not parse illegal combinations")]
    public void Parse_rejects_illegal_combinations()
    {
        Assert.Null(XParseColor.ParseColor("rgb:0/11/222"));
        Assert.Null(XParseColor.ParseColor("rgbi:00/11/22"));
        Assert.Null(XParseColor.ParseColor("#aabbbcc"));
        Assert.Null(XParseColor.ParseColor("#aabbgg"));
        Assert.Null(XParseColor.ParseColor("rgb:aa/bb/gg"));
    }

    [UpstreamFact("XTJS-0795", "XParseColor toXColorRgb rgb:<r>/<g>/<b> scheme in 4/8/12/16 bit")]
    public void Format_rgb_scheme_at_supported_depths()
    {
        AssertFormatted("rgb:0/0/0", 4, "rgb:0/0/0");
        AssertFormatted("rgb:f/f/f", 4, "rgb:f/f/f");
        AssertFormatted("rgb:1/2/3", 4, "rgb:1/2/3");
        AssertFormatted("rgb:00/00/00", 8, "rgb:00/00/00");
        AssertFormatted("rgb:ff/ff/ff", 8, "rgb:ff/ff/ff");
        AssertFormatted("rgb:11/22/33", 8, "rgb:11/22/33");
        AssertFormatted("rgb:000/000/000", 12, "rgb:000/000/000");
        AssertFormatted("rgb:fff/fff/fff", 12, "rgb:fff/fff/fff");
        AssertFormatted("rgb:111/222/333", 12, "rgb:111/222/333");
        AssertFormatted("rgb:0000/0000/0000", 16, "rgb:0000/0000/0000");
        AssertFormatted("rgb:ffff/ffff/ffff", 16, "rgb:ffff/ffff/ffff");
        AssertFormatted("rgb:1111/2222/3333", 16, "rgb:1111/2222/3333");
    }

    [UpstreamFact("XTJS-0796", "XParseColor toXColorRgb defaults to 16 bit output")]
    public void Format_defaults_to_16_bit_output()
    {
        Assert.Equal("rgb:1111/2222/3333", XParseColor.ToRgbString(RequiredColor("rgb:1/2/3")));
        Assert.Equal("rgb:1111/2222/3333", XParseColor.ToRgbString(RequiredColor("rgb:11/22/33")));
        Assert.Equal("rgb:1111/2222/3333", XParseColor.ToRgbString(RequiredColor("rgb:111/222/333")));
        Assert.Equal("rgb:1212/1212/1212", XParseColor.ToRgbString(RequiredColor("rgb:123/123/123")));
    }

    [UpstreamFact("XTJS-0797", "XParseColor toXColorRgb reduces colors to 8 bit resolution")]
    public void Format_reduces_colors_to_8_bit_resolution()
    {
        Assert.Equal("rgb:121/121/121", XParseColor.ToRgbString(RequiredColor("rgb:123/123/123"), 12));
        Assert.Equal("rgb:1212/1212/1212", XParseColor.ToRgbString(RequiredColor("rgb:1234/1234/1234"), 16));
    }

    private static void AssertColor(string value, byte red, byte green, byte blue) =>
        Assert.Equal(new RgbColor(red, green, blue), RequiredColor(value));

    private static void AssertFormatted(string value, int bits, string expected) =>
        Assert.Equal(expected, XParseColor.ToRgbString(RequiredColor(value), bits));

    private static RgbColor RequiredColor(string value)
    {
        RgbColor? color = XParseColor.ParseColor(value);
        Assert.True(color.HasValue, $"Expected '{value}' to be a valid color.");
        return color.Value;
    }
}

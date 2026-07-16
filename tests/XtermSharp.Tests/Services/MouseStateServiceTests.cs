using XtermSharp.Internal;

namespace XtermSharp.Tests.Services;

public sealed class MouseStateServiceTests
{
    private static readonly TerminalMouseEvent BasicEvent = new(
        1,
        1,
        0,
        0,
        TerminalMouseButton.Left,
        TerminalMouseAction.Down);

    [UpstreamFact("XTJS-1264", "MouseStateService init")]
    public void Initializes_with_no_protocol_and_default_encoding()
    {
        var service = new MouseStateService();
        Assert.Equal("DEFAULT", service.ActiveEncoding);
        Assert.Equal("NONE", service.ActiveProtocol);
    }

    [UpstreamFact("XTJS-1265", "MouseStateService default protocols - NONE, X10, VT200, DRAG, ANY")]
    public void Includes_all_default_protocols()
    {
        var service = new MouseStateService();
        Assert.Equal(["NONE", "X10", "VT200", "DRAG", "ANY"], service.ProtocolNames);
    }

    [UpstreamFact("XTJS-1266", "MouseStateService default encodings - DEFAULT, SGR")]
    public void Includes_all_default_encodings()
    {
        var service = new MouseStateService();
        Assert.Equal(["DEFAULT", "SGR", "SGR_PIXELS"], service.EncodingNames);
    }

    [UpstreamFact("XTJS-1267", "MouseStateService protocol/encoding setter, reset")]
    public void Protocol_and_encoding_can_be_set_and_reset()
    {
        var service = new MouseStateService
        {
            ActiveEncoding = "SGR",
            ActiveProtocol = "ANY"
        };
        Assert.Equal("SGR", service.ActiveEncoding);
        Assert.Equal("ANY", service.ActiveProtocol);
        service.Reset();
        Assert.Equal("DEFAULT", service.ActiveEncoding);
        Assert.Equal("NONE", service.ActiveProtocol);
        Assert.Throws<ArgumentException>(() => service.ActiveEncoding = "xyz");
        Assert.Throws<ArgumentException>(() => service.ActiveProtocol = "xyz");
    }

    [UpstreamFact("XTJS-1268", "MouseStateService addEncoding")]
    public void Custom_encoding_can_be_added()
    {
        var service = new MouseStateService();
        service.AddEncoding("XYZ", _ => "custom");
        service.ActiveEncoding = "XYZ";
        Assert.Equal("XYZ", service.ActiveEncoding);
    }

    [UpstreamFact("XTJS-1269", "MouseStateService addProtocol")]
    public void Custom_protocol_can_be_added()
    {
        var service = new MouseStateService();
        service.AddProtocol("XYZ", TerminalMouseEventTypes.None, value => (false, value));
        service.ActiveProtocol = "XYZ";
        Assert.Equal("XYZ", service.ActiveProtocol);
    }

    [UpstreamFact("XTJS-1270", "MouseStateService onProtocolChange")]
    public void Protocol_change_reports_requested_event_types()
    {
        var service = new MouseStateService();
        var events = new List<TerminalMouseEventTypes>();
        service.ProtocolChanged += events.Add;
        service.ActiveProtocol = "NONE";
        service.ActiveProtocol = "ANY";
        Assert.Equal(
            [
                TerminalMouseEventTypes.None,
                TerminalMouseEventTypes.Down | TerminalMouseEventTypes.Up | TerminalMouseEventTypes.Wheel |
                TerminalMouseEventTypes.Drag | TerminalMouseEventTypes.Move
            ],
            events);
    }

    [UpstreamFact("XTJS-1271", "MouseStateService restrictMouseEvent/encodeMouseEvent")]
    public void Active_protocol_restricts_and_active_encoding_encodes()
    {
        var service = new MouseStateService
        {
            ActiveProtocol = "ANY",
            ActiveEncoding = "DEFAULT"
        };
        Assert.True(service.TryEncode(BasicEvent, out string sequence));
        Assert.Equal([0x1B, 0x5B, 0x4D, 0x20, 0x21, 0x21], sequence.Select(value => (int)value));
    }
}

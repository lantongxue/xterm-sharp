using System.Text;
using XtermSharp.Internal;
using XtermSharp.Internal.Parser;

namespace XtermSharp.Tests.Parser;

internal sealed class EscapeParserRecorder
{
    internal List<string> Calls { get; } = [];

    internal EscapeSequenceParser CreateParser()
    {
        var parser = new EscapeSequenceParser();
        parser.SetPrintHandler(data => Calls.Add("print:" + TextDecoder.Utf32ToString(data)));
        parser.SetExecuteHandlerFallback(code => Calls.Add($"execute:{code}"));
        parser.SetEscHandlerFallback(identifier => Calls.Add("esc:" + EscapeSequenceParser.IdentifierToString(identifier)));
        parser.SetCsiHandlerFallback((identifier, parameters) =>
            Calls.Add($"csi:{EscapeSequenceParser.IdentifierToString(identifier)}:{EscapeParserTestSupport.FormatParameters(parameters)}"));
        parser.SetOscHandlerFallback((identifier, action, payload) =>
            Calls.Add($"osc:{identifier}:{action}:{Payload(payload)}"));
        parser.SetDcsHandlerFallback((identifier, action, payload) =>
            Calls.Add($"dcs:{EscapeSequenceParser.IdentifierToString(identifier)}:{action}:{Payload(payload)}"));
        parser.SetApcHandlerFallback((identifier, action, payload) =>
            Calls.Add($"apc:{EscapeSequenceParser.IdentifierToString(identifier)}:{action}:{Payload(payload)}"));
        return parser;
    }

    private static string Payload(object? payload) => payload switch
    {
        null => string.Empty,
        bool value => value ? "true" : "false",
        CsiParameters parameters => EscapeParserTestSupport.FormatParameters(parameters),
        _ => payload.ToString() ?? string.Empty
    };
}

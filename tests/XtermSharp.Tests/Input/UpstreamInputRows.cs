using XtermSharp.TestSupport;

namespace XtermSharp.Tests.Input;

internal static class UpstreamInputRows
{
    internal static TheoryData<string> ForFile(string file)
    {
        var data = new TheoryData<string>();
        foreach (UpstreamTestCase test in UpstreamManifest.LoadEmbedded().Tests.Where(test => test.File == file))
        {
            data.Add(new TheoryDataRow<string>(test.Id)
            {
                TestDisplayName = $"{test.Id} {test.FullTitle}"
            });
        }
        return data;
    }
}

internal static class KeyEvent
{
    internal static TerminalKeyEvent Create(
        string key = "",
        string code = "",
        int keyCode = 0,
        TerminalModifiers modifiers = TerminalModifiers.None,
        TerminalKeyEventType eventType = TerminalKeyEventType.Press) =>
        new(key, code, keyCode, modifiers, eventType);
}

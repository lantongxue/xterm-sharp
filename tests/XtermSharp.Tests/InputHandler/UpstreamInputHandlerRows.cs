using XtermSharp.TestSupport;

namespace XtermSharp.Tests.InputHandler;

internal static class UpstreamInputHandlerRows
{
    private const string UpstreamFile = "src/common/InputHandler.test.ts";

    internal static TheoryData<string> ForRange(int firstId, int lastId)
    {
        var data = new TheoryData<string>();
        foreach (UpstreamTestCase test in UpstreamManifest.LoadEmbedded().Tests.Where(test =>
                     test.File == UpstreamFile &&
                     int.Parse(test.Id.AsSpan(5), System.Globalization.CultureInfo.InvariantCulture) is var id &&
                     id >= firstId &&
                     id <= lastId))
        {
            data.Add(new TheoryDataRow<string>(test.Id)
            {
                TestDisplayName = $"{test.Id} {test.FullTitle}"
            });
        }
        return data;
    }
}

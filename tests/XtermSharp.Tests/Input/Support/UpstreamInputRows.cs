using XtermSharp.TestSupport;

namespace XtermSharp.Tests.Input.Support;

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

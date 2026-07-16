using XtermSharp.TestSupport;

namespace XtermSharp.Tests.Parser;

internal static class UpstreamParserRows
{
    internal static TheoryData<string> ForFile(string file, int minimumId = 1, int maximumId = 9999)
    {
        var data = new TheoryData<string>();
        foreach (UpstreamTestCase test in UpstreamManifest.LoadEmbedded().Tests.Where(test =>
                     test.File == file &&
                     int.Parse(test.Id.AsSpan(5), System.Globalization.CultureInfo.InvariantCulture) is var id &&
                     id >= minimumId &&
                     id <= maximumId))
        {
            data.Add(new TheoryDataRow<string>(test.Id)
            {
                TestDisplayName = $"{test.Id} {test.FullTitle}"
            });
        }
        return data;
    }
}

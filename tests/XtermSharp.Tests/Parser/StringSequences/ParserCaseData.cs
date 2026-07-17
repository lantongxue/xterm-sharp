using System.Collections.Immutable;
using XtermSharp.Internal;
using XtermSharp.Internal.Parser;
using XtermSharp.TestSupport;

namespace XtermSharp.Tests.Parser;

internal static class ParserCaseData
{
    public static TheoryData<string, string, int> For(string file)
    {
        UpstreamTestManifest manifest = UpstreamManifest.LoadEmbedded();
        var data = new TheoryData<string, string, int>();
        int scenario = 0;
        foreach (UpstreamTestCase test in manifest.Tests.Where(test => test.File == file))
        {
            data.Add(new TheoryDataRow<string, string, int>(test.Id, test.FullTitle, scenario++)
            {
                TestDisplayName = $"{test.Id} {test.FullTitle}"
            });
        }
        return data;
    }
}

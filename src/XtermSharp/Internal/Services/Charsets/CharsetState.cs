using System.Collections.ObjectModel;
using System.Text;

namespace XtermSharp.Internal.Services.Charsets;

internal sealed record CharsetState(
    int GLevel,
    IReadOnlyList<IReadOnlyDictionary<int, string>?> Charsets);

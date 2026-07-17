using System.Text;

namespace XtermSharp.Tests.Services;

internal sealed class DoubleWidthProvider : IUnicodeProvider
{
    public string Version => "123";

    public int GetWidth(Rune rune) => 2;
}

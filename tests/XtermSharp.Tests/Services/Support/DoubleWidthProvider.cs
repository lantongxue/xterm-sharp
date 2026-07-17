using System.Text;

namespace XtermSharp.Tests.Services.Support;

internal sealed class DoubleWidthProvider : IUnicodeProvider
{
    public string Version => "123";

    public int GetWidth(Rune rune) => 2;
}

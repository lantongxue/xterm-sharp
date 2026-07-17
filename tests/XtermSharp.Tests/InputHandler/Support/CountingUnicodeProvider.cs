using System.Text;

namespace XtermSharp.Tests.InputHandler.Support;

internal sealed class CountingUnicodeProvider : IUnicodeProvider
{
    public string Version => "input-handler-counting";

    public int WidthCalls { get; private set; }

    public int GetWidth(Rune rune)
    {
        WidthCalls++;
        return 1;
    }
}

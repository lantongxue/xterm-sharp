namespace XtermSharp.Tests.Utilities;

internal sealed class Receiver
{
    public int Value { get; private set; }

    public void Handle(int value) => Value = value;
}

namespace XtermSharp.Tests.Utilities.Support;

internal sealed class Receiver
{
    public int Value { get; private set; }

    public void Handle(int value) => Value = value;
}

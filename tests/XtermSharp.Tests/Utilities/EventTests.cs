using XtermSharp.Internal.Utilities;
using XtermSharp.TestSupport;

namespace XtermSharp.Tests.Utilities;

public sealed class EventTests
{
    [UpstreamFact("XTJS-0187", "Emitter should fire with 0 listeners without error")]
    public void Fire_ShouldWorkWithZeroListeners()
    {
        var emitter = new Emitter<int>();
        emitter.Fire(42);
    }

    [UpstreamFact("XTJS-0188", "Emitter should fire with 1 listener")]
    public void Fire_ShouldFireWithOneListener()
    {
        var emitter = new Emitter<int>();
        int? received = null;
        emitter.Event(value => received = value);
        emitter.Fire(42);
        Assert.Equal(42, received);
    }

    [UpstreamFact("XTJS-0189", "Emitter should fire with 1 listener using thisArgs")]
    public void Fire_ShouldFireWithOneListenerUsingBoundTarget()
    {
        var emitter = new Emitter<int>();
        var receiver = new Receiver();
        emitter.Event(receiver.Handle);
        emitter.Fire(42);
        Assert.Equal(42, receiver.Value);
    }

    [UpstreamFact("XTJS-0190", "Emitter should fire with multiple listeners")]
    public void Fire_ShouldFireWithMultipleListeners()
    {
        var emitter = new Emitter<int>();
        var results = new List<int>();
        emitter.Event(value => results.Add(value));
        emitter.Event(value => results.Add(value * 2));
        emitter.Event(value => results.Add(value * 3));
        emitter.Fire(10);
        Assert.Equal(new[] { 10, 20, 30 }, results);
    }

    [UpstreamFact("XTJS-0191", "Emitter should handle listener removal during fire")]
    public void Fire_ShouldHandleListenerRemovalDuringFire()
    {
        var emitter = new Emitter<int>();
        var results = new List<string>();
        emitter.Event(_ => results.Add("first"));
        IDisposable? disposable = null;
        disposable = emitter.Event(_ =>
        {
            results.Add("second");
            disposable!.Dispose();
        });
        emitter.Event(_ => results.Add("third"));
        emitter.Fire(1);
        Assert.Equal(new[] { "first", "second", "third" }, results);
    }

    [UpstreamFact("XTJS-0192", "Emitter should not fire after dispose")]
    public void Fire_ShouldNotFireAfterDispose()
    {
        var emitter = new Emitter<int>();
        bool called = false;
        emitter.Event(_ => called = true);
        emitter.Dispose();
        emitter.Fire(42);
        Assert.False(called);
    }

    [UpstreamFact("XTJS-0193", "Emitter should allow disposing a listener")]
    public void Event_ShouldAllowDisposingAListener()
    {
        var emitter = new Emitter<int>();
        int count = 0;
        IDisposable disposable = emitter.Event(_ => count++);
        emitter.Fire(1);
        disposable.Dispose();
        emitter.Fire(2);
        Assert.Equal(1, count);
    }

}

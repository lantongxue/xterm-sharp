using XtermSharp.Internal.Utilities;
using XtermSharp.TestSupport;

namespace XtermSharp.Tests.Utilities;

public sealed class CircularListTests
{
    [UpstreamFact("XTJS-0140", "CircularList push should push values onto the array")]
    public void Push_ShouldPushValuesOntoTheArray()
    {
        var list = new CircularList<string>(5);
        list.Push("1");
        list.Push("2");
        list.Push("3");
        list.Push("4");
        list.Push("5");
        Assert.Equal("1", list.Get(0));
        Assert.Equal("2", list.Get(1));
        Assert.Equal("3", list.Get(2));
        Assert.Equal("4", list.Get(3));
        Assert.Equal("5", list.Get(4));
    }

    [UpstreamFact("XTJS-0141", "CircularList push should push old values from the start out of the array when max length is reached")]
    public void Push_ShouldPushOldValuesFromTheStartOutOfTheArrayWhenMaxLengthIsReached()
    {
        var list = new CircularList<string>(2);
        list.Push("1");
        list.Push("2");
        Assert.Equal("1", list.Get(0));
        Assert.Equal("2", list.Get(1));
        list.Push("3");
        Assert.Equal("2", list.Get(0));
        Assert.Equal("3", list.Get(1));
        list.Push("4");
        Assert.Equal("3", list.Get(0));
        Assert.Equal("4", list.Get(1));
    }

    [UpstreamFact("XTJS-0142", "CircularList maxLength should increase the size of the list")]
    public void MaxLength_ShouldIncreaseTheSizeOfTheList()
    {
        var list = new CircularList<string>(2);
        list.Push("1");
        list.Push("2");
        Assert.Equal("1", list.Get(0));
        Assert.Equal("2", list.Get(1));
        list.MaxLength = 4;
        list.Push("3");
        list.Push("4");
        Assert.Equal("1", list.Get(0));
        Assert.Equal("2", list.Get(1));
        Assert.Equal("3", list.Get(2));
        Assert.Equal("4", list.Get(3));
        list.Push("wrapped");
        Assert.Equal("2", list.Get(0));
        Assert.Equal("3", list.Get(1));
        Assert.Equal("4", list.Get(2));
        Assert.Equal("wrapped", list.Get(3));
    }

    [UpstreamFact("XTJS-0143", "CircularList maxLength should return the maximum length of the list")]
    public void MaxLength_ShouldReturnTheMaximumLengthOfTheList()
    {
        var list = new CircularList<string>(2);
        Assert.Equal(2, list.MaxLength);
        list.Push("1");
        list.Push("2");
        Assert.Equal(2, list.MaxLength);
        list.Push("3");
        Assert.Equal(2, list.MaxLength);
        list.MaxLength = 4;
        Assert.Equal(4, list.MaxLength);
    }

    [UpstreamFact("XTJS-0144", "CircularList length should return the current length of the list, capped at the maximum length")]
    public void Length_ShouldReturnTheCurrentLengthCappedAtTheMaximumLength()
    {
        var list = new CircularList<string>(2);
        Assert.Equal(0, list.Length);
        list.Push("1");
        Assert.Equal(1, list.Length);
        list.Push("2");
        Assert.Equal(2, list.Length);
        list.Push("3");
        Assert.Equal(2, list.Length);
    }

    [UpstreamFact("XTJS-0145", "CircularList splice should delete items")]
    public void Splice_ShouldDeleteItems()
    {
        var list = new CircularList<string>(2);
        list.Push("1");
        list.Push("2");
        list.Splice(0, 1);
        Assert.Equal(1, list.Length);
        Assert.Equal("2", list.Get(0));
        list.Push("3");
        list.Splice(1, 1);
        Assert.Equal(1, list.Length);
        Assert.Equal("2", list.Get(0));
    }

    [UpstreamFact("XTJS-0146", "CircularList splice should insert items")]
    public void Splice_ShouldInsertItems()
    {
        var list = new CircularList<string>(2);
        list.Push("1");
        list.Splice(0, 0, "2");
        Assert.Equal(2, list.Length);
        Assert.Equal("2", list.Get(0));
        Assert.Equal("1", list.Get(1));
        list.Splice(1, 0, "3");
        Assert.Equal(2, list.Length);
        Assert.Equal("3", list.Get(0));
        Assert.Equal("1", list.Get(1));
    }

    [UpstreamFact("XTJS-0147", "CircularList splice should delete items then insert items")]
    public void Splice_ShouldDeleteItemsThenInsertItems()
    {
        var list = new CircularList<string>(3);
        list.Push("1");
        list.Push("2");
        list.Splice(0, 1, "3", "4");
        Assert.Equal(3, list.Length);
        Assert.Equal("3", list.Get(0));
        Assert.Equal("4", list.Get(1));
        Assert.Equal("2", list.Get(2));
    }

    [UpstreamFact("XTJS-0148", "CircularList splice should wrap the array correctly when more items are inserted than deleted")]
    public void Splice_ShouldWrapTheArrayCorrectlyWhenMoreItemsAreInsertedThanDeleted()
    {
        var list = new CircularList<string>(3);
        list.Push("1");
        list.Push("2");
        list.Splice(1, 0, "3", "4");
        Assert.Equal(3, list.Length);
        Assert.Equal("3", list.Get(0));
        Assert.Equal("4", list.Get(1));
        Assert.Equal("2", list.Get(2));
    }

    [UpstreamFact("XTJS-0149", "CircularList trimStart should remove items from the beginning of the list")]
    public void TrimStart_ShouldRemoveItemsFromTheBeginningOfTheList()
    {
        var list = new CircularList<string>(5);
        list.Push("1");
        list.Push("2");
        list.Push("3");
        list.Push("4");
        list.Push("5");
        list.TrimStart(1);
        Assert.Equal(4, list.Length);
        Assert.Equal("2", list.Get(0));
        Assert.Equal("3", list.Get(1));
        Assert.Equal("4", list.Get(2));
        Assert.Equal("5", list.Get(3));
        list.TrimStart(2);
        Assert.Equal(2, list.Length);
        Assert.Equal("4", list.Get(0));
        Assert.Equal("5", list.Get(1));
    }

    [UpstreamFact("XTJS-0150", "CircularList trimStart should remove all items if the requested trim amount is larger than the list's length")]
    public void TrimStart_ShouldRemoveAllItemsIfTheRequestedAmountIsLargerThanTheLength()
    {
        var list = new CircularList<string>(5);
        list.Push("1");
        list.TrimStart(2);
        Assert.Equal(0, list.Length);
    }

    [UpstreamFact("XTJS-0151", "CircularList shiftElements should not mutate the list when count is 0")]
    public void ShiftElements_ShouldNotMutateTheListWhenCountIsZero()
    {
        var list = new CircularList<int>(5);
        list.Push(1);
        list.Push(2);
        list.ShiftElements(0, 0, 1);
        Assert.Equal(2, list.Length);
        Assert.Equal(1, list.Get(0));
        Assert.Equal(2, list.Get(1));
    }

    [UpstreamFact("XTJS-0152", "CircularList shiftElements should throw for invalid args")]
    public void ShiftElements_ShouldThrowForInvalidArgs()
    {
        var list = new CircularList<int>(5);
        list.Push(1);
        ArgumentOutOfRangeException beforeStart = Assert.Throws<ArgumentOutOfRangeException>(() => list.ShiftElements(-1, 1, 1));
        Assert.Contains("start argument out of range", beforeStart.Message);
        ArgumentOutOfRangeException afterEnd = Assert.Throws<ArgumentOutOfRangeException>(() => list.ShiftElements(1, 1, 1));
        Assert.Contains("start argument out of range", afterEnd.Message);
        ArgumentOutOfRangeException negativeTarget = Assert.Throws<ArgumentOutOfRangeException>(() => list.ShiftElements(0, 1, -1));
        Assert.Contains("Cannot shift elements in list beyond index 0", negativeTarget.Message);
    }

    [UpstreamFact("XTJS-0153", "CircularList shiftElements should shift an element forward")]
    public void ShiftElements_ShouldShiftAnElementForward()
    {
        var list = new CircularList<int>(5);
        list.Push(1);
        list.Push(2);
        list.ShiftElements(0, 1, 1);
        Assert.Equal(2, list.Length);
        Assert.Equal(1, list.Get(0));
        Assert.Equal(1, list.Get(1));
    }

    [UpstreamFact("XTJS-0154", "CircularList shiftElements should shift elements forward")]
    public void ShiftElements_ShouldShiftElementsForward()
    {
        var list = new CircularList<int>(5);
        list.Push(1);
        list.Push(2);
        list.Push(3);
        list.Push(4);
        list.ShiftElements(0, 2, 2);
        Assert.Equal(4, list.Length);
        Assert.Equal(1, list.Get(0));
        Assert.Equal(2, list.Get(1));
        Assert.Equal(1, list.Get(2));
        Assert.Equal(2, list.Get(3));
    }

    [UpstreamFact("XTJS-0155", "CircularList shiftElements should shift elements forward, expanding the list if needed")]
    public void ShiftElements_ShouldShiftElementsForwardExpandingTheListIfNeeded()
    {
        var list = new CircularList<int>(5);
        list.Push(1);
        list.Push(2);
        list.ShiftElements(0, 2, 2);
        Assert.Equal(4, list.Length);
        Assert.Equal(1, list.Get(0));
        Assert.Equal(2, list.Get(1));
        Assert.Equal(1, list.Get(2));
        Assert.Equal(2, list.Get(3));
    }

    [UpstreamFact("XTJS-0156", "CircularList shiftElements should shift elements forward, wrapping the list if needed")]
    public void ShiftElements_ShouldShiftElementsForwardWrappingTheListIfNeeded()
    {
        var list = new CircularList<int>(5);
        list.Push(1);
        list.Push(2);
        list.Push(3);
        list.Push(4);
        list.Push(5);
        list.ShiftElements(2, 2, 3);
        Assert.Equal(5, list.Length);
        Assert.Equal(3, list.Get(0));
        Assert.Equal(4, list.Get(1));
        Assert.Equal(5, list.Get(2));
        Assert.Equal(3, list.Get(3));
        Assert.Equal(4, list.Get(4));
    }

    [UpstreamFact("XTJS-0157", "CircularList shiftElements should shift an element backwards")]
    public void ShiftElements_ShouldShiftAnElementBackwards()
    {
        var list = new CircularList<int>(5);
        list.Push(1);
        list.Push(2);
        list.ShiftElements(1, 1, -1);
        Assert.Equal(2, list.Length);
        Assert.Equal(2, list.Get(0));
        Assert.Equal(2, list.Get(1));
    }

    [UpstreamFact("XTJS-0158", "CircularList shiftElements should shift elements backwards")]
    public void ShiftElements_ShouldShiftElementsBackwards()
    {
        var list = new CircularList<int>(5);
        list.Push(1);
        list.Push(2);
        list.Push(3);
        list.Push(4);
        list.ShiftElements(2, 2, -2);
        Assert.Equal(4, list.Length);
        Assert.Equal(3, list.Get(0));
        Assert.Equal(4, list.Get(1));
        Assert.Equal(3, list.Get(2));
        Assert.Equal(4, list.Get(3));
    }
}

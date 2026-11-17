namespace MusselChart;

using System.Collections.Specialized;

public sealed class ChartRingBufferTests
{
    [Fact]
    public void PushKeepsLatestItemsInOrder()
    {
        var buffer = new ChartRingBuffer<int>(3);

        for (var i = 1; i <= 5; i++)
        {
            buffer.Push(i);
        }

        Assert.Equal(3, buffer.Count);
        Assert.Equal(5, buffer.Sequence);
        Assert.Equal([3, 4, 5], buffer.ToArray());
        Assert.Equal(3, buffer[0]);
        Assert.Equal(5, buffer[2]);
    }

    [Fact]
    public void ReadReturnsOnlyAppendedItems()
    {
        var buffer = new ChartRingBuffer<int>(4);
        buffer.Push(1);
        buffer.Push(2);
        var first = buffer.Read(-1, 0, null, []);

        buffer.Push(3);
        var items = new List<int>();
        var read = buffer.Read(first.Sequence, first.Generation, null, items);

        Assert.False(first.Appended);
        Assert.True(read.Appended);
        Assert.Equal([3], items);
        Assert.Equal(3, read.Sequence);
        Assert.Equal(3, read.Count);
    }

    [Fact]
    public void ReadReturnsAllItemsWhenGapIsLargerThanCapacity()
    {
        var buffer = new ChartRingBuffer<int>(3);
        buffer.Push(1);
        var first = buffer.Read(-1, 0, null, []);
        for (var i = 2; i <= 6; i++)
        {
            buffer.Push(i);
        }

        var items = new List<int>();
        var read = buffer.Read(first.Sequence, first.Generation, null, items);

        Assert.False(read.Appended);
        Assert.Equal([4, 5, 6], items);
    }

    [Fact]
    public void ReadReturnsAllItemsAfterClear()
    {
        var buffer = new ChartRingBuffer<int>(3);
        buffer.Push(1);
        var first = buffer.Read(-1, 0, null, []);

        buffer.Clear();
        buffer.Push(7);
        var items = new List<int>();
        var read = buffer.Read(first.Sequence, first.Generation, null, items);

        Assert.False(read.Appended);
        Assert.Equal([7], items);
        Assert.Single(buffer);
    }

    [Fact]
    public void ReadUntilAMarkThatNewerItemsPassedReadsNothing()
    {
        var buffer = new ChartRingBuffer<int>(3);
        buffer.Push(1);
        buffer.Push(2);
        var first = buffer.Read(-1, 0, null, []);
        var mark = ((IChartBuffer)buffer).Mark();
        for (var i = 3; i <= 6; i++)
        {
            buffer.Push(i);
        }

        var items = new List<int>();
        var read = buffer.Read(first.Sequence, first.Generation, mark, items);

        Assert.Empty(items);
        Assert.False(read.Appended);
        Assert.Equal(0, read.Count);
    }

    [Fact]
    public void ReadUntilAMarkOfAnotherGenerationReadsTheItemsThere()
    {
        var buffer = new ChartRingBuffer<int>(3);
        buffer.Push(1);
        buffer.Push(2);
        var first = buffer.Read(-1, 0, null, []);
        var mark = ((IChartBuffer)buffer).Mark();
        buffer.Clear();
        buffer.Push(7);

        var items = new List<int>();
        var read = buffer.Read(first.Sequence, first.Generation, mark, items);

        Assert.Equal([7], items);
        Assert.False(read.Appended);
    }

    [Fact]
    public void PushAndClearTellThatTheItemsChanged()
    {
        var buffer = new ChartRingBuffer<int>(2);
        var actions = new List<NotifyCollectionChangedAction>();
        var counts = new List<int>();
        buffer.CollectionChanged += (_, e) =>
        {
            actions.Add(e.Action);
            counts.Add(buffer.Count);
        };

        buffer.Push(1);
        buffer.Push(2);
        buffer.Push(3);
        buffer.Clear();

        Assert.All(actions, static x => Assert.Equal(NotifyCollectionChangedAction.Reset, x));
        Assert.Equal([1, 2, 2, 0], counts);
    }

    [Fact]
    public void IndexerRejectsOutOfRange()
    {
        var buffer = new ChartRingBuffer<int>(2);
        buffer.Push(1);

        Assert.Throws<ArgumentOutOfRangeException>(() => buffer[1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChartRingBuffer<int>(0));
    }
}

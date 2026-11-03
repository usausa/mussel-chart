namespace MusselChart;

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
        var first = buffer.Read(-1, 0, []);

        buffer.Push(3);
        var items = new List<int>();
        var read = buffer.Read(first.Sequence, first.Generation, items);

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
        var first = buffer.Read(-1, 0, []);
        for (var i = 2; i <= 6; i++)
        {
            buffer.Push(i);
        }

        var items = new List<int>();
        var read = buffer.Read(first.Sequence, first.Generation, items);

        Assert.False(read.Appended);
        Assert.Equal([4, 5, 6], items);
    }

    [Fact]
    public void ReadReturnsAllItemsAfterClear()
    {
        var buffer = new ChartRingBuffer<int>(3);
        buffer.Push(1);
        var first = buffer.Read(-1, 0, []);

        buffer.Clear();
        buffer.Push(7);
        var items = new List<int>();
        var read = buffer.Read(first.Sequence, first.Generation, items);

        Assert.False(read.Appended);
        Assert.Equal([7], items);
        Assert.Single(buffer);
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

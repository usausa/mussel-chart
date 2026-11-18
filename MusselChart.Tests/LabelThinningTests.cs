namespace MusselChart;

using MusselChart.Internal;

public sealed class LabelThinningTests
{
    [Fact]
    public void ThinningKeepsBothEnds()
    {
        var labels = Enumerable.Range(0, 11).Select(static i => new LabelExtent(i * 10, (i * 10) + 18)).ToList();
        var selected = new List<int>();

        LabelThinning.Select(labels, 4, Int32.MaxValue, selected);

        Assert.Equal(0, selected[0]);
        Assert.Equal(10, selected[^1]);
        Assert.True(selected.Count < labels.Count);
    }
}

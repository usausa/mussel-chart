namespace MusselChart;

using MusselChart.Internal;

public sealed class ChartTextTests
{
    [Fact]
    public void FullWidthCharactersAreWiderThanNarrowOnes()
    {
        Assert.True(ChartText.Measure("あ", 10) > ChartText.Measure("a", 10));
        Assert.True(ChartText.Measure("a", 10) > ChartText.Measure("i", 10));
        Assert.Equal(0, ChartText.Measure(null, 10));
    }

    [Fact]
    public void TruncateEndsWithAnEllipsis()
    {
        Assert.Equal("Short", ChartText.Truncate("Short", 10, 1000));
        var text = ChartText.Truncate("A very long text to be cut", 10, 60);
        Assert.EndsWith(ChartText.Ellipsis, text, StringComparison.Ordinal);
        Assert.True(ChartText.Measure(text, 10) <= 60);
        Assert.Empty(ChartText.Truncate("Long", 10, 1));
    }

    [Fact]
    public void TipEscapesItsTexts()
    {
        var tip = ChartTip.Create("a\"b", [new ChartTipRow("#000000", "c\\d", "e\nf")], [new ChartTipMark(1.25, 2, "#FFFFFF")], new ChartTipLine(1, 2, 3, 4));

        Assert.Equal("{\"t\":\"a\\\"b\",\"r\":[[\"#000000\",\"c\\\\d\",\"e\\nf\"]],\"m\":[[1.2,2,\"#FFFFFF\"]],\"l\":[1,2,3,4]}", tip.Replace("1.3", "1.2", StringComparison.Ordinal));
    }

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

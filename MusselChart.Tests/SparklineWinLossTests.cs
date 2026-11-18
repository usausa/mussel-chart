namespace MusselChart;

public sealed class SparklineWinLossTests : BunitContext
{
    [Fact]
    public void WinsFillTheUpperHalfAndLossesTheLower()
    {
        var cut = Render<MusselSparklineView>(parameters => parameters
            .Add(static p => p.Values, [1d, -2, 0, Double.NaN, 3])
            .Add(static p => p.Kind, ChartSparklineKind.WinLoss)
            .Add(static p => p.Height, 20));

        var bars = cut.FindAll("rect");
        Assert.Equal(3, bars.Count);
        Assert.Equal("0", bars[0].GetAttribute("y"));
        Assert.Equal("47.5", bars[0].GetAttribute("height"));
        Assert.Equal("52.5", bars[1].GetAttribute("y"));
        Assert.Equal("0 0 5 100", cut.Find("svg").GetAttribute("viewBox"));
    }

    [Fact]
    public void LossesUseTheCriticalColorUnlessSet()
    {
        var plain = Render<MusselSparklineView>(parameters => parameters
            .Add(static p => p.Values, [1d, -2])
            .Add(static p => p.Kind, ChartSparklineKind.WinLoss)
            .Add(static p => p.Color, "#00AA00"));
        var colored = Render<MusselSparklineView>(parameters => parameters
            .Add(static p => p.Values, [1d, -2])
            .Add(static p => p.Kind, ChartSparklineKind.WinLoss)
            .Add(static p => p.NegativeColor, "#AA0000"));

        var bars = plain.FindAll("rect");
        Assert.Equal("#00AA00", bars[0].GetAttribute("fill"));
        Assert.Equal("mussel-sparkline-loss", bars[1].ClassList[0]);
        Assert.Null(bars[1].GetAttribute("fill"));
        Assert.Equal("#AA0000", colored.FindAll("rect")[1].GetAttribute("fill"));
    }

    [Fact]
    public void NegativeBarsKeepTheColorUnlessSet()
    {
        var plain = Render<MusselSparklineView>(parameters => parameters
            .Add(static p => p.Values, [1d, -2])
            .Add(static p => p.Kind, ChartSparklineKind.Bar)
            .Add(static p => p.Color, "#00AA00"));
        var colored = Render<MusselSparklineView>(parameters => parameters
            .Add(static p => p.Values, [1d, -2])
            .Add(static p => p.Kind, ChartSparklineKind.Bar)
            .Add(static p => p.Color, "#00AA00")
            .Add(static p => p.NegativeColor, "#AA0000"));

        Assert.Equal("#00AA00", plain.FindAll("rect")[1].GetAttribute("fill"));
        Assert.Equal("#AA0000", colored.FindAll("rect")[1].GetAttribute("fill"));
    }
}

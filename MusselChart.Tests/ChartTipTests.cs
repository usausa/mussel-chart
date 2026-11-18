namespace MusselChart;

using MusselChart.Internal;

public sealed class ChartTipTests
{
    [Fact]
    public void TipEscapesItsTexts()
    {
        var tip = ChartTip.Create("a\"b", [new ChartTipRow("#000000", "c\\d", "e\nf")], [new ChartTipMark(1.25, 2, "#FFFFFF")], new ChartTipLine(1, 2, 3, 4));

        Assert.Equal("{\"t\":\"a\\\"b\",\"r\":[[\"#000000\",\"c\\\\d\",\"e\\nf\"]],\"m\":[[1.2,2,\"#FFFFFF\"]],\"l\":[1,2,3,4]}", tip.Replace("1.3", "1.2", StringComparison.Ordinal));
    }
}

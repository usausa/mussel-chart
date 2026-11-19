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
    public void TruncateKeepsWholeCharactersOutsideTheBasicPlane()
    {
        Assert.Equal("\U0001F600\U0001F600" + ChartText.Ellipsis, ChartText.Truncate("\U0001F600\U0001F600\U0001F600\U0001F600", 10, 30));
    }
}

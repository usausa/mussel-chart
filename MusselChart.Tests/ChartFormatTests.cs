namespace MusselChart;

using MusselChart.Internal;

public sealed class ChartFormatTests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(240, "240")]
    [InlineData(4096, "4096")]
    [InlineData(-3, "-3")]
    [InlineData(12.34, "12.3")]
    [InlineData(0.04, "0")]
    public void PixelKeepsOneDecimal(double value, string expected)
    {
        Assert.Equal(expected, ChartFormat.Pixel(value));
    }

    [Fact]
    public void PixelDoesNotWriteANegativeZero()
    {
        Assert.Equal("0", ChartFormat.Pixel(-0d));
    }

    [Fact]
    public void PixelSharesTheTextOfWholeNumbers()
    {
        Assert.Same(ChartFormat.Pixel(240), ChartFormat.Pixel(240));
    }
}

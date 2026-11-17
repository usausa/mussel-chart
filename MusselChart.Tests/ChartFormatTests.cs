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

public sealed class ChartStringBuilderTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-0.001)]
    [InlineData(1.005)]
    [InlineData(12.345)]
    [InlineData(-5.5)]
    [InlineData(1234567.891)]
    public void AppendNumberWritesTheTextOfTheGeometry(double value)
    {
        var builder = ChartStringBuilder.Rent();
        builder.AppendNumber(value);

        Assert.Equal(ChartGeometry.Text(value), ChartStringBuilder.ToStringAndReturn(builder));
    }

    [Fact]
    public void ReturnedBuilderIsRentedEmpty()
    {
        var first = ChartStringBuilder.Rent();
        first.Append("text");
        Assert.Equal("text", ChartStringBuilder.ToStringAndReturn(first));

        var second = ChartStringBuilder.Rent();

        Assert.Same(first, second);
        Assert.Equal(0, second.Length);
    }

    [Fact]
    public void BuilderInUseIsNotRentedAgain()
    {
        var first = ChartStringBuilder.Rent();
        var second = ChartStringBuilder.Rent();

        Assert.NotSame(first, second);
    }
}

namespace MusselChart;

using MusselChart.Internal;

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

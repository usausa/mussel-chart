namespace MusselChart;

using MusselChart.Internal;

public sealed class CompactNumberFormatTests
{
    [Theory]
    [InlineData(0.004, "0")]
    [InlineData(1234.567, "1,234.57")]
    [InlineData(2.675, "2.68")]
    [InlineData(1.005, "1.01")]
    [InlineData(-2.675, "-2.68")]
    [InlineData(12350, "12.4K")]
    [InlineData(-12350, "-12.4K")]
    [InlineData(9999.999, "10,000")]
    [InlineData(12345, "12.3K")]
    [InlineData(99995, "100K")]
    [InlineData(999499, "999K")]
    [InlineData(999999, "1M")]
    [InlineData(-999999, "-1M")]
    [InlineData(1234567, "1.23M")]
    [InlineData(999999999, "1B")]
    [InlineData(1e15, "1,000T")]
    [InlineData(1e33, "1,000,000,000,000,000,000,000T")]
    [InlineData(1e34, "10,000,000,000,000,000,000,000T")]
    public void CompactNumbersCarryIntoTheNextUnit(double value, string expected) =>
        Assert.Equal(expected, CompactNumberFormat.Format(value, CultureInfo.GetCultureInfo("en-US")));

    [Theory]
    [InlineData(1234567880, 20, "1.23456788B")]
    [InlineData(1234567900, 20, "1.2345679B")]
    [InlineData(20.002, 0.002, "20.002")]
    [InlineData(20.01, 0.002, "20.01")]
    [InlineData(0.002, 0.002, "0.002")]
    [InlineData(999500, 500, "999.5K")]
    [InlineData(102500000, 2500000, "102.5M")]
    [InlineData(-1500000, 500000, "-1.5M")]
    [InlineData(20.003666666666667, 0.0026666666666666666, "20.004")]
    public void AxisNumbersKeepTheDecimalsOfTheirInterval(double value, double step, string expected) =>
        Assert.Equal(expected, CompactNumberFormat.Format(value, step, CultureInfo.GetCultureInfo("en-US")));

    [Theory]
    [InlineData(0, 20)]
    [InlineData(20, 20)]
    [InlineData(-25, 25)]
    [InlineData(0.2, 0.2)]
    [InlineData(0.75, 0.25)]
    [InlineData(2000, 2000)]
    [InlineData(10000, 2000)]
    [InlineData(12500, 2500)]
    [InlineData(500000, 500000)]
    [InlineData(1500000, 500000)]
    [InlineData(2e9, 5e8)]
    [InlineData(1e15, 2e14)]
    [InlineData(0.36666666666666664, 0.26666666666666666)]
    [InlineData(66.666666666666671, 33.333333333333336)]
    public void AxisNumbersOfAnOrdinaryIntervalAreWrittenAsBefore(double value, double step)
    {
        var culture = CultureInfo.GetCultureInfo("en-US");

        Assert.Equal(CompactNumberFormat.Format(value, culture), CompactNumberFormat.Format(value, step, culture));
    }
}

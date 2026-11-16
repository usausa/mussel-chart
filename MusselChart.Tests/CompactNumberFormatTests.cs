namespace MusselChart;

using System.Globalization;

using MusselChart.Internal;

public sealed class CompactNumberFormatTests
{
    [Theory]
    [InlineData(0.004, "0")]
    [InlineData(1234.567, "1,234.57")]
    [InlineData(9999.999, "10,000")]
    [InlineData(12345, "12.3K")]
    [InlineData(99995, "100K")]
    [InlineData(999499, "999K")]
    [InlineData(999999, "1M")]
    [InlineData(-999999, "-1M")]
    [InlineData(1234567, "1.23M")]
    [InlineData(999999999, "1B")]
    [InlineData(1e15, "1,000T")]
    public void CompactNumbersCarryIntoTheNextUnit(double value, string expected) =>
        Assert.Equal(expected, CompactNumberFormat.Format(value, CultureInfo.GetCultureInfo("en-US")));
}

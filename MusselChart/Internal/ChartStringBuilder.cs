namespace MusselChart.Internal;

using System.Globalization;
using System.Text;

// Long attribute values are built without a builder for each of them and without a string for each number
internal static class ChartStringBuilder
{
    private const int InitialCapacity = 256;

    private const int MaxCachedCapacity = 16384;

    [ThreadStatic]
    private static StringBuilder? cached;

    public static StringBuilder Rent()
    {
        var builder = cached;
        if (builder is null)
        {
            return new StringBuilder(InitialCapacity);
        }

        cached = null;
        return builder.Clear();
    }

    public static string ToStringAndReturn(StringBuilder builder)
    {
        var text = builder.ToString();
        if (builder.Capacity <= MaxCachedCapacity)
        {
            cached = builder;
        }

        return text;
    }

    public static StringBuilder AppendNumber(this StringBuilder builder, double value)
    {
        var rounded = Math.Round(value, 2);
        return builder.Append(CultureInfo.InvariantCulture, $"{(rounded == 0 ? 0 : rounded):0.##}");
    }
}

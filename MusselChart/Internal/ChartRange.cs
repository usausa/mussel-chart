namespace MusselChart.Internal;

internal readonly record struct ChartRangeOptions(double? Minimum, double? Maximum, double? SoftMinimum, double? SoftMaximum, bool IncludeZero, bool Nice);

internal static class ChartRange
{
    public static AxisScale Resolve(double minimum, double maximum, in ChartRangeOptions options, int divisions)
    {
        if (!Double.IsFinite(minimum) || !Double.IsFinite(maximum))
        {
            minimum = 0;
            maximum = 1;
        }

        if (options.IncludeZero)
        {
            minimum = Math.Min(minimum, 0);
            maximum = Math.Max(maximum, 0);
        }

        if (Finite(options.SoftMinimum) is { } softMinimum)
        {
            minimum = Math.Min(minimum, softMinimum);
        }

        if (Finite(options.SoftMaximum) is { } softMaximum)
        {
            maximum = Math.Max(maximum, softMaximum);
        }

        if (maximum <= minimum)
        {
            var delta = Math.Abs(maximum) > 0 ? Math.Abs(maximum) * 0.1 : 1;
            minimum -= options.IncludeZero ? 0 : delta;
            maximum += delta;
        }

        var (fixedMinimum, fixedMaximum) = FixedEnds(options.Minimum, options.Maximum);
        minimum = fixedMinimum ?? minimum;
        maximum = fixedMaximum ?? maximum;
        if (maximum <= minimum)
        {
            if (fixedMaximum is null)
            {
                maximum = Above(minimum);
            }
            else
            {
                minimum = Below(maximum);
            }
        }

        return NiceScale.Create(minimum, maximum, divisions, options.Nice && (fixedMinimum is null), options.Nice && (fixedMaximum is null));
    }

    public static (double? Minimum, double? Maximum) FixedEnds(double? minimum, double? maximum)
    {
        var low = Finite(minimum);
        var high = Finite(maximum);
        return (low is { } start) && (high is { } end) && !(end > start) ? (null, null) : (low, high);
    }

    public static double Above(double end) => end + 1 > end ? end + 1 : end + GetGap(end);

    public static double Below(double end) => end - 1 < end ? end - 1 : end - GetGap(end);

    public static (double Minimum, double Maximum) Pad(double minimum, double maximum, double padding, double length)
    {
        if ((padding <= 0) || !(length > padding * 2) || !Double.IsFinite(minimum) || !Double.IsFinite(maximum))
        {
            return (minimum, maximum);
        }

        var extra = (maximum - minimum) / (1 - (padding * 2 / length)) * padding / length;
        return (minimum - extra, maximum + extra);
    }

    private static double? Finite(double? value) => (value is { } number) && Double.IsFinite(number) ? number : null;

    private static double GetGap(double end)
    {
        var magnitude = Math.Abs(end);
        return Math.Min(magnitude * 0.1, Double.MaxValue - magnitude);
    }
}

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

        if (options.SoftMinimum is { } softMinimum)
        {
            minimum = Math.Min(minimum, softMinimum);
        }

        if (options.SoftMaximum is { } softMaximum)
        {
            maximum = Math.Max(maximum, softMaximum);
        }

        if (maximum <= minimum)
        {
            var delta = Math.Abs(maximum) > 0 ? Math.Abs(maximum) * 0.1 : 1;
            minimum -= delta;
            maximum += delta;
        }

        var fixedMinimum = options.Minimum;
        var fixedMaximum = options.Maximum;
        if ((fixedMinimum is { } low) && (fixedMaximum is { } high) && !(high > low))
        {
            fixedMinimum = null;
            fixedMaximum = null;
        }

        minimum = fixedMinimum ?? minimum;
        maximum = fixedMaximum ?? maximum;
        if (maximum <= minimum)
        {
            maximum = minimum + 1;
        }

        return NiceScale.Create(minimum, maximum, divisions, options.Nice && (fixedMinimum is null), options.Nice && (fixedMaximum is null));
    }

    // Widens a range so that marks of a size in pixels at its ends stay inside the length
    public static (double Minimum, double Maximum) Pad(double minimum, double maximum, double padding, double length)
    {
        if ((padding <= 0) || !(length > padding * 2) || !Double.IsFinite(minimum) || !Double.IsFinite(maximum))
        {
            return (minimum, maximum);
        }

        var extra = (maximum - minimum) / (1 - (padding * 2 / length)) * padding / length;
        return (minimum - extra, maximum + extra);
    }
}

namespace MusselChart.Internal;

using System.Globalization;

internal enum ChartSeriesKind
{
    Line,
    Area,
    Bar,
    RangeBar,
    Scatter,
    Bubble
}

internal enum ChartXKind
{
    Time,
    Number,
    Category
}

internal readonly record struct ChartPointData(long Ticks, double Number, string? Category, double Value, double Low, double Size);

internal sealed record SeriesOptions(
    string? Title,
    string? Color,
    bool IsVisible,
    bool LegendVisible,
    string? ValueFormat,
    Func<double, string>? ValueFormatter,
    object? Source,
    ChartXKind XKind,
    double Thickness,
    double Opacity,
    string? Stack,
    bool Live)
{
    public ChartMarkerShape Shape { get; init; }

    public double MinSize { get; init; }

    public double MaxSize { get; init; }

    public bool SizeIncludesZero { get; init; }
}

internal static class ChartTime
{
    private const long OffsetStep = TimeSpan.TicksPerDay * 7;

    private const long MaxOffsetSteps = 4096;

    public static long ToUtcTicks(DateTime value, TimeZoneInfo zone) => value.Kind switch
    {
        DateTimeKind.Utc => value.Ticks,
        DateTimeKind.Local => value.ToUniversalTime().Ticks,
        _ => Math.Clamp(value.Ticks - zone.GetUtcOffset(value).Ticks, 0, DateTime.MaxValue.Ticks)
    };

    public static long ToZoneTicks(long utcTicks, TimeZoneInfo zone) =>
        utcTicks + OffsetOf(utcTicks, zone);

    public static long OffsetOf(long utcTicks, TimeZoneInfo zone) => zone.GetUtcOffset(new DateTime(Math.Clamp(utcTicks, 0, DateTime.MaxValue.Ticks), DateTimeKind.Utc)).Ticks;

    public static void FindOffsetChanges(long start, long end, TimeZoneInfo zone, List<long> changes)
    {
        changes.Clear();
        if (end <= start)
        {
            return;
        }

        var step = Math.Max(OffsetStep, ((end - start) / MaxOffsetSteps) + 1);
        var (low, offset) = (start, OffsetOf(start, zone));
        while (low < end)
        {
            var high = end - low > step ? low + step : end;
            var next = OffsetOf(high, zone);
            if (next != offset)
            {
                changes.Add(FindOffsetChange(low, high, offset, zone));
            }

            (low, offset) = (high, next);
        }
    }

    private static long FindOffsetChange(long low, long high, long offset, TimeZoneInfo zone)
    {
        while (high - low > 1)
        {
            var middle = low + ((high - low) / 2);
            if (OffsetOf(middle, zone) == offset)
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        return high;
    }
}

internal static class ChartFormat
{
    private static readonly string?[] WholePixels = new string?[4096];

    public static string Value(double value, string? format, Func<double, string>? formatter, CultureInfo culture)
    {
        if (formatter is not null)
        {
            return formatter(value);
        }

        if (String.IsNullOrEmpty(format))
        {
            return CompactNumberFormat.Format(value, culture);
        }

        return format.Contains('{', StringComparison.Ordinal) ? String.Format(culture, format, value) : value.ToString(format, culture);
    }

    public static (string? Format, Func<double, string>? Formatter) Pick(string? format, Func<double, string>? formatter, string? fallbackFormat, Func<double, string>? fallbackFormatter) =>
        (format is not null) || (formatter is not null) ? (format, formatter) : (fallbackFormat, fallbackFormatter);

    public static string Time(DateTime value, string? format, Func<DateTime, string>? formatter, string defaultFormat, CultureInfo culture)
    {
        if (formatter is not null)
        {
            return formatter(value);
        }

        if (String.IsNullOrEmpty(format))
        {
            return value.ToString(defaultFormat, CultureInfo.InvariantCulture);
        }

        return format.Contains('{', StringComparison.Ordinal) ? String.Format(culture, format, value) : value.ToString(format, culture);
    }

    public static bool IsDayFirst(CultureInfo culture)
    {
        var pattern = culture.DateTimeFormat.ShortDatePattern;
        var quote = '\0';
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }
            }
            else if (c is '\'' or '"')
            {
                quote = c;
            }
            else if (c == '\\')
            {
                i++;
            }
            else if (c is 'd' or 'M')
            {
                return c == 'd';
            }
        }

        return false;
    }

    public static string MergeClass(string own, IReadOnlyDictionary<string, object>? attributes) =>
        (attributes is not null) && attributes.TryGetValue("class", out var value) && (value is string { Length: > 0 } text) ? $"{own} {text}" : own;

    public static string Pixel(double value)
    {
        var whole = (int)value;
        if ((whole >= 0) && (whole < WholePixels.Length) && value.Equals(whole))
        {
            return WholePixels[whole] ??= whole.ToString(CultureInfo.InvariantCulture);
        }

        return value.ToString("0.#", CultureInfo.InvariantCulture);
    }

    public static string Coordinate(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    public static string Data(double value) => value.ToString("G6", CultureInfo.InvariantCulture);

    public static string Position(double value) => value.ToString("G9", CultureInfo.InvariantCulture);

    public static string LevelClass(ChartThresholdLevel level) => level switch
    {
        ChartThresholdLevel.Good => "mussel-good",
        ChartThresholdLevel.Warning => "mussel-warning",
        ChartThresholdLevel.Serious => "mussel-serious",
        ChartThresholdLevel.Critical => "mussel-critical",
        _ => "mussel-neutral"
    };

    public static ChartThreshold? FindThreshold(IReadOnlyList<ChartThreshold>? thresholds, double value)
    {
        if ((thresholds is null) || Double.IsNaN(value))
        {
            return null;
        }

        ChartThreshold? found = null;
        foreach (var threshold in thresholds)
        {
            if ((threshold.Value <= value) && ((found is null) || (threshold.Value >= found.Value)))
            {
                found = threshold;
            }
        }

        return found;
    }
}

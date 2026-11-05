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
    public static long ToUtcTicks(DateTime value, TimeZoneInfo zone) => value.Kind switch
    {
        DateTimeKind.Utc => value.Ticks,
        DateTimeKind.Local => value.ToUniversalTime().Ticks,
        _ => TimeZoneInfo.ConvertTimeToUtc(value, zone).Ticks
    };

    public static long ToZoneTicks(long utcTicks, TimeZoneInfo zone) =>
        utcTicks + zone.GetUtcOffset(new DateTime(utcTicks, DateTimeKind.Utc)).Ticks;
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

    public static string Time(DateTime value, string? format, Func<DateTime, string>? formatter, string defaultFormat, CultureInfo culture)
    {
        if (formatter is not null)
        {
            return formatter(value);
        }

        if (String.IsNullOrEmpty(format))
        {
            return value.ToString(defaultFormat, culture);
        }

        return format.Contains('{', StringComparison.Ordinal) ? String.Format(culture, format, value) : value.ToString(format, culture);
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

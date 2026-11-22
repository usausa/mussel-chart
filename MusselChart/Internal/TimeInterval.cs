namespace MusselChart.Internal;

internal readonly record struct TimeInterval(ChartDateTimeIntervalType Unit, int Count)
{
    public long MinimumTicks => Count <= (DateTime.MaxValue.Ticks / UnitTicks) ? Math.Max(Count, 1) * UnitTicks : DateTime.MaxValue.Ticks;

    public TimeLabelPrecision LabelPrecision => Unit switch
    {
        ChartDateTimeIntervalType.Months => Count < 2 ? TimeLabelPrecision.Day : TimeLabelPrecision.Month,
        ChartDateTimeIntervalType.Years => TimeLabelPrecision.Month,
        _ => GetFixedPrecision(MinimumTicks)
    };

    private long UnitTicks => Unit switch
    {
        ChartDateTimeIntervalType.Minutes => TimeSpan.TicksPerMinute,
        ChartDateTimeIntervalType.Hours => TimeSpan.TicksPerHour,
        ChartDateTimeIntervalType.Days => TimeSpan.TicksPerDay,
        ChartDateTimeIntervalType.Weeks => TimeSpan.TicksPerDay * 7,
        ChartDateTimeIntervalType.Months => TimeSpan.TicksPerDay * 28,
        ChartDateTimeIntervalType.Years => TimeSpan.TicksPerDay * 365,
        _ => TimeSpan.TicksPerSecond
    };

    public string GetTickFormat(double value, bool dayFirst)
    {
        var precision = LabelPrecision;
        var ticks = (long)value;
        if ((precision > TimeLabelPrecision.Minute) || ((ticks / TimeSpan.TicksPerDay) == ((ticks - MinimumTicks) / TimeSpan.TicksPerDay)))
        {
            return GetDefaultFormat(precision, dayFirst);
        }

        return (ticks % TimeSpan.TicksPerDay) == 0 ? GetDefaultFormat(TimeLabelPrecision.Day, dayFirst) : GetTrackingFormat(precision, true, dayFirst);
    }

    public static string GetDefaultFormat(TimeLabelPrecision precision, bool dayFirst) => precision switch
    {
        TimeLabelPrecision.Second => "HH:mm:ss",
        TimeLabelPrecision.Minute => "HH:mm",
        TimeLabelPrecision.Day => dayFirst ? "d/M" : "M/d",
        _ => "yyyy/M"
    };

    public static string GetTrackingFormat(TimeLabelPrecision precision, bool spansDays, bool dayFirst) => precision switch
    {
        TimeLabelPrecision.Second => spansDays ? (dayFirst ? "d/M HH:mm:ss" : "M/d HH:mm:ss") : "HH:mm:ss",
        TimeLabelPrecision.Minute => spansDays ? (dayFirst ? "d/M HH:mm" : "M/d HH:mm") : "HH:mm",
        _ => GetDefaultFormat(precision, dayFirst)
    };

    public static string GetYearTrackingFormat(TimeLabelPrecision precision, bool dayFirst) => precision switch
    {
        TimeLabelPrecision.Second => dayFirst ? "d/M/yyyy HH:mm:ss" : "yyyy/M/d HH:mm:ss",
        TimeLabelPrecision.Minute => dayFirst ? "d/M/yyyy HH:mm" : "yyyy/M/d HH:mm",
        TimeLabelPrecision.Day => dayFirst ? "d/M/yyyy" : "yyyy/M/d",
        _ => GetDefaultFormat(precision, dayFirst)
    };

    public static TimeLabelPrecision GetValuePrecision(ReadOnlySpan<double> values, TimeLabelPrecision precision)
    {
        foreach (var value in values)
        {
            if (precision == TimeLabelPrecision.Second)
            {
                break;
            }

            if (Double.IsFinite(value) && (value >= 0) && (value <= DateTime.MaxValue.Ticks))
            {
                var current = GetValuePrecision(ChartDateTime.ClampTicks(value));
                if (current < precision)
                {
                    precision = current;
                }
            }
        }

        return precision;
    }

    private static TimeLabelPrecision GetFixedPrecision(long ticks)
    {
        if ((ticks % TimeSpan.TicksPerMinute) != 0)
        {
            return TimeLabelPrecision.Second;
        }

        return (ticks % TimeSpan.TicksPerDay) != 0 ? TimeLabelPrecision.Minute : TimeLabelPrecision.Day;
    }

    private static TimeLabelPrecision GetValuePrecision(long ticks)
    {
        if ((ticks % TimeSpan.TicksPerMinute) != 0)
        {
            return TimeLabelPrecision.Second;
        }

        if ((ticks % TimeSpan.TicksPerDay) != 0)
        {
            return TimeLabelPrecision.Minute;
        }

        return new DateTime(ticks).Day != 1 ? TimeLabelPrecision.Day : TimeLabelPrecision.Month;
    }
}

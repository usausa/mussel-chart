namespace MusselChart;

using System.Globalization;

using MusselChart.Internal;

public sealed partial class MusselChartView
{
    private const double LabelCharWidth = 6.6;

    private const double LabelGap = 6;

    private const double PlotPadding = 8;

    private const double LiveRadius = 10;

    private const double XLabelHeight = 20;

    private const double SpacingSamples = 64;

    private readonly List<AxisLabel> yLabels = [];

    private readonly List<AxisLabel> xLabels = [];

    private readonly List<string> yGridLines = [];

    private readonly List<string> xGridLines = [];

    private readonly List<ThresholdLine> thresholdLines = [];

    private readonly List<ThresholdBand> thresholdBands = [];

    private readonly List<LivePoint> livePoints = [];

    private readonly List<LegendRow> legendRows = [];

    private readonly List<string> legendColumns = [];

    private readonly List<SeriesView> seriesViews = [];

    private readonly List<double> ticks = [];

    private string viewBox = string.Empty;

    private string transform = string.Empty;

    private string baseText = string.Empty;

    private string clipX = string.Empty;

    private string clipY = string.Empty;

    private string clipWidth = string.Empty;

    private string clipHeight = string.Empty;

    private string plotLeftText = string.Empty;

    private string plotRightText = string.Empty;

    private string plotTopText = string.Empty;

    private string plotBottomText = string.Empty;

    private string plotWidthText = string.Empty;

    private string yLabelX = string.Empty;

    private string xLabelY = string.Empty;

    private string cultureName = string.Empty;

    private string offsetText = string.Empty;

    private string timeFormat = string.Empty;

    private string emptyText = string.Empty;

    private bool legendVisible;

    private void Arrange()
    {
        yLabels.Clear();
        xLabels.Clear();
        yGridLines.Clear();
        xGridLines.Clear();
        thresholdLines.Clear();
        thresholdBands.Clear();
        livePoints.Clear();
        legendRows.Clear();
        legendColumns.Clear();
        seriesViews.Clear();

        var palette = Palette ?? ChartPalettes.DefaultLight;
        var colors = new Dictionary<ChartSeries, string>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < seriesList.Count; i++)
        {
            var series = seriesList[i];
            colors[series] = series.Color ?? (palette.Count > 0 ? palette[i % palette.Count] : "#808080");
        }

        var visible = seriesList.Select(x => states[x]).Where(static x => x.Visible).ToList();

        long? earliest = null;
        long? latest = null;
        foreach (var state in visible)
        {
            foreach (var point in state.Points)
            {
                if (Double.IsFinite(point.Value))
                {
                    earliest = earliest is null ? point.Ticks : Math.Min(earliest.Value, point.Ticks);
                    latest = latest is null ? point.Ticks : Math.Max(latest.Value, point.Ticks);
                }
            }
        }

        var hasData = latest is not null;
        var end = xAxis?.Maximum is { } maximum ? ChartTime.ToUtcTicks(maximum, zone) : latest ?? DateTime.UtcNow.Ticks;
        var start = xAxis?.WindowSize is { } window
            ? end - window.Ticks
            : xAxis?.Minimum is { } minimum ? ChartTime.ToUtcTicks(minimum, zone) : earliest ?? (end - TimeSpan.TicksPerMinute);
        if (end <= start)
        {
            start -= TimeSpan.TicksPerSecond;
            end += TimeSpan.TicksPerSecond;
        }

        var barSpacing = visible.Where(static x => x.Series.Kind == ChartSeriesKind.Bar).Select(MeasureSpacing).DefaultIfEmpty(0).Max();
        if (barSpacing > 0)
        {
            var half = (long)(barSpacing * TimeSpan.TicksPerSecond / 2);
            start -= xAxis?.WindowSize is null ? half : 0;
            end += half;
        }

        var plotTop = PlotPadding;
        var xLabelsVisible = (xAxis?.IsVisible ?? true) && (xAxis?.LabelsVisible ?? true);
        var plotBottom = Height - (xLabelsVisible ? XLabelHeight : PlotPadding);
        var plotHeight = Math.Max(plotBottom - plotTop, 1);
        var scale = ResolveValueScale(visible, start, end, plotHeight);

        var yVisible = (yAxis?.IsVisible ?? true) && (yAxis?.LabelsVisible ?? true);
        TickGenerator.Numeric(scale, ticks);
        var valueLabels = ticks.Select(FormatAxisValue).ToList();
        var labelWidth = yVisible && (valueLabels.Count > 0) ? (valueLabels.Max(static x => x.Length) * LabelCharWidth) + LabelGap : 0;
        var plotLeft = PlotPadding + labelWidth;
        var live = visible.Any(static x => x.Series.CreateOptions().Live);
        var plotRight = Math.Max(width - PlotPadding - (live ? LiveRadius : 0), plotLeft + 1);
        var plotWidth = plotRight - plotLeft;

        var scaleX = plotWidth / ((end - start) / (double)TimeSpan.TicksPerSecond);
        var offsetX = plotLeft - (((start - baseTicks) / (double)TimeSpan.TicksPerSecond) * scaleX);
        var scaleY = plotHeight / (scale.Maximum - scale.Minimum);
        var offsetY = plotBottom + (scale.Minimum * scaleY);
        transform = FormattableString.Invariant($"matrix({scaleX:0.######} 0 0 {-scaleY:0.######} {offsetX:0.###} {offsetY:0.###})");

        viewBox = FormattableString.Invariant($"0 0 {ChartFormat.Pixel(width)} {ChartFormat.Pixel(Height)}");
        baseText = (baseTicks / TimeSpan.TicksPerSecond).ToString(CultureInfo.InvariantCulture);
        plotLeftText = ChartFormat.Pixel(plotLeft);
        plotRightText = ChartFormat.Pixel(plotRight);
        plotTopText = ChartFormat.Pixel(plotTop);
        plotBottomText = ChartFormat.Pixel(plotBottom);
        plotWidthText = ChartFormat.Pixel(plotWidth);
        clipX = ChartFormat.Pixel(plotLeft);
        clipY = ChartFormat.Pixel(plotTop - 2);
        clipWidth = ChartFormat.Pixel(plotWidth + (live ? LiveRadius : 0));
        clipHeight = ChartFormat.Pixel(plotHeight + 4);
        yLabelX = ChartFormat.Pixel(plotLeft - LabelGap);
        xLabelY = ChartFormat.Pixel(Height - 6);
        emptyText = hasData ? string.Empty : EmptyText ?? string.Empty;

        var yGrid = yAxis?.GridLinesVisible ?? true;
        for (var i = 0; i < ticks.Count; i++)
        {
            var y = ChartFormat.Pixel(offsetY - (ticks[i] * scaleY));
            if (yGrid)
            {
                yGridLines.Add(y);
            }

            if (yVisible)
            {
                yLabels.Add(new AxisLabel(y, valueLabels[i]));
            }
        }

        ArrangeTime(start, end, plotLeft, plotWidth, xLabelsVisible);

        ArrangeThresholds(scale, offsetY, scaleY);

        foreach (var state in visible)
        {
            var options = state.Series.CreateOptions();
            var color = colors[state.Series];
            var halfWidth = 0d;
            var widthText = "0";
            if (state.Series.Kind == ChartSeriesKind.Bar)
            {
                var spacing = MeasureSpacing(state);
                var barWidth = Math.Min(spacing * Math.Clamp(options.Opacity, 0.05, 1), options.Thickness / scaleX);
                halfWidth = barWidth / 2;
                widthText = ChartFormat.Coordinate(barWidth);
            }

            var format = options.ValueFormat ?? yAxis?.LabelFormat ?? string.Empty;
            seriesViews.Add(new SeriesView(
                state,
                state.Series.Kind,
                color,
                ChartFormat.Pixel(options.Thickness),
                options.Opacity.ToString("0.###", CultureInfo.InvariantCulture),
                halfWidth,
                widthText,
                format,
                state.Series.StackKey ?? string.Empty));

            if (options.Live && (state.Series.Kind != ChartSeriesKind.Bar))
            {
                var last = state.Points.FindLast(static x => Double.IsFinite(x.Value));
                if (last.Sequence > 0)
                {
                    livePoints.Add(new LivePoint(ChartFormat.Pixel(offsetX + (last.X * scaleX)), ChartFormat.Pixel(offsetY - (last.Top * scaleY)), color));
                }
            }
        }

        cultureName = culture.Name;
        offsetText = ((long)zone.GetUtcOffset(new DateTime(end, DateTimeKind.Utc)).TotalMinutes).ToString(CultureInfo.InvariantCulture);
        var spansDays = (end - start) >= TimeSpan.TicksPerDay;
        var finest = visible.Select(MeasureSpacing).Where(static x => x > 0).DefaultIfEmpty(60).Min();
        timeFormat = TimeInterval.GetTrackingFormat(finest < 60 ? TimeLabelPrecision.Second : finest < 86400 ? TimeLabelPrecision.Minute : TimeLabelPrecision.Day, spansDays);

        ArrangeLegend(colors, start, end);
    }

    private AxisScale ResolveValueScale(List<SeriesState> visible, long start, long end, double plotHeight)
    {
        var minimum = Double.PositiveInfinity;
        var maximum = Double.NegativeInfinity;
        foreach (var state in visible)
        {
            foreach (var point in state.Points)
            {
                if (Double.IsFinite(point.Value) && (point.Ticks >= start) && (point.Ticks <= end))
                {
                    var bottom = state.Series.Kind == ChartSeriesKind.Line ? point.Top : point.Bottom;
                    minimum = Math.Min(minimum, Math.Min(point.Top, bottom));
                    maximum = Math.Max(maximum, Math.Max(point.Top, bottom));
                }
            }
        }

        var includeZero = yAxis?.IncludeZero ?? visible.Any(static x => x.Series.Kind != ChartSeriesKind.Line);
        if (!Double.IsFinite(minimum))
        {
            minimum = 0;
            maximum = 1;
        }

        if (includeZero)
        {
            minimum = Math.Min(minimum, 0);
            maximum = Math.Max(maximum, 0);
        }

        if (yAxis?.SoftMinimum is { } softMinimum)
        {
            minimum = Math.Min(minimum, softMinimum);
        }

        if (yAxis?.SoftMaximum is { } softMaximum)
        {
            maximum = Math.Max(maximum, softMaximum);
        }

        if (maximum <= minimum)
        {
            var delta = Math.Abs(maximum) > 0 ? Math.Abs(maximum) * 0.1 : 1;
            minimum -= delta;
            maximum += delta;
        }

        var fixedMinimum = yAxis?.Minimum;
        var fixedMaximum = yAxis?.Maximum;
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

        var nice = yAxis?.NiceRange ?? true;
        var divisions = TickGenerator.GetMaxDivisions(plotHeight, TickGenerator.VerticalLabelSpacing, yAxis?.MaxLabelCount);
        return NiceScale.Create(minimum, maximum, divisions, nice && (fixedMinimum is null), nice && (fixedMaximum is null));
    }

    private void ArrangeTime(long start, long end, double plotLeft, double plotWidth, bool labelsVisible)
    {
        var localStart = ChartTime.ToZoneTicks(start, zone);
        var localEnd = ChartTime.ToZoneTicks(end, zone);
        var span = (double)(localEnd - localStart);
        var divisions = TickGenerator.GetMaxDivisions(plotWidth, TickGenerator.HorizontalLabelSpacing, xAxis?.MaxLabelCount);
        var widths = new TimeLabelWidths(8 * LabelCharWidth, 5 * LabelCharWidth, 5 * LabelCharWidth, 7 * LabelCharWidth);
        var interval = TickGenerator.SelectTimeInterval(span, plotWidth, divisions, LabelGap * 2, widths);
        TickGenerator.Time(localStart, localEnd, interval, culture.DateTimeFormat.FirstDayOfWeek, ticks);

        var grid = xAxis?.GridLinesVisible ?? false;
        foreach (var tick in ticks)
        {
            var x = ChartFormat.Pixel(plotLeft + ((tick - localStart) / span * plotWidth));
            if (grid)
            {
                xGridLines.Add(x);
            }

            if (labelsVisible)
            {
                var text = ChartFormat.Time(new DateTime((long)tick, DateTimeKind.Unspecified), xAxis?.LabelFormat, xAxis?.LabelFormatter, interval.GetTickFormat(tick), culture);
                xLabels.Add(new AxisLabel(x, text));
            }
        }
    }

    private void ArrangeThresholds(AxisScale scale, double offsetY, double scaleY)
    {
        if ((Thresholds is null) || (ThresholdsMode == ChartThresholdsMode.None))
        {
            return;
        }

        var sorted = Thresholds.OrderBy(static x => x.Value).ToList();
        for (var i = 0; i < sorted.Count; i++)
        {
            var threshold = sorted[i];
            var className = threshold.Color is null ? ChartFormat.LevelClass(threshold.Level) : "mussel-custom";
            if ((ThresholdsMode & ChartThresholdsMode.Bands) != 0)
            {
                var low = Math.Max(threshold.Value, scale.Minimum);
                var high = Math.Min(i + 1 < sorted.Count ? sorted[i + 1].Value : scale.Maximum, scale.Maximum);
                if (high > low)
                {
                    var top = offsetY - (high * scaleY);
                    thresholdBands.Add(new ThresholdBand(ChartFormat.Pixel(top), ChartFormat.Pixel((high - low) * scaleY), "mussel-band " + className, threshold.Color));
                }
            }

            if (((ThresholdsMode & ChartThresholdsMode.Lines) != 0) && Double.IsFinite(threshold.Value) && (threshold.Value >= scale.Minimum) && (threshold.Value <= scale.Maximum))
            {
                thresholdLines.Add(new ThresholdLine(ChartFormat.Pixel(offsetY - (threshold.Value * scaleY)), className, threshold.Color, threshold.Label));
            }
        }
    }

    private void ArrangeLegend(Dictionary<ChartSeries, string> colors, long start, long end)
    {
        var listed = seriesList.Where(static x => x.LegendVisible && !String.IsNullOrEmpty(x.Title)).ToList();
        legendVisible = LegendVisible ?? (listed.Count >= 2);
        if (!legendVisible)
        {
            return;
        }

        var columns = new List<ChartLegendValues>();
        if (LegendMode == ChartLegendMode.Table)
        {
            foreach (var value in (ReadOnlySpan<ChartLegendValues>)[ChartLegendValues.Last, ChartLegendValues.Min, ChartLegendValues.Max, ChartLegendValues.Mean])
            {
                if ((LegendValues & value) != 0)
                {
                    columns.Add(value);
                    legendColumns.Add(value == ChartLegendValues.Last ? "Last *" : value.ToString());
                }
            }
        }

        foreach (var series in listed)
        {
            var state = states[series];
            var options = series.CreateOptions();
            var values = new List<string>(columns.Count);
            if (columns.Count > 0)
            {
                var window = state.Points.Where(x => Double.IsFinite(x.Value) && (x.Ticks >= start) && (x.Ticks <= end)).Select(static x => x.Value).ToList();
                foreach (var column in columns)
                {
                    if (window.Count == 0)
                    {
                        values.Add(string.Empty);
                        continue;
                    }

                    var value = column switch
                    {
                        ChartLegendValues.Last => window[^1],
                        ChartLegendValues.Min => window.Min(),
                        ChartLegendValues.Max => window.Max(),
                        _ => window.Average()
                    };
                    values.Add(ChartFormat.Value(value, options.ValueFormat ?? yAxis?.LabelFormat, options.ValueFormatter ?? yAxis?.LabelFormatter, culture));
                }
            }

            legendRows.Add(new LegendRow(series, series.Title!, colors[series], !state.Visible, values));
        }
    }

    private string FormatAxisValue(double value) =>
        ChartFormat.Value(value, yAxis?.LabelFormat, yAxis?.LabelFormatter, culture);

    private static double MeasureSpacing(SeriesState state)
    {
        var points = state.Points;
        var count = (int)Math.Min(points.Count - 1, SpacingSamples);
        if (count <= 0)
        {
            return 0;
        }

        var spacings = new double[count];
        for (var i = 0; i < count; i++)
        {
            spacings[i] = points[points.Count - 1 - i].X - points[points.Count - 2 - i].X;
        }

        Array.Sort(spacings);
        return spacings[count / 2];
    }
}

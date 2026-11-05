namespace MusselChart;

using System.Globalization;

using MusselChart.Internal;

public sealed partial class MusselChartView
{
    private const double LabelCharWidth = 6.6;

    private const double LabelGap = 6;

    private const double LabelFontSize = 11;

    private const double PlotPadding = 8;

    private const double LiveRadius = 10;

    private const double XLabelHeight = 20;

    private const double HorizontalEndMargin = 12;

    private const int SpacingSamples = 64;

    private const int MaxStackAllocatedSlots = 32;

    private static readonly List<string> NoLegendValues = [];

    private const double SegmentGap = 2;

    private const double MarkerOutline = 1;

    private const double MarkerClipMargin = 2;

    private const double CategoryLabelSpacing = 8;

    private const double MaxLabelWidthRatio = 1d / 3;

    private const double HorizontalThresholdLabelX = 4;

    private const double HorizontalThresholdLabelY = 16;

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

    private readonly List<SeriesState> visibleStates = [];

    private readonly List<double> valueTicks = [];

    private readonly List<string> valueTexts = [];

    private readonly List<string> categoryTexts = [];

    private readonly List<ChartThreshold> sortedThresholds = [];

    private readonly List<ChartLegendValues> legendValueColumns = [];

    private readonly Dictionary<string, int> barGroups = new(StringComparer.Ordinal);

    private readonly List<(double Fraction, string Text)> positionTicks = [];

    private readonly List<LabelExtent> labelExtents = [];

    private readonly List<int> selectedLabels = [];

    private string transform = string.Empty;

    private string baseText = "0";

    private string clipX = string.Empty;

    private string clipY = string.Empty;

    private string clipWidth = string.Empty;

    private string clipHeight = string.Empty;

    private string plotLeftText = string.Empty;

    private string plotRightText = string.Empty;

    private string plotTopText = string.Empty;

    private string plotBottomText = string.Empty;

    private string yLabelX = string.Empty;

    private string xLabelY = string.Empty;

    private string cultureName = string.Empty;

    private string offsetText = string.Empty;

    private string timeFormat = string.Empty;

    private string emptyText = string.Empty;

    private string xKindText = string.Empty;

    private string? xFormatText;

    private string? categoriesText;

    private bool legendVisible;

    private double bubbleExtent;

    private double largestBubble;

    private double smallestBubble;

    private static bool IsBarKind(ChartSeriesKind kind) => kind is ChartSeriesKind.Bar or ChartSeriesKind.RangeBar;

    private static bool IsMarkerKind(ChartSeriesKind kind) => kind is ChartSeriesKind.Scatter or ChartSeriesKind.Bubble;

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
        var visible = visibleStates;
        visible.Clear();
        var hasData = false;
        var live = false;
        var markerMargin = 0d;
        for (var i = 0; i < seriesList.Count; i++)
        {
            var series = seriesList[i];
            var state = states[series];
            state.Color = series.Color ?? (palette.Count > 0 ? palette[i % palette.Count] : "#808080");
            if (!state.Visible)
            {
                continue;
            }

            var options = series.Options;
            state.ApplyOptions(options);
            visible.Add(state);
            hasData = hasData || state.Points.Exists(static x => Double.IsFinite(x.Value));
            live = live || options.Live;
            if (series.Kind == ChartSeriesKind.Scatter)
            {
                markerMargin = Math.Max(markerMargin, (options.Thickness / 2) + MarkerOutline);
            }
        }

        var horizontal = Orientation == ChartOrientation.Horizontal;
        var positionLabelsVisible = (xAxis?.IsVisible ?? true) && (xAxis?.LabelsVisible ?? true);
        var valueLabelsVisible = (yAxis?.IsVisible ?? true) && (yAxis?.LabelsVisible ?? true);

        SizeBubbles(visible);
        var window = ResolveWindow(visible, hasData);
        var percent = IsPercentAxis(visible);

        // The plot: labels of the value axis on the left and of the position axis at the bottom, or the other way around
        var plotTop = PlotPadding;
        double plotBottom;
        double plotLeft;
        double plotRight;
        AxisScale scale;
        PositionRange position;
        if (horizontal)
        {
            plotBottom = Height - (valueLabelsVisible ? XLabelHeight : PlotPadding);
            var length = Math.Max(plotBottom - plotTop, 1);
            position = ResolvePosition(visible, window, length);
            CollectPositionTicks(position, length, true);
            var labelWidth = positionLabelsVisible ? MeasurePositionLabels() : 0;
            plotLeft = PlotPadding + labelWidth;
            plotRight = Math.Max(Width - PlotPadding - (live ? LiveRadius : HorizontalEndMargin), plotLeft + 1);
            scale = ResolveValueScale(visible, window, plotRight - plotLeft, TickGenerator.HorizontalLabelSpacing);
            FormatValueTicks(scale, percent, valueLabelsVisible);
        }
        else
        {
            plotBottom = Height - (positionLabelsVisible ? XLabelHeight : PlotPadding);
            scale = ResolveValueScale(visible, window, Math.Max(plotBottom - plotTop, 1), TickGenerator.VerticalLabelSpacing);
            FormatValueTicks(scale, percent, valueLabelsVisible);
            var longest = 0;
            foreach (var text in valueTexts)
            {
                longest = Math.Max(longest, text.Length);
            }

            var labelWidth = longest > 0 ? (longest * LabelCharWidth) + LabelGap : 0;
            plotLeft = PlotPadding + labelWidth;
            plotRight = Math.Max(Width - PlotPadding - (live ? LiveRadius : 0), plotLeft + 1);
            position = ResolvePosition(visible, window, plotRight - plotLeft);
            CollectPositionTicks(position, plotRight - plotLeft, false);
        }

        var plotWidth = plotRight - plotLeft;
        var plotHeight = Math.Max(plotBottom - plotTop, 1);
        var positionLength = horizontal ? plotHeight : plotWidth;
        var valueLength = horizontal ? plotWidth : plotHeight;
        var scaleP = positionLength / (position.End - position.Start);
        var scaleV = valueLength / (scale.Maximum - scale.Minimum);
        var map = horizontal
            ? new PlotMap(true, scaleP, plotTop - (position.Start * scaleP), scaleV, plotLeft - (scale.Minimum * scaleV))
            : new PlotMap(false, scaleP, plotLeft - (position.Start * scaleP), scaleV, plotBottom + (scale.Minimum * scaleV));
        transform = map.Matrix;

        var clipMargin = Math.Max(markerMargin, MarkerClipMargin);
        plotLeftText = ChartFormat.Pixel(plotLeft);
        plotRightText = ChartFormat.Pixel(plotRight);
        plotTopText = ChartFormat.Pixel(plotTop);
        plotBottomText = ChartFormat.Pixel(plotBottom);
        clipX = ChartFormat.Pixel(plotLeft - markerMargin);
        clipY = ChartFormat.Pixel(plotTop - clipMargin);
        clipWidth = ChartFormat.Pixel(plotWidth + markerMargin + (live ? LiveRadius : markerMargin));
        clipHeight = ChartFormat.Pixel(plotHeight + (clipMargin * 2));
        yLabelX = ChartFormat.Pixel(plotLeft - LabelGap);
        xLabelY = ChartFormat.Pixel(Height - 6);
        emptyText = hasData ? string.Empty : EmptyText ?? string.Empty;

        ArrangeValueAxis(map, valueLabelsVisible);
        ArrangePositionAxis(horizontal, plotLeft, plotTop, plotWidth, plotHeight, positionLabelsVisible);
        ArrangeThresholds(scale, map, plotLeft, plotRight, plotTop, plotBottom);

        ArrangeBars(visible, position, scaleP);
        foreach (var state in visible)
        {
            var series = state.Series;
            var options = series.Options;
            var kind = series.Kind;
            var stacked = IsBarKind(kind) && (series.StackKey is not null);
            seriesViews.Add(new SeriesView(
                state,
                kind,
                options.ValueFormat ?? yAxis?.LabelFormat ?? string.Empty,
                stacked ? SegmentGap / scaleV : 0,
                stacked ? stacks.GetValueOrDefault((kind, series.StackKey!)) : null,
                IsMarkerKind(kind) ? CreateMarkers(state, options, map, position) : null));

            if (options.Live && !IsBarKind(kind) && !IsMarkerKind(kind))
            {
                var last = state.Points.FindLast(static x => Double.IsFinite(x.Value));
                if (last.Sequence > 0)
                {
                    var (x, y) = map.Map(last.X, last.Top);
                    livePoints.Add(new LivePoint(ChartFormat.Pixel(x), ChartFormat.Pixel(y), state.Color));
                }
            }
        }

        ArrangeTracking(visible, position);
        ArrangeLegend(position, percent);
    }

    private void SizeBubbles(List<SeriesState> visible)
    {
        smallestBubble = Double.PositiveInfinity;
        largestBubble = Double.NegativeInfinity;
        bubbleExtent = 0;
        foreach (var state in visible)
        {
            if (state.Series.Kind != ChartSeriesKind.Bubble)
            {
                continue;
            }

            foreach (var marker in state.Markers)
            {
                smallestBubble = Math.Min(smallestBubble, marker.Size);
                largestBubble = Math.Max(largestBubble, marker.Size);
            }
        }

        foreach (var state in visible)
        {
            if (state.Series.Kind != ChartSeriesKind.Bubble)
            {
                continue;
            }

            var options = state.Series.Options;
            foreach (var marker in state.Markers)
            {
                bubbleExtent = Math.Max(bubbleExtent, (GetBubbleSize(marker.Size, options) / 2) + MarkerOutline);
            }
        }
    }

    // The area grows from 0, or from the smallest value, to the largest value among all of the bubble series
    private double GetBubbleSize(double value, SeriesOptions options)
    {
        var minimum = options.MinSize;
        var maximum = Math.Max(options.MaxSize, minimum);
        double ratio;
        if (options.SizeIncludesZero)
        {
            ratio = (largestBubble > 0) && (value > 0) ? Math.Min(value / largestBubble, 1) : 0;
        }
        else
        {
            var range = largestBubble - smallestBubble;
            ratio = range > 0 ? Math.Clamp((value - smallestBubble) / range, 0, 1) : 0;
        }

        return Math.Sqrt((minimum * minimum) + (((maximum * maximum) - (minimum * minimum)) * ratio));
    }

    // The part of the position axis fixed by the settings, which limits the values that decide the value axis
    private PositionWindow ResolveWindow(List<SeriesState> visible, bool hasData)
    {
        if (xKind == ChartXKind.Number)
        {
            var axis = xAxis as NumericAxis;
            return new PositionWindow(axis?.Minimum ?? Double.NegativeInfinity, axis?.Maximum ?? Double.PositiveInfinity);
        }

        if (xKind != ChartXKind.Time)
        {
            return new PositionWindow(Double.NegativeInfinity, Double.PositiveInfinity);
        }

        long? earliest = null;
        long? latest = null;
        if (hasData)
        {
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
        }

        var axisTime = xAxis as DateTimeAxis;
        var end = axisTime?.Maximum is { } maximum ? ChartTime.ToUtcTicks(maximum, zone) : latest ?? DateTime.UtcNow.Ticks;
        var start = axisTime?.WindowSize is { } window
            ? end - window.Ticks
            : axisTime?.Minimum is { } minimum ? ChartTime.ToUtcTicks(minimum, zone) : earliest ?? (end - TimeSpan.TicksPerMinute);
        if (end <= start)
        {
            start -= TimeSpan.TicksPerSecond;
            end += TimeSpan.TicksPerSecond;
        }

        var barSpacing = MeasureBarSpacing(visible);
        if (barSpacing > 0)
        {
            var half = (long)(barSpacing * TimeSpan.TicksPerSecond / 2);
            start -= axisTime?.WindowSize is null ? half : 0;
            end += half;
        }

        return new PositionWindow(ToSeconds(start), ToSeconds(end));
    }

    private static double MeasureBarSpacing(List<SeriesState> visible)
    {
        var spacing = 0d;
        foreach (var state in visible)
        {
            if (IsBarKind(state.Series.Kind))
            {
                spacing = Math.Max(spacing, MeasureSpacing(state));
            }
        }

        return spacing;
    }

    private double ToSeconds(long value) => (value - baseTicks) / (double)TimeSpan.TicksPerSecond;

    private long ToTicks(double seconds) => baseTicks + (long)Math.Round(seconds * TimeSpan.TicksPerSecond);

    private PositionRange ResolvePosition(List<SeriesState> visible, in PositionWindow window, double length)
    {
        switch (xKind)
        {
            case ChartXKind.Category:
                var count = Math.Max(categories.Count, 1);
                return xAxis is CategoryAxis { LabelPlacement: ChartLabelPlacement.OnTicks } && (count > 1)
                    ? new PositionRange(0, count - 1, default)
                    : new PositionRange(-0.5, count - 0.5, default);
            case ChartXKind.Number:
                return ResolveNumberPosition(visible, length);
            default:
                var (start, end) = ChartRange.Pad(window.Start, window.End, bubbleExtent, length);
                return new PositionRange(start, end, default);
        }
    }

    private PositionRange ResolveNumberPosition(List<SeriesState> visible, double length)
    {
        var axis = xAxis as NumericAxis;
        var minimum = Double.PositiveInfinity;
        var maximum = Double.NegativeInfinity;
        foreach (var state in visible)
        {
            foreach (var point in state.Points)
            {
                if (Double.IsFinite(point.Value))
                {
                    minimum = Math.Min(minimum, point.X);
                    maximum = Math.Max(maximum, point.X);
                }
            }
        }

        var gap = MeasureBarSpacing(visible);
        if (gap > 0)
        {
            minimum -= gap / 2;
            maximum += gap / 2;
        }

        (minimum, maximum) = ChartRange.Pad(minimum, maximum, bubbleExtent, length);
        var options = new ChartRangeOptions(axis?.Minimum, axis?.Maximum, axis?.SoftMinimum, axis?.SoftMaximum, axis?.IncludeZero ?? false, axis?.NiceRange ?? false);
        var divisions = TickGenerator.GetMaxDivisions(length, Orientation == ChartOrientation.Horizontal ? TickGenerator.VerticalLabelSpacing : TickGenerator.HorizontalLabelSpacing, xAxis?.MaxLabelCount);
        var scale = ChartRange.Resolve(minimum, maximum, options, divisions);
        return new PositionRange(scale.Minimum, scale.Maximum, scale);
    }

    private AxisScale ResolveValueScale(List<SeriesState> visible, in PositionWindow window, double length, double spacing)
    {
        var minimum = Double.PositiveInfinity;
        var maximum = Double.NegativeInfinity;
        foreach (var state in visible)
        {
            var kind = state.Series.Kind;
            foreach (var point in state.Points)
            {
                if (Double.IsFinite(point.Value) && (point.X >= window.Start) && (point.X <= window.End))
                {
                    var bottom = kind is ChartSeriesKind.Line or ChartSeriesKind.Scatter or ChartSeriesKind.Bubble ? point.Top : point.Bottom;
                    minimum = Math.Min(minimum, Math.Min(point.Top, bottom));
                    maximum = Math.Max(maximum, Math.Max(point.Top, bottom));
                }
            }
        }

        (minimum, maximum) = ChartRange.Pad(minimum, maximum, bubbleExtent, length);
        var includeZero = yAxis?.IncludeZero ?? visible.Exists(static x => x.Series.Kind is ChartSeriesKind.Area or ChartSeriesKind.Bar);
        var options = new ChartRangeOptions(yAxis?.Minimum, yAxis?.Maximum, yAxis?.SoftMinimum, yAxis?.SoftMaximum, includeZero, yAxis?.NiceRange ?? true);
        var divisions = TickGenerator.GetMaxDivisions(length, spacing, yAxis?.MaxLabelCount);
        return ChartRange.Resolve(minimum, maximum, options, divisions);
    }

    private bool IsPercentAxis(List<SeriesState> visible) =>
        (stackMode == ChartStackMode.Percent) && (visible.Count > 0) && visible.TrueForAll(static x => x.Series.StackKey is not null);

    private void CollectPositionTicks(in PositionRange position, double length, bool vertical)
    {
        positionTicks.Clear();
        switch (xKind)
        {
            case ChartXKind.Category:
                CollectCategoryTicks(position, length, vertical);
                break;
            case ChartXKind.Number:
                CollectNumberTicks(position);
                break;
            default:
                CollectTimeTicks(position, length, vertical);
                break;
        }
    }

    private void CollectCategoryTicks(in PositionRange position, double length, bool vertical)
    {
        var axis = xAxis as CategoryAxis;
        var span = position.End - position.Start;
        labelExtents.Clear();
        categoryTexts.Clear();
        for (var i = 0; i < categories.Count; i++)
        {
            var text = FormatCategory(categories[i], axis);
            categoryTexts.Add(text);
            var center = (i - position.Start) / span * length;
            var half = (vertical ? ChartText.LineHeight(LabelFontSize) : ChartText.Measure(text, LabelFontSize)) / 2;
            labelExtents.Add(new LabelExtent(center - half, center + half));
        }

        LabelThinning.Select(labelExtents, CategoryLabelSpacing, axis?.MaxLabelCount ?? Int32.MaxValue, selectedLabels);
        foreach (var index in selectedLabels)
        {
            positionTicks.Add(((index - position.Start) / span, categoryTexts[index]));
        }
    }

    private string FormatCategory(string category, CategoryAxis? axis)
    {
        if (axis?.LabelFormatter is { } formatter)
        {
            return formatter(category);
        }

        var text = category;
        if ((axis?.LabelMaxLength is { } limit) && (limit > 0))
        {
            var info = new StringInfo(text);
            if (info.LengthInTextElements > limit)
            {
                text = info.SubstringByTextElements(0, limit).TrimEnd() + ChartText.Ellipsis;
            }
        }

        return (axis?.LabelFormat is { } format) && format.Contains('{', StringComparison.Ordinal) ? String.Format(culture, format, text) : text;
    }

    private void CollectNumberTicks(in PositionRange position)
    {
        var axis = xAxis as NumericAxis;
        TickGenerator.Numeric(position.Scale, ticks);
        foreach (var tick in ticks)
        {
            var text = ChartFormat.Value(tick, axis?.LabelFormat, axis?.LabelFormatter, culture);
            positionTicks.Add(((tick - position.Start) / (position.End - position.Start), text));
        }
    }

    private void CollectTimeTicks(in PositionRange position, double length, bool vertical)
    {
        var axis = xAxis as DateTimeAxis;
        var localStart = ChartTime.ToZoneTicks(ToTicks(position.Start), zone);
        var localEnd = ChartTime.ToZoneTicks(ToTicks(position.End), zone);
        var span = (double)(localEnd - localStart);
        var spacing = vertical ? TickGenerator.VerticalLabelSpacing : TickGenerator.HorizontalLabelSpacing;
        var divisions = TickGenerator.GetMaxDivisions(length, spacing, axis?.MaxLabelCount);
        var height = ChartText.LineHeight(LabelFontSize);
        var widths = vertical ? new TimeLabelWidths(height, height, height, height) : new TimeLabelWidths(8 * LabelCharWidth, 5 * LabelCharWidth, 5 * LabelCharWidth, 7 * LabelCharWidth);
        var interval = TickGenerator.SelectTimeInterval(span, length, divisions, LabelGap * 2, widths);
        TickGenerator.Time(localStart, localEnd, interval, culture.DateTimeFormat.FirstDayOfWeek, ticks);
        foreach (var tick in ticks)
        {
            var text = ChartFormat.Time(new DateTime((long)tick, DateTimeKind.Unspecified), axis?.LabelFormat, axis?.LabelFormatter, interval.GetTickFormat(tick), culture);
            positionTicks.Add(((tick - localStart) / span, text));
        }
    }

    private double MeasurePositionLabels()
    {
        var limit = Width * MaxLabelWidthRatio;
        var widest = 0d;
        for (var i = 0; i < positionTicks.Count; i++)
        {
            var (fraction, text) = positionTicks[i];
            widest = Math.Max(widest, ChartText.Measure(text, LabelFontSize));
            positionTicks[i] = (fraction, ChartText.Truncate(text, LabelFontSize, limit));
        }

        return widest > 0 ? Math.Min(widest, limit) + LabelGap : 0;
    }

    private void FormatValueTicks(in AxisScale scale, bool percent, bool labelsVisible)
    {
        TickGenerator.Numeric(scale, valueTicks);
        valueTexts.Clear();
        if (labelsVisible)
        {
            foreach (var tick in valueTicks)
            {
                valueTexts.Add(FormatAxisValue(tick, percent));
            }
        }
    }

    private void ArrangeValueAxis(in PlotMap map, bool labelsVisible)
    {
        var grid = yAxis?.GridLinesVisible ?? true;
        for (var i = 0; i < valueTicks.Count; i++)
        {
            var (x, y) = map.Map(0, valueTicks[i]);
            var position = ChartFormat.Pixel(map.Horizontal ? x : y);
            if (grid)
            {
                (map.Horizontal ? xGridLines : yGridLines).Add(position);
            }

            if (labelsVisible)
            {
                (map.Horizontal ? xLabels : yLabels).Add(new AxisLabel(position, valueTexts[i]));
            }
        }
    }

    private void ArrangePositionAxis(bool horizontal, double plotLeft, double plotTop, double plotWidth, double plotHeight, bool labelsVisible)
    {
        var grid = xAxis?.GridLinesVisible ?? false;
        foreach (var (fraction, text) in positionTicks)
        {
            var position = ChartFormat.Pixel(horizontal ? plotTop + (fraction * plotHeight) : plotLeft + (fraction * plotWidth));
            if (grid)
            {
                (horizontal ? yGridLines : xGridLines).Add(position);
            }

            if (labelsVisible)
            {
                (horizontal ? yLabels : xLabels).Add(new AxisLabel(position, text));
            }
        }
    }

    private void ArrangeThresholds(in AxisScale scale, in PlotMap map, double plotLeft, double plotRight, double plotTop, double plotBottom)
    {
        if ((Thresholds is null) || (ThresholdsMode == ChartThresholdsMode.None))
        {
            return;
        }

        var left = ChartFormat.Pixel(plotLeft);
        var right = ChartFormat.Pixel(plotRight);
        var top = ChartFormat.Pixel(plotTop);
        var bottom = ChartFormat.Pixel(plotBottom);
        var sorted = sortedThresholds;
        sorted.Clear();
        // ReSharper disable once ForCanBeConvertedToForeach
        for (var i = 0; i < Thresholds.Count; i++)
        {
            var threshold = Thresholds[i];
            var index = sorted.Count;
            while ((index > 0) && (sorted[index - 1].Value > threshold.Value))
            {
                index--;
            }

            sorted.Insert(index, threshold);
        }

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
                    var size = ChartFormat.Pixel((high - low) * map.ScaleV);
                    var bandClass = "mussel-band " + className;
                    thresholdBands.Add(map.Horizontal
                        ? new ThresholdBand(ChartFormat.Pixel(map.Map(0, low).X), top, size, ChartFormat.Pixel(plotBottom - plotTop), bandClass, threshold.Color)
                        : new ThresholdBand(left, ChartFormat.Pixel(map.Map(0, high).Y), ChartFormat.Pixel(plotRight - plotLeft), size, bandClass, threshold.Color));
                }
            }

            if (((ThresholdsMode & ChartThresholdsMode.Lines) != 0) && Double.IsFinite(threshold.Value) && (threshold.Value >= scale.Minimum) && (threshold.Value <= scale.Maximum))
            {
                var (x, y) = map.Map(0, threshold.Value);
                thresholdLines.Add(map.Horizontal
                    ? new ThresholdLine(ChartFormat.Pixel(x), top, ChartFormat.Pixel(x), bottom, ChartFormat.Pixel(x - HorizontalThresholdLabelX), ChartFormat.Pixel(plotTop + HorizontalThresholdLabelY), className, threshold.Color, threshold.Label)
                    : new ThresholdLine(left, ChartFormat.Pixel(y), right, ChartFormat.Pixel(y), right, ChartFormat.Pixel(y), className, threshold.Color, threshold.Label));
            }
        }
    }

    // Each stack and each bar takes an equal slot of the group, and bars narrowed by the limit are centered in the column
    private void ArrangeBars(List<SeriesState> visible, in PositionRange position, double scaleP)
    {
        barGroups.Clear();
        var count = 0;
        var spacing = 0d;
        var ratio = 0d;
        foreach (var state in visible)
        {
            if (!IsBarKind(state.Series.Kind))
            {
                continue;
            }

            if (count == 0)
            {
                ratio = state.Series.Options.Opacity;
            }

            if (state.Series.StackKey is not { } key)
            {
                state.BarSlot = count++;
            }
            else
            {
                if (!barGroups.TryGetValue(key, out var index))
                {
                    index = count++;
                    barGroups[key] = index;
                }

                state.BarSlot = index;
            }

            if (xKind != ChartXKind.Category)
            {
                spacing = Math.Max(spacing, MeasureSpacing(state));
            }
        }

        if (count == 0)
        {
            return;
        }

        if (xKind == ChartXKind.Category)
        {
            spacing = 1;
        }
        else if (!(spacing > 0))
        {
            spacing = position.End - position.Start;
        }

        var group = spacing * scaleP * Math.Clamp(ratio, 0.05, 1);
        var gap = count > 1 ? Math.Min(SegmentGap, group / count / 3) : 0;
        var slot = Math.Max(0, (group - ((count - 1) * gap)) / count);
        var widths = count <= MaxStackAllocatedSlots ? stackalloc double[count] : new double[count];
        foreach (var state in visible)
        {
            if (IsBarKind(state.Series.Kind))
            {
                widths[state.BarSlot] = Math.Max(widths[state.BarSlot], Math.Min(slot, state.Series.Options.Thickness));
            }
        }

        var total = (count - 1) * gap;
        foreach (var width in widths)
        {
            total += width;
        }

        foreach (var state in visible)
        {
            if (!IsBarKind(state.Series.Kind))
            {
                continue;
            }

            var start = -total / 2;
            for (var k = 0; k < state.BarSlot; k++)
            {
                start += widths[k] + gap;
            }

            var width = widths[state.BarSlot];
            state.PlaceBars((start + (width / 2)) / scaleP, width / scaleP, xKind != ChartXKind.Time);
        }
    }

    private static int LargerBubbleFirst(SeriesMarker x, SeriesMarker y)
    {
        var size = y.Size.CompareTo(x.Size);
        return size != 0 ? size : x.Key.CompareTo(y.Key);
    }

    private List<MarkerView> CreateMarkers(SeriesState state, SeriesOptions options, in PlotMap map, in PositionRange position)
    {
        var bubble = state.Series.Kind == ChartSeriesKind.Bubble;
        var markers = state.MarkerViews;
        markers.Clear();
        if (bubble && !state.MarkersSorted)
        {
            state.Markers.Sort(LargerBubbleFirst);
            state.MarkersSorted = true;
        }

        var sharedRadius = bubble ? null : ChartGeometry.Text(options.Thickness / 2);
        foreach (var marker in state.Markers)
        {
            if ((marker.X < position.Start) || (marker.X > position.End))
            {
                continue;
            }

            var size = bubble ? GetBubbleSize(marker.Size, options) : options.Thickness;
            var radius = size / 2;
            var (x, y) = map.Map(marker.X, marker.Y);
            markers.Add(new MarkerView(
                marker.Key,
                marker.XText,
                marker.YText,
                marker.SizeText,
                ChartGeometry.Text(x),
                ChartGeometry.Text(y),
                sharedRadius ?? ChartGeometry.Text(radius),
                CreateMarkerPath(x, y, radius, options.Shape)));
        }

        return markers;
    }

    private static string? CreateMarkerPath(double x, double y, double radius, ChartMarkerShape shape)
    {
        if (shape == ChartMarkerShape.Circle)
        {
            return null;
        }

        var extent = shape == ChartMarkerShape.Diamond ? radius * Math.Sqrt(2) : radius;
        var builder = ChartStringBuilder.Rent();
        if (shape == ChartMarkerShape.Diamond)
        {
            builder.Append('M').AppendNumber(x).Append(' ').AppendNumber(y - extent);
            builder.Append('L').AppendNumber(x + extent).Append(' ').AppendNumber(y);
            builder.Append('L').AppendNumber(x).Append(' ').AppendNumber(y + extent);
            builder.Append('L').AppendNumber(x - extent).Append(' ').AppendNumber(y);
        }
        else
        {
            builder.Append('M').AppendNumber(x - extent).Append(' ').AppendNumber(y - extent);
            builder.Append('H').AppendNumber(x + extent);
            builder.Append('V').AppendNumber(y + extent);
            builder.Append('H').AppendNumber(x - extent);
        }

        builder.Append('Z');
        return ChartStringBuilder.ToStringAndReturn(builder);
    }

    private void ArrangeTracking(List<SeriesState> visible, in PositionRange position)
    {
        cultureName = culture.Name;
        xKindText = xKind switch
        {
            ChartXKind.Number => "number",
            ChartXKind.Category => "category",
            _ => "time"
        };
        xFormatText = xKind == ChartXKind.Number ? xAxis?.LabelFormat : null;
        categoriesText = xKind == ChartXKind.Category ? ChartTip.CreateStrings(categories) : null;
        offsetText = string.Empty;
        timeFormat = string.Empty;
        if (xKind != ChartXKind.Time)
        {
            return;
        }

        var start = ToTicks(position.Start);
        var end = ToTicks(position.End);
        offsetText = ((long)zone.GetUtcOffset(new DateTime(end, DateTimeKind.Utc)).TotalMinutes).ToString(CultureInfo.InvariantCulture);
        var spansDays = (end - start) >= TimeSpan.TicksPerDay;
        var finest = Double.PositiveInfinity;
        foreach (var state in visible)
        {
            var spacing = MeasureSpacing(state);
            if (spacing > 0)
            {
                finest = Math.Min(finest, spacing);
            }
        }

        finest = Double.IsInfinity(finest) ? 60 : finest;
        timeFormat = TimeInterval.GetTrackingFormat(finest < 60 ? TimeLabelPrecision.Second : finest < 86400 ? TimeLabelPrecision.Minute : TimeLabelPrecision.Day, spansDays);
    }

    private static bool IsListed(ChartSeries series) => series.LegendVisible && !String.IsNullOrEmpty(series.Title);

    private void ArrangeLegend(in PositionRange position, bool percent)
    {
        var listed = 0;
        foreach (var series in seriesList)
        {
            listed += IsListed(series) ? 1 : 0;
        }

        legendVisible = LegendVisible ?? (listed >= 2);
        if (!legendVisible)
        {
            return;
        }

        legendValueColumns.Clear();
        if (LegendMode == ChartLegendMode.Table)
        {
            foreach (var value in (ReadOnlySpan<ChartLegendValues>)[ChartLegendValues.Last, ChartLegendValues.Min, ChartLegendValues.Max, ChartLegendValues.Mean])
            {
                if ((LegendValues & value) != 0)
                {
                    legendValueColumns.Add(value);
                    legendColumns.Add(value == ChartLegendValues.Last ? "Last *" : value.ToString());
                }
            }
        }

        foreach (var series in seriesList)
        {
            if (IsListed(series))
            {
                var state = states[series];
                legendRows.Add(new LegendRow(series, series.Title!, state.Color, !state.Visible, series.Kind is not (ChartSeriesKind.Line or ChartSeriesKind.Area), CreateLegendValues(state, position, percent)));
            }
        }
    }

    private List<string> CreateLegendValues(SeriesState state, in PositionRange position, bool percent)
    {
        if (legendValueColumns.Count == 0)
        {
            return NoLegendValues;
        }

        var values = new List<string>(legendValueColumns.Count);
        var count = 0;
        var last = 0d;
        var minimum = Double.PositiveInfinity;
        var maximum = Double.NegativeInfinity;
        var sum = 0d;
        foreach (var point in state.Points)
        {
            if (Double.IsFinite(point.Value) && (point.X >= position.Start) && (point.X <= position.End))
            {
                count++;
                last = point.Value;
                minimum = Math.Min(minimum, point.Value);
                maximum = Math.Max(maximum, point.Value);
                sum += point.Value;
            }
        }

        var options = state.Series.Options;
        foreach (var column in legendValueColumns)
        {
            if (count == 0)
            {
                values.Add(string.Empty);
                continue;
            }

            var value = column switch
            {
                ChartLegendValues.Last => last,
                ChartLegendValues.Min => minimum,
                ChartLegendValues.Max => maximum,
                _ => sum / count
            };
            values.Add(ChartFormat.Value(value, options.ValueFormat ?? (percent ? null : yAxis?.LabelFormat), options.ValueFormatter ?? (percent ? null : yAxis?.LabelFormatter), culture));
        }

        return values;
    }

    private string FormatAxisValue(double value, bool percent)
    {
        if (percent && (yAxis?.LabelFormat is null) && (yAxis?.LabelFormatter is null))
        {
            return (value / 100).ToString("P0", culture);
        }

        return ChartFormat.Value(value, yAxis?.LabelFormat, yAxis?.LabelFormatter, culture);
    }

    private static double MeasureSpacing(SeriesState state)
    {
        var points = state.Points;
        var count = Math.Min(points.Count - 1, SpacingSamples);
        if (count <= 0)
        {
            return 0;
        }

        Span<double> spacings = stackalloc double[count];
        for (var i = 0; i < count; i++)
        {
            spacings[i] = points[points.Count - 1 - i].X - points[points.Count - 2 - i].X;
        }

        spacings.Sort();
        return spacings[count / 2];
    }

    private readonly record struct PositionWindow(double Start, double End);

    private readonly record struct PositionRange(double Start, double End, AxisScale Scale);

    // Data goes into the SVG as it is, and this transform moves it to the plot, so that new points cost only themselves
    private readonly record struct PlotMap(bool Horizontal, double ScaleP, double OffsetP, double ScaleV, double OffsetV)
    {
        public string Matrix => Horizontal
            ? String.Create(CultureInfo.InvariantCulture, $"matrix(0 {ScaleP:0.######} {ScaleV:0.######} 0 {OffsetV:0.###} {OffsetP:0.###})")
            : String.Create(CultureInfo.InvariantCulture, $"matrix({ScaleP:0.######} 0 0 {-ScaleV:0.######} {OffsetP:0.###} {OffsetV:0.###})");

        public (double X, double Y) Map(double position, double value) => Horizontal
            ? (OffsetV + (value * ScaleV), OffsetP + (position * ScaleP))
            : (OffsetP + (position * ScaleP), OffsetV - (value * ScaleV));
    }
}

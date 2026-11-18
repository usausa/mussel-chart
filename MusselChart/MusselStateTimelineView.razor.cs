namespace MusselChart;

using System.Globalization;
using System.Runtime.InteropServices;

using Microsoft.AspNetCore.Components;

using MusselChart.Internal;

public sealed partial class MusselStateTimelineView<TItem>
{
    private const double Padding = 8;

    private const double LabelGap = 8;

    private const double LabelFontSize = 11;

    private const double TextFontSize = 11;

    private const double TextPadding = 2;

    private const double AxisHeight = 20;

    private const double LabelCharWidth = 6.6;

    private const double LabelWidthRatio = 1d / 3;

    private const int MinCharacters = 2;

    private readonly StateTimelineLayout layout = new();

    private readonly List<ChartLegendItem> legendItems = [];

    private readonly List<TItem> copied = [];

    private readonly List<BandView> bands = [];

    private readonly List<BandText> bandTexts = [];

    private readonly List<ChartTipRow> tipRows = [];

    private readonly List<RowLabel> rowLabels = [];

    private readonly List<AxisLabel> axisLabels = [];

    private readonly List<string> gridLines = [];

    private readonly List<double> ticks = [];

    private readonly List<(long Utc, double Local)> zoneTicks = [];

    private readonly List<long> offsetChanges = [];

    private readonly List<LabelExtent> extents = [];

    private readonly List<int> selected = [];

    private readonly List<StateTimelineEntry> entries = [];

    private readonly List<double> times = [];

    private string emptyText = string.Empty;

    private string plotTop = string.Empty;

    private string plotBottom = string.Empty;

    private string axisY = string.Empty;

    private string fillOpacity = string.Empty;

    private string? lineWidth;

    [Parameter]
    [EditorRequired]
    public IReadOnlyList<TItem>? Items { get; set; }

    [Parameter]
    public Func<TItem, string?>? Row { get; set; }

    [Parameter]
    [EditorRequired]
    public Func<TItem, DateTime>? Start { get; set; }

    [Parameter]
    public Func<TItem, DateTime?>? End { get; set; }

    [Parameter]
    [EditorRequired]
    public Func<TItem, object?>? State { get; set; }

    [Parameter]
    public DateTime? Minimum { get; set; }

    [Parameter]
    public DateTime? Maximum { get; set; }

    [Parameter]
    public IReadOnlyList<ChartValueMapping>? ValueMappings { get; set; }

    [Parameter]
    public IReadOnlyList<ChartThreshold>? Thresholds { get; set; }

    [Parameter]
    public bool MergeValues { get; set; } = true;

    [Parameter]
    public ChartVisibilityMode ShowValue { get; set; }

    [Parameter]
    public ChartValueAlignment AlignValue { get; set; }

    [Parameter]
    public double RowHeight { get; set; } = 0.9;

    [Parameter]
    public double FillOpacity { get; set; } = 0.7;

    [Parameter]
    public double LineWidth { get; set; }

    [Parameter]
    public string? ValueFormat { get; set; }

    [Parameter]
    public Func<double, string>? ValueFormatter { get; set; }

    [Parameter]
    public bool? LegendVisible { get; set; }

    [Parameter]
    public ChartLegendPosition LegendPosition { get; set; } = ChartLegendPosition.Bottom;

    [Parameter]
    public TimeZoneInfo? TimeZone { get; set; }

    private void Prepare()
    {
        var culture = Culture ?? CultureInfo.CurrentCulture;
        WatchSource(Items);
        ApplySources();
        var zone = TimeZone ?? TimeZoneInfo.Local;
        layout.Update(new StateTimelineLayoutOptions
        {
            Mappings = ValueMappings,
            Thresholds = Thresholds,
            MergeValues = MergeValues,
            ValueFormat = ValueFormat,
            ValueFormatter = ValueFormatter,
            Culture = culture
        }, ReadEntries(zone));

        fillOpacity = Math.Clamp(FillOpacity, 0, 1).ToString("0.###", CultureInfo.InvariantCulture);
        lineWidth = LineWidth > 0 ? ChartFormat.Pixel(LineWidth) : null;

        bands.Clear();
        bandTexts.Clear();
        rowLabels.Clear();
        axisLabels.Clear();
        gridLines.Clear();
        legendItems.Clear();

        var (start, end) = ResolveRange(zone);
        emptyText = layout.Segments.Count > 0 ? string.Empty : EmptyText ?? string.Empty;
        var bottom = Height - AxisHeight;
        var labelWidth = MeasureRowLabels();
        var left = Padding + (labelWidth > 0 ? labelWidth + LabelGap : 0);
        var plotWidth = Math.Max(Width - Padding - left, 1);
        var plotHeight = Math.Max(bottom - Padding, 1);
        plotTop = ChartFormat.Pixel(Padding);
        plotBottom = ChartFormat.Pixel(bottom);
        axisY = ChartFormat.Pixel(Height - 6);

        var rows = layout.RowNames.Count;
        var ratio = Math.Clamp(RowHeight, 0, 1);
        var rowHeight = rows > 0 ? plotHeight * ratio / rows : 0;
        var pitch = rows > 1 ? rowHeight + (plotHeight * (1 - ratio) / (rows - 1)) : rowHeight;
        if (end > start)
        {
            var dayFirst = ChartFormat.IsDayFirst(culture);
            ArrangeAxis(start, end, left, plotWidth, culture, dayFirst, zone);
            ArrangeBands(start, end, left, plotWidth, rowHeight, pitch, dayFirst, zone);
        }

        ArrangeRowLabels(labelWidth, rowHeight, pitch);
        if (LegendVisible ?? true)
        {
            foreach (var (text, color) in layout.Legend)
            {
                legendItems.Add(new ChartLegendItem(text, text, color));
            }
        }
    }

    private List<StateTimelineEntry> ReadEntries(TimeZoneInfo zone)
    {
        entries.Clear();
        if ((Items is null) || (Start is null) || (State is null))
        {
            return entries;
        }

        var items = Items is ChartRingBuffer<TItem> buffer ? buffer.CopyTo(copied) : Items;
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var (text, number) = StateTimelineLayout.ReadState(State(item));
            var end = End?.Invoke(item) is { } value ? ChartTime.ToUtcTicks(value, zone) : Double.NaN;
            entries.Add(new StateTimelineEntry(i, Row?.Invoke(item), ChartTime.ToUtcTicks(Start(item), zone), end, text, number));
        }

        return entries;
    }

    private static double ToLocal(double utcTicks, TimeZoneInfo zone) => ChartTime.ToZoneTicks(ChartDateTime.ClampTicks(utcTicks), zone);

    private (double Start, double End) ResolveRange(TimeZoneInfo zone)
    {
        var minimum = Double.PositiveInfinity;
        var maximum = Double.NegativeInfinity;
        foreach (var segment in layout.Segments)
        {
            minimum = Math.Min(minimum, segment.Start);
            maximum = Math.Max(maximum, Double.IsFinite(segment.End) ? segment.End : segment.Start);
        }

        var start = Minimum is { } low ? ChartTime.ToUtcTicks(low, zone) : minimum;
        var end = Maximum is { } high ? ChartTime.ToUtcTicks(high, zone) : maximum;
        if (!(end > start) && Double.IsFinite(start))
        {
            end = start + TimeSpan.TicksPerSecond;
        }

        return (start, end);
    }

    private double MeasureRowLabels()
    {
        if (!layout.HasRowNames)
        {
            return 0;
        }

        var widest = 0d;
        // ReSharper disable once ForCanBeConvertedToForeach
        for (var i = 0; i < layout.RowNames.Count; i++)
        {
            widest = Math.Max(widest, ChartText.Measure(layout.RowNames[i], LabelFontSize));
        }

        return Math.Min(widest, Width * LabelWidthRatio);
    }

    private void ArrangeAxis(double start, double end, double left, double plotWidth, CultureInfo culture, bool dayFirst, TimeZoneInfo zone)
    {
        var span = end - start;
        var divisions = TickGenerator.GetMaxDivisions(plotWidth, TickGenerator.HorizontalLabelSpacing, null);
        var widths = new TimeLabelWidths(8 * LabelCharWidth, 5 * LabelCharWidth, 5 * LabelCharWidth, 7 * LabelCharWidth);
        var interval = TickGenerator.SelectTimeInterval(span, plotWidth, divisions, LabelGap, widths);
        TickGenerator.ZoneTime(ChartDateTime.ClampTicks(start), ChartDateTime.ClampTicks(end), zone, interval, culture.DateTimeFormat.FirstDayOfWeek, ticks, zoneTicks, offsetChanges);
        foreach (var (utc, local) in zoneTicks)
        {
            var x = ChartFormat.Pixel(left + ((utc - start) / span * plotWidth));
            gridLines.Add(x);
            axisLabels.Add(new AxisLabel(x, ChartDateTime.FromTicks(local).ToString(interval.GetTickFormat(local, dayFirst), CultureInfo.InvariantCulture)));
        }
    }

    private void ArrangeBands(double start, double end, double left, double plotWidth, double rowHeight, double pitch, bool dayFirst, TimeZoneInfo zone)
    {
        var span = end - start;
        var right = left + plotWidth;
        var precision = GetTimePrecision(end, zone);
        var format = TimeInterval.GetTrackingFormat(precision, DayOf(start, zone) != DayOf(end, zone), dayFirst);
        var dayFormat = TimeInterval.GetTrackingFormat(precision, true, dayFirst);
        var hover = TrackballMode == ChartTrackballMode.Hover;
        foreach (var segment in layout.Segments)
        {
            var segmentEnd = Double.IsFinite(segment.End) ? Math.Min(segment.End, end) : end;
            var from = Math.Round(Math.Max(left + ((segment.Start - start) / span * plotWidth), left));
            var to = Math.Round(Math.Min(left + ((segmentEnd - start) / span * plotWidth), right));
            if ((to <= left) || (from >= right))
            {
                continue;
            }

            var top = Math.Round(Padding + (segment.Row * pitch));
            var height = Math.Max(1, Math.Round(Padding + (segment.Row * pitch) + rowHeight) - top);
            var width = Math.Max(1, to - from);
            var state = layout.States[segment.State];
            string? tip = null;
            if (hover)
            {
                var bandFormat = DayOf(segment.Start, zone) != DayOf(segmentEnd, zone) ? dayFormat : format;
                var title = FormatTime(ToLocal(segment.Start, zone), bandFormat) + " – " + FormatTime(ToLocal(segmentEnd, zone), bandFormat);
                tipRows.Clear();
                tipRows.Add(new ChartTipRow(state.Color, layout.RowNames[segment.Row], state.Text));
                tipRows.Add(new ChartTipRow(null, null, StateTimelineLayout.FormatDuration(segmentEnd - segment.Start)));
                tip = ChartTip.Create(title, tipRows);
            }

            bands.Add(new BandView(ChartFormat.Pixel(from), ChartFormat.Pixel(top), ChartFormat.Pixel(width), ChartFormat.Pixel(height), state.Color, lineWidth is null ? null : state.Color, tip));
            AddBandText(state, from, width, top + (height / 2));
        }
    }

    private static string FormatTime(double ticks, string format) =>
        ChartDateTime.FromTicks(ticks).ToString(format, CultureInfo.InvariantCulture);

    private static long DayOf(double utcTicks, TimeZoneInfo zone) => (long)(ToLocal(utcTicks, zone) / TimeSpan.TicksPerDay);

    private TimeLabelPrecision GetTimePrecision(double end, TimeZoneInfo zone)
    {
        times.Clear();
        // ReSharper disable once ForCanBeConvertedToForeach
        for (var i = 0; i < layout.Segments.Count; i++)
        {
            var segment = layout.Segments[i];
            times.Add(ToLocal(segment.Start, zone));
            if (Double.IsFinite(segment.End))
            {
                times.Add(ToLocal(Math.Min(segment.End, end), zone));
            }
        }

        return TimeInterval.GetValuePrecision(CollectionsMarshal.AsSpan(times), TimeLabelPrecision.Month);
    }

    private void AddBandText(in StateTimelineState state, double from, double width, double middle)
    {
        if ((ShowValue == ChartVisibilityMode.Never) || (state.Text.Length == 0))
        {
            return;
        }

        var inset = Math.Max(LineWidth, 0) + TextPadding;
        var available = width - (inset * 2);
        if (available <= 0)
        {
            return;
        }

        var head = state.Text[..Math.Min(MinCharacters, state.Text.Length)];
        if ((ShowValue == ChartVisibilityMode.Auto) && (ChartText.Measure(head, TextFontSize) > available))
        {
            return;
        }

        var text = ChartText.Truncate(state.Text, TextFontSize, available);
        var (x, anchor) = AlignValue switch
        {
            ChartValueAlignment.Center => (from + (width / 2), "middle"),
            ChartValueAlignment.Right => (from + width - inset, "end"),
            _ => (from + inset, "start")
        };
        bandTexts.Add(new BandText(ChartFormat.Pixel(x), ChartFormat.Pixel(middle), anchor, text, ChartColor.Contrast(state.Color)));
    }

    private void ArrangeRowLabels(double labelWidth, double rowHeight, double pitch)
    {
        if (!(labelWidth > 0))
        {
            return;
        }

        var height = ChartText.LineHeight(LabelFontSize);
        extents.Clear();
        for (var row = 0; row < layout.RowNames.Count; row++)
        {
            var center = Padding + (row * pitch) + (rowHeight / 2);
            extents.Add(new LabelExtent(center - (height / 2), center + (height / 2)));
        }

        LabelThinning.Select(extents, 0, Int32.MaxValue, selected);
        foreach (var row in selected)
        {
            var center = Padding + (row * pitch) + (rowHeight / 2);
            rowLabels.Add(new RowLabel(ChartFormat.Pixel(Padding), ChartFormat.Pixel(center), ChartText.Truncate(layout.RowNames[row] ?? string.Empty, LabelFontSize, labelWidth)));
        }
    }

    private readonly record struct BandView(string X, string Y, string Width, string Height, string Color, string? Stroke, string? Tip);

    private readonly record struct BandText(string X, string Y, string Anchor, string Text, string Color);

    private readonly record struct RowLabel(string X, string Y, string Text);
}

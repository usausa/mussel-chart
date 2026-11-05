namespace MusselChart;

using System.Globalization;

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

    private readonly List<BandView> bands = [];

    private readonly List<BandText> bandTexts = [];

    private readonly List<RowLabel> rowLabels = [];

    private readonly List<AxisLabel> axisLabels = [];

    private readonly List<string> gridLines = [];

    private readonly List<double> ticks = [];

    private readonly List<LabelExtent> extents = [];

    private readonly List<int> selected = [];

    private string viewBox = string.Empty;

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

        viewBox = FormattableString.Invariant($"0 0 {ChartFormat.Pixel(Width)} {ChartFormat.Pixel(Height)}");
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
            ArrangeAxis(start, end, left, plotWidth, culture);
            ArrangeBands(start, end, left, plotWidth, rowHeight, pitch, culture);
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
        var entries = new List<StateTimelineEntry>(Items?.Count ?? 0);
        if ((Items is null) || (Start is null) || (State is null))
        {
            return entries;
        }

        for (var i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            var (text, number) = ReadState(State(item));
            var end = End?.Invoke(item) is { } value ? ToLocalTicks(value, zone) : Double.NaN;
            entries.Add(new StateTimelineEntry(i, Row?.Invoke(item), ToLocalTicks(Start(item), zone), end, text, number));
        }

        return entries;
    }

    private static double ToLocalTicks(DateTime value, TimeZoneInfo zone) => ChartTime.ToZoneTicks(ChartTime.ToUtcTicks(value, zone), zone);

    // Numbers take the thresholds and the color scale, and everything else is a text state
    private static (string? Text, double Number) ReadState(object? value) => value switch
    {
        null => (null, Double.NaN),
        string text => (text, Double.NaN),
        Enum => (value.ToString(), Double.NaN),
        bool => (value.ToString(), Double.NaN),
        IConvertible convertible and (byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal) => (null, convertible.ToDouble(CultureInfo.InvariantCulture)),
        _ => (value.ToString(), Double.NaN)
    };

    private (double Start, double End) ResolveRange(TimeZoneInfo zone)
    {
        var minimum = Double.PositiveInfinity;
        var maximum = Double.NegativeInfinity;
        foreach (var segment in layout.Segments)
        {
            minimum = Math.Min(minimum, segment.Start);
            maximum = Math.Max(maximum, Double.IsFinite(segment.End) ? segment.End : segment.Start);
        }

        var start = Minimum is { } low ? ToLocalTicks(low, zone) : minimum;
        var end = Maximum is { } high ? ToLocalTicks(high, zone) : maximum;
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

        var widest = layout.RowNames.Select(static x => ChartText.Measure(x, LabelFontSize)).DefaultIfEmpty(0).Max();
        return Math.Min(widest, Width * LabelWidthRatio);
    }

    private void ArrangeAxis(double start, double end, double left, double plotWidth, CultureInfo culture)
    {
        var span = end - start;
        var divisions = TickGenerator.GetMaxDivisions(plotWidth, TickGenerator.HorizontalLabelSpacing, null);
        var widths = new TimeLabelWidths(8 * LabelCharWidth, 5 * LabelCharWidth, 5 * LabelCharWidth, 7 * LabelCharWidth);
        var interval = TickGenerator.SelectTimeInterval(span, plotWidth, divisions, LabelGap, widths);
        TickGenerator.Time(start, end, interval, culture.DateTimeFormat.FirstDayOfWeek, ticks);
        foreach (var tick in ticks)
        {
            var x = ChartFormat.Pixel(left + ((tick - start) / span * plotWidth));
            gridLines.Add(x);
            axisLabels.Add(new AxisLabel(x, new DateTime((long)tick, DateTimeKind.Unspecified).ToString(interval.GetTickFormat(tick), culture)));
        }
    }

    private void ArrangeBands(double start, double end, double left, double plotWidth, double rowHeight, double pitch, CultureInfo culture)
    {
        var span = end - start;
        var right = left + plotWidth;
        var precision = GetTimePrecision();
        var spansDays = (long)(start / TimeSpan.TicksPerDay) != (long)(end / TimeSpan.TicksPerDay);
        var format = TimeInterval.GetTrackingFormat(precision, spansDays);
        var hover = TrackballMode == ChartTrackballMode.Hover;
        foreach (var segment in layout.Segments)
        {
            var segmentEnd = Double.IsFinite(segment.End) ? segment.End : end;
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
                var title = FormatTime(segment.Start, format, culture) + " – " + FormatTime(segmentEnd, format, culture);
                tip = ChartTip.Create(title, [new ChartTipRow(state.Color, layout.RowNames[segment.Row], state.Text), new ChartTipRow(null, null, StateTimelineLayout.FormatDuration(segmentEnd - segment.Start))]);
            }

            bands.Add(new BandView(ChartFormat.Pixel(from), ChartFormat.Pixel(top), ChartFormat.Pixel(width), ChartFormat.Pixel(height), state.Color, lineWidth is null ? null : state.Color, tip));
            AddBandText(state, from, width, top + (height / 2));
        }
    }

    private static string FormatTime(double ticks, string format, CultureInfo culture) =>
        new DateTime((long)ticks, DateTimeKind.Unspecified).ToString(format, culture);

    private TimeLabelPrecision GetTimePrecision()
    {
        var times = new List<double>(layout.Segments.Count * 2);
        foreach (var segment in layout.Segments)
        {
            times.Add(segment.Start);
            if (Double.IsFinite(segment.End))
            {
                times.Add(segment.End);
            }
        }

        return TimeInterval.GetValuePrecision(times.ToArray(), TimeLabelPrecision.Month);
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

    private sealed record BandView(string X, string Y, string Width, string Height, string Color, string? Stroke, string? Tip);

    private sealed record BandText(string X, string Y, string Anchor, string Text, string Color);

    private sealed record RowLabel(string X, string Y, string Text);
}

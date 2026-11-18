namespace MusselChart;

using System.Globalization;

using Microsoft.AspNetCore.Components;

using MusselChart.Internal;

public sealed partial class MusselBarGaugeView : IDisposable
{
    private const double SegmentGap = 2;

    private const double ValueCharWidth = 7.5;

    private readonly string idPrefix = "mussel-" + Guid.NewGuid().ToString("N")[..12];

    private readonly List<GaugeRow> rows = [];

    private readonly List<ChartEntry> copied = [];

    private readonly List<ChartThreshold> sorted = [];

    private readonly Dictionary<string, List<GaugeStop>> stops = new(StringComparer.Ordinal);

    private readonly List<(string Gradient, string Pattern, string Mask)> ids = [];

    private readonly ChartSourceWatcher sources;

    private string rootClass = string.Empty;

    private string barHeight = string.Empty;

    private string segmentWidth = string.Empty;

    private string segmentCell = string.Empty;

    private string valueWidth = string.Empty;

    public MusselBarGaugeView()
    {
        sources = new ChartSourceWatcher(InvokeAsync, StateHasChanged);
    }

    [Parameter]
    public IReadOnlyList<ChartEntry>? Items { get; set; }

    [Parameter]
    public double Minimum { get; set; }

    [Parameter]
    public double Maximum { get; set; } = 100;

    [Parameter]
    public ChartBarGaugeMode DisplayMode { get; set; }

    [Parameter]
    public double SegmentWidth { get; set; } = 12;

    [Parameter]
    public double BarThickness { get; set; } = 8;

    [Parameter]
    public ChartBarGaugeLabelPosition LabelPosition { get; set; }

    [Parameter]
    public bool ValueVisible { get; set; } = true;

    [Parameter]
    public ChartBarGaugeValueMode ValueMode { get; set; } = ChartBarGaugeValueMode.Percent;

    [Parameter]
    public string? ValueFormat { get; set; }

    [Parameter]
    public Func<double, string>? ValueFormatter { get; set; }

    [Parameter]
    public bool TrackVisible { get; set; } = true;

    [Parameter]
    public IReadOnlyList<ChartThreshold>? Thresholds { get; set; }

    [Parameter]
    public string? Color { get; set; }

    [Parameter]
    public IReadOnlyList<string>? Palette { get; set; }

    [Parameter]
    public CultureInfo? Culture { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    public void Dispose() => sources.Clear();

    private void Prepare()
    {
        var culture = Culture ?? CultureInfo.CurrentCulture;
        sources.Add(Items);
        sources.Apply();
        var fallback = Color ?? (Palette is { Count: > 0 } ? Palette[0] : ChartPalettes.DefaultLight[0]);
        rootClass = LabelPosition == ChartBarGaugeLabelPosition.Left ? "mussel-gauge mussel-gauge-left" : "mussel-gauge";
        barHeight = ChartFormat.Pixel(BarThickness);
        segmentWidth = ChartFormat.Pixel(SegmentWidth);
        segmentCell = ChartFormat.Pixel(Math.Max(SegmentWidth - SegmentGap, 1));

        rows.Clear();
        sorted.Clear();
        if (Thresholds is { Count: > 0 })
        {
            sorted.AddRange(Thresholds.OrderBy(static x => x.Value));
        }

        stops.Clear();
        var items = Items is ChartRingBuffer<ChartEntry> buffer ? buffer.CopyTo(copied) : Items ?? [];
        var longest = 0;
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var maximum = item.Maximum ?? Maximum;
            var range = maximum - Minimum;
            var known = Double.IsFinite(item.Value);
            var ratio = known && (range > 0) ? (item.Value - Minimum) / range : 0;
            var percent = ratio * 100;
            var own = item.Color ?? fallback;
            var color = known && (Thresholds is { Count: > 0 }) ? ResolveColor(percent, own) : own;
            if (!stops.TryGetValue(own, out var gradient))
            {
                gradient = CreateStops(own);
                stops[own] = gradient;
            }

            while (ids.Count <= i)
            {
                var n = ids.Count;
                ids.Add(($"{idPrefix}-g{n}", $"{idPrefix}-p{n}", $"{idPrefix}-m{n}"));
            }

            var text = ValueMode switch
            {
                _ when !known => "-",
                ChartBarGaugeValueMode.Percent => ratio.ToString("P0", culture),
                ChartBarGaugeValueMode.ValueOfMaximum => $"{FormatValue(item.Value, culture)} / {FormatValue(maximum, culture)}",
                _ => FormatValue(item.Value, culture)
            };
            longest = Math.Max(longest, text.Length);
            var target = (item.Target is { } goal) && (range > 0) ? ToPercent((goal - Minimum) / range) : null;
            rows.Add(new GaugeRow(
                item.Label,
                ToPercent(ratio) ?? "0%",
                target,
                color,
                text,
                gradient,
                ids[i].Gradient,
                ids[i].Pattern,
                ids[i].Mask));
        }

        valueWidth = ChartFormat.Pixel(Math.Max(longest, 3) * ValueCharWidth);
    }

    private string FormatValue(double value, CultureInfo culture) => ChartFormat.Value(value, ValueFormat, ValueFormatter, culture);

    private string ResolveColor(double percent, string fallback)
    {
        var threshold = ChartFormat.FindThreshold(Thresholds, percent);
        return threshold is null ? fallback : threshold.Color ?? (threshold.Level == ChartThresholdLevel.None ? fallback : ChartColor.Level(threshold.Level));
    }

    private List<GaugeStop> CreateStops(string fallback)
    {
        if (sorted.Count == 0)
        {
            return [new GaugeStop("0", fallback)];
        }

        var gradient = new List<GaugeStop>();
        if (!Double.IsNegativeInfinity(sorted[0].Value))
        {
            var first = Double.IsFinite(sorted[0].Value) ? Math.Clamp(sorted[0].Value / 100, 0, 1) : 0;
            gradient.Add(new GaugeStop("0", fallback));
            gradient.Add(new GaugeStop(first.ToString("0.####", CultureInfo.InvariantCulture), fallback));
        }

        for (var i = 0; i < sorted.Count; i++)
        {
            var threshold = sorted[i];
            var color = threshold.Color ?? (threshold.Level == ChartThresholdLevel.None ? fallback : ChartColor.Level(threshold.Level));
            var start = Double.IsFinite(threshold.Value) ? Math.Clamp(threshold.Value / 100, 0, 1) : 0;
            var end = i + 1 < sorted.Count ? Math.Clamp(sorted[i + 1].Value / 100, 0, 1) : 1;
            gradient.Add(new GaugeStop(start.ToString("0.####", CultureInfo.InvariantCulture), color));
            gradient.Add(new GaugeStop(end.ToString("0.####", CultureInfo.InvariantCulture), color));
        }

        return gradient;
    }

    private static string? ToPercent(double ratio) =>
        Double.IsFinite(ratio) ? (Math.Clamp(ratio, 0, 1) * 100).ToString("0.##", CultureInfo.InvariantCulture) + "%" : null;

    protected override bool ShouldRender()
    {
        sources.Rendered();
        return true;
    }

    private sealed record GaugeStop(string Offset, string Color);

    private sealed record GaugeRow(string? Label, string Ratio, string? Target, string Color, string ValueText, IReadOnlyList<GaugeStop> Stops, string GradientId, string PatternId, string MaskId);
}

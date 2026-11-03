namespace MusselChart;

using System.Globalization;

using Microsoft.AspNetCore.Components;

using MusselChart.Internal;

public sealed partial class MusselBarGaugeView
{
    private const double SegmentGap = 2;

    private const double ValueCharWidth = 7.5;

    private readonly string idPrefix = "mussel-" + Guid.NewGuid().ToString("N")[..12];

    private readonly List<GaugeRow> rows = [];

    private string rootClass = string.Empty;

    private string barHeight = string.Empty;

    private string segmentWidth = string.Empty;

    private string segmentCell = string.Empty;

    private string valueWidth = string.Empty;

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

    private void Prepare()
    {
        var culture = Culture ?? CultureInfo.CurrentCulture;
        var fallback = Color ?? (Palette is { Count: > 0 } ? Palette[0] : ChartPalettes.DefaultLight[0]);
        rootClass = LabelPosition == ChartBarGaugeLabelPosition.Left ? "mussel-gauge mussel-gauge-left" : "mussel-gauge";
        barHeight = ChartFormat.Pixel(BarThickness);
        segmentWidth = ChartFormat.Pixel(SegmentWidth);
        segmentCell = ChartFormat.Pixel(Math.Max(SegmentWidth - SegmentGap, 1));

        rows.Clear();
        var items = Items ?? [];
        var longest = 0;
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var maximum = item.Maximum ?? Maximum;
            var range = maximum - Minimum;
            var ratio = range > 0 ? (item.Value - Minimum) / range : 0;
            var percent = ratio * 100;
            var color = Thresholds is { Count: > 0 } ? ResolveColor(percent, item.Color ?? fallback) : item.Color ?? fallback;
            var stops = Thresholds is { Count: > 0 } ? CreateStops(item.Color ?? fallback) : [new GaugeStop("0", item.Color ?? fallback)];
            var text = ValueMode switch
            {
                ChartBarGaugeValueMode.Percent => ratio.ToString("P0", culture),
                ChartBarGaugeValueMode.ValueOfMaximum => $"{FormatValue(item.Value, culture)} / {FormatValue(maximum, culture)}",
                _ => FormatValue(item.Value, culture)
            };
            longest = Math.Max(longest, text.Length);
            var target = item.Target is { } goal && (range > 0) ? ToPercent((goal - Minimum) / range) : null;
            rows.Add(new GaugeRow(
                item.Label,
                ToPercent(ratio)!,
                target,
                color,
                text,
                stops,
                $"{idPrefix}-g{i}",
                $"{idPrefix}-p{i}",
                $"{idPrefix}-m{i}"));
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
        var stops = new List<GaugeStop>();
        var sorted = Thresholds!.OrderBy(static x => x.Value).ToList();
        for (var i = 0; i < sorted.Count; i++)
        {
            var threshold = sorted[i];
            var color = threshold.Color ?? (threshold.Level == ChartThresholdLevel.None ? fallback : ChartColor.Level(threshold.Level));
            var start = Double.IsFinite(threshold.Value) ? Math.Clamp(threshold.Value / 100, 0, 1) : 0;
            var end = i + 1 < sorted.Count ? Math.Clamp(sorted[i + 1].Value / 100, 0, 1) : 1;
            stops.Add(new GaugeStop(start.ToString("0.####", CultureInfo.InvariantCulture), color));
            stops.Add(new GaugeStop(end.ToString("0.####", CultureInfo.InvariantCulture), color));
        }

        return stops;
    }

    private static string? ToPercent(double ratio) =>
        Double.IsFinite(ratio) ? (Math.Clamp(ratio, 0, 1) * 100).ToString("0.##", CultureInfo.InvariantCulture) + "%" : null;

    private sealed record GaugeStop(string Offset, string Color);

    private sealed record GaugeRow(string? Label, string Ratio, string? Target, string Color, string ValueText, IReadOnlyList<GaugeStop> Stops, string GradientId, string PatternId, string MaskId);
}

namespace MusselChart;

using System.Globalization;

using Microsoft.AspNetCore.Components;

using MusselChart.Internal;

public sealed partial class MusselStatView
{
    private const double BrightThreshold = 180;

    private readonly string gradientId = "mussel-" + Guid.NewGuid().ToString("N")[..12];

    private TrendPath trend = TrendPath.Empty;

    private string rootClass = string.Empty;

    private string valueText = string.Empty;

    private string? valueStyle;

    private string tileColor = string.Empty;

    private string gradientFrom = string.Empty;

    private string gradientTo = string.Empty;

    private string backgroundFill = string.Empty;

    private string backgroundOpacity = "1";

    private string trendFill = string.Empty;

    private string trendFillOpacity = "0.2";

    private string trendStroke = string.Empty;

    [Parameter]
    public string? Title { get; set; }

    [Parameter]
    public double Value { get; set; }

    [Parameter]
    public string? ValueFormat { get; set; }

    [Parameter]
    public Func<double, string>? ValueFormatter { get; set; }

    [Parameter]
    public string? Unit { get; set; }

    [Parameter]
    public IEnumerable<double>? TrendValues { get; set; }

    [Parameter]
    public ChartTrendPlacement TrendPlacement { get; set; }

    [Parameter]
    public IReadOnlyList<ChartThreshold>? Thresholds { get; set; }

    [Parameter]
    public ChartStatColorMode ColorMode { get; set; }

    [Parameter]
    public ChartFillMode FillMode { get; set; }

    [Parameter]
    public string? Fill { get; set; }

    [Parameter]
    public IReadOnlyList<string>? Palette { get; set; }

    [Parameter]
    public string? EmptyText { get; set; }

    [Parameter]
    public CultureInfo? Culture { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private void Prepare()
    {
        var culture = Culture ?? CultureInfo.CurrentCulture;
        var threshold = ChartFormat.FindThreshold(Thresholds, Value);
        tileColor = Fill ?? threshold?.Color ?? (threshold is { Level: not ChartThresholdLevel.None } ? ChartColor.Level(threshold.Level) : null) ?? (Palette is { Count: > 0 } ? Palette[0] : ChartPalettes.DefaultLight[0]);
        valueText = Double.IsNaN(Value) ? EmptyText ?? "-" : ChartFormat.Value(Value, ValueFormat, ValueFormatter, culture);
        trend = TrendPath.Create(TrendValues, null, null, false, 4);

        var filled = (ColorMode == ChartStatColorMode.Background) && (FillMode == ChartFillMode.Gradient);
        gradientFrom = ChartColor.Shift(tileColor, 8, -15);
        gradientTo = ChartColor.Shift(tileColor, -8, -5);
        backgroundFill = filled ? $"url(#{gradientId})" : tileColor;
        backgroundOpacity = filled ? "1" : "0.15";
        valueStyle = ColorMode == ChartStatColorMode.Value ? $"color: {tileColor}" : null;

        if (filled)
        {
            trendFill = "#FFFFFF";
            trendFillOpacity = "0.25";
            trendStroke = ChartColor.Shift(tileColor, 0, 40);
        }
        else
        {
            trendFill = tileColor;
            trendFillOpacity = "0.2";
            trendStroke = tileColor;
        }

        var text = filled ? (ChartColor.Brightness(tileColor) > BrightThreshold ? " mussel-stat-dark-text" : " mussel-stat-light-text") : string.Empty;
        var empty = Double.IsNaN(Value) ? " mussel-stat-empty" : string.Empty;
        rootClass = "mussel-stat" + text + empty;
    }
}

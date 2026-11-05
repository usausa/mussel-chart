namespace MusselChart;

using System.Globalization;

using Microsoft.AspNetCore.Components;

using MusselChart.Internal;

public sealed partial class MusselPieChartView
{
    private readonly PieLayout layout = new();

    private readonly HashSet<string> hiddenLabels = new(StringComparer.Ordinal);

    private readonly List<ChartLegendItem> legendItems = [];

    private readonly List<SliceView> sliceViews = [];

    private readonly List<InsideLabel> insideLabels = [];

    private readonly List<OutsideLabel> outsideLabels = [];

    private string sliceClass = string.Empty;

    private string centerX = string.Empty;

    private string emptyText = string.Empty;

    [Parameter]
    public IReadOnlyList<ChartEntry>? Items { get; set; }

    [Parameter]
    public IReadOnlyList<string>? Palette { get; set; }

    [Parameter]
    public double InnerRadius { get; set; } = 0.6;

    [Parameter]
    public double StartAngle { get; set; }

    [Parameter]
    public double SweepAngle { get; set; } = 360;

    [Parameter]
    public double SegmentGap { get; set; } = 2;

    [Parameter]
    public ChartSortMode SortMode { get; set; }

    [Parameter]
    public int MaxSlices { get; set; } = 8;

    [Parameter]
    public double OthersThreshold { get; set; }

    [Parameter]
    public string OthersLabel { get; set; } = "Other";

    [Parameter]
    public ChartPieLabelsMode LabelsMode { get; set; }

    [Parameter]
    public ChartPieLabelContent LabelContent { get; set; }

    [Parameter]
    public bool? LegendVisible { get; set; }

    [Parameter]
    public ChartLegendPosition LegendPosition { get; set; } = ChartLegendPosition.Bottom;

    [Parameter]
    public ChartLegendValueMode LegendValueMode { get; set; }

    [Parameter]
    public bool LegendToggleEnabled { get; set; }

    [Parameter]
    public ChartCenterValueMode CenterValueMode { get; set; }

    [Parameter]
    public double? CenterValue { get; set; }

    [Parameter]
    public string? CenterLabel { get; set; }

    [Parameter]
    public string? CenterValueFormat { get; set; }

    [Parameter]
    public Func<double, string>? CenterValueFormatter { get; set; }

    [Parameter]
    public string? ValueFormat { get; set; }

    [Parameter]
    public Func<double, string>? ValueFormatter { get; set; }

    private void Prepare()
    {
        var culture = Culture ?? CultureInfo.CurrentCulture;
        if (!LegendToggleEnabled)
        {
            hiddenLabels.Clear();
        }

        layout.Update(Width, Height, new PieLayoutOptions
        {
            InnerRadius = InnerRadius,
            StartAngle = StartAngle,
            SweepAngle = SweepAngle,
            Gap = SegmentGap,
            SortMode = SortMode,
            MaxSlices = MaxSlices,
            OthersThreshold = OthersThreshold,
            OthersLabel = OthersLabel,
            LabelsMode = LabelsMode,
            LabelContent = LabelContent,
            CenterValueMode = CenterValueMode,
            CenterValue = CenterValue,
            CenterLabel = CenterLabel,
            CenterValueFormat = CenterValueFormat,
            CenterValueFormatter = CenterValueFormatter,
            ValueFormat = ValueFormat,
            ValueFormatter = ValueFormatter,
            HiddenLabels = hiddenLabels,
            Culture = culture
        }, Items ?? []);

        sliceClass = TrackballMode == ChartTrackballMode.Hover ? "mussel-slices mussel-slices-hover" : "mussel-slices";
        centerX = ChartGeometry.Text(layout.CenterX);
        emptyText = layout.Slices.Count > 0 ? string.Empty : EmptyText ?? string.Empty;

        sliceViews.Clear();
        insideLabels.Clear();
        outsideLabels.Clear();
        var palette = Palette ?? ChartPalettes.DefaultLight;
        foreach (var slice in layout.Slices)
        {
            var color = GetColor(slice, palette);
            var tip = ChartTip.Create(null, [new ChartTipRow(color, slice.Label, $"{slice.ValueText} ({slice.PercentText})")]);
            sliceViews.Add(new SliceView(slice.DataIndex, layout.GetPath(slice), color, tip));
            if ((LabelsMode == ChartPieLabelsMode.Inside) && (layout.GetInsideLabel(slice) is { } point))
            {
                insideLabels.Add(new InsideLabel(ChartGeometry.Text(point.X), ChartGeometry.Text(point.Y), slice.LabelText!, ChartColor.Contrast(color)));
            }
        }

        foreach (var label in layout.OutsideLabels)
        {
            var line = String.Create(CultureInfo.InvariantCulture, $"{label.EdgeX:0.##},{label.EdgeY:0.##} {label.ElbowX:0.##},{label.ElbowY:0.##} {label.EndX:0.##},{label.ElbowY:0.##}");
            outsideLabels.Add(new OutsideLabel(line, ChartGeometry.Text(label.X), ChartGeometry.Text(label.Y), label.Right ? "start" : "end", label.Slice.LabelText!, GetColor(label.Slice, palette)));
        }

        legendItems.Clear();
        if (LegendVisible ?? true)
        {
            foreach (var slice in layout.LegendSlices)
            {
                if (!String.IsNullOrEmpty(slice.Label))
                {
                    var value = LegendValueMode switch
                    {
                        ChartLegendValueMode.Value => slice.ValueText,
                        ChartLegendValueMode.Percent => slice.PercentText,
                        _ => null
                    };
                    legendItems.Add(new ChartLegendItem(slice.IsOthers ? string.Empty : slice.Label, slice.Label, GetColor(slice, palette), value, layout.IsHiddenSlice(slice)));
                }
            }
        }
    }

    private static string GetColor(PieSlice slice, IReadOnlyList<string> palette)
    {
        if (slice.Color is not null)
        {
            return slice.Color;
        }

        return !slice.IsOthers && (slice.Rank >= 0) && (slice.Rank < palette.Count) ? palette[slice.Rank] : ChartColor.Other;
    }

    private void ToggleSlice(string label)
    {
        if (!LegendToggleEnabled || (label.Length == 0))
        {
            return;
        }

        if (!hiddenLabels.Remove(label) && (layout.Slices.Count > 1))
        {
            hiddenLabels.Add(label);
        }
    }

    private readonly record struct SliceView(int Key, string Path, string Color, string Tip);

    private readonly record struct InsideLabel(string X, string Y, string Text, string Color);

    private readonly record struct OutsideLabel(string Line, string X, string Y, string Anchor, string Text, string Color);
}

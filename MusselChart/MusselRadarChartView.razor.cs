namespace MusselChart;

using System.Globalization;

using Microsoft.AspNetCore.Components;

using MusselChart.Internal;

public sealed partial class MusselRadarChartView
{
    private const double RingMargin = 5;

    private const double LevelBoxPadding = 2;

    private readonly string maskId = "mussel-" + Guid.NewGuid().ToString("N")[..12];

    private readonly RadarLayout layout = new();

    private readonly List<RadarSeriesBase> seriesList = [];

    private readonly HashSet<RadarSeriesBase> hiddenSeries = new(ReferenceEqualityComparer.Instance);

    private readonly List<ChartLegendItem> legendItems = [];

    private readonly List<PolygonView> polygons = [];

    private readonly List<AxisLabelView> axisLabels = [];

    private readonly List<LevelLabelView> levelLabels = [];

    private readonly List<AxisAreaView> axisAreas = [];

    private string viewBox = string.Empty;

    private string gridPath = string.Empty;

    private string centerX = string.Empty;

    private string emptyText = string.Empty;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public IReadOnlyList<string>? Palette { get; set; }

    [Parameter]
    public double? Minimum { get; set; }

    [Parameter]
    public double? Maximum { get; set; }

    [Parameter]
    public ChartRadarGridShape GridShape { get; set; }

    [Parameter]
    public int LevelCount { get; set; } = 5;

    [Parameter]
    public string? ValueFormat { get; set; }

    [Parameter]
    public Func<double, string>? ValueFormatter { get; set; }

    [Parameter]
    public bool? LegendVisible { get; set; }

    [Parameter]
    public ChartLegendPosition LegendPosition { get; set; } = ChartLegendPosition.Bottom;

    [Parameter]
    public bool LegendToggleEnabled { get; set; }

    internal void AddSeries(RadarSeriesBase series)
    {
        seriesList.Add(series);
        RequestRender();
    }

    internal void RemoveSeries(RadarSeriesBase series)
    {
        seriesList.Remove(series);
        hiddenSeries.Remove(series);
        RequestRender();
    }

    internal void InvalidateSeries() => RequestRender();

    private bool IsSeriesVisible(RadarSeriesBase series) => series.IsVisible && !hiddenSeries.Contains(series);

    private void Prepare()
    {
        var culture = Culture ?? CultureInfo.CurrentCulture;
        var options = new RadarLayoutOptions
        {
            Minimum = Minimum,
            Maximum = Maximum,
            GridShape = GridShape,
            LevelCount = LevelCount,
            ValueFormat = ValueFormat,
            ValueFormatter = ValueFormatter,
            Culture = culture
        };

        var points = new List<IReadOnlyList<RadarPoint>>(seriesList.Count);
        var visible = new List<bool>(seriesList.Count);
        foreach (var series in seriesList)
        {
            var list = new List<RadarPoint>();
            series.ReadPoints(list);
            points.Add(list);
            visible.Add(IsSeriesVisible(series));
        }

        layout.Update(Width, Height, options, points, visible, RingMargin);

        viewBox = FormattableString.Invariant($"0 0 {ChartFormat.Pixel(Width)} {ChartFormat.Pixel(Height)}");
        gridPath = layout.AxisCount > 0 ? layout.CreateGridPath() : string.Empty;
        centerX = ChartGeometry.Text(layout.CenterX);
        var hasData = points.Exists(static x => x.Any(static p => Double.IsFinite(p.Value)));
        emptyText = hasData ? string.Empty : EmptyText ?? string.Empty;

        var palette = Palette ?? ChartPalettes.DefaultLight;
        var colors = new string[seriesList.Count];
        for (var i = 0; i < colors.Length; i++)
        {
            colors[i] = seriesList[i].Color ?? (i < palette.Count ? palette[i] : ChartColor.Other);
        }

        polygons.Clear();
        for (var s = 0; s < seriesList.Count; s++)
        {
            if (visible[s] && (layout.AxisCount > 0))
            {
                var series = seriesList[s];
                polygons.Add(new PolygonView(layout.CreatePolygon(s), colors[s], series.FillOpacity.ToString("0.###", CultureInfo.InvariantCulture), ChartFormat.Pixel(series.Thickness)));
            }
        }

        ArrangeLabels();
        ArrangeAreas(colors, visible, culture);
        ArrangeLegend(colors);
    }

    private void ArrangeLabels()
    {
        axisLabels.Clear();
        levelLabels.Clear();
        if (!layout.ShowsLabels)
        {
            return;
        }

        foreach (var axis in layout.Axes)
        {
            if (axis.Text is not null)
            {
                axisLabels.Add(new AxisLabelView(ChartGeometry.Text(axis.LabelX), ChartGeometry.Text(axis.LabelY), axis.Anchor, axis.Text));
            }
        }

        var height = ChartText.LineHeight(RadarLayout.LabelFontSize);
        foreach (var level in layout.Levels)
        {
            var y = layout.CenterY - (layout.Radius * layout.GetRatio(level.Value));
            levelLabels.Add(new LevelLabelView(
                ChartGeometry.Text(y),
                level.Text,
                ChartGeometry.Text(layout.CenterX - (level.Width / 2) - LevelBoxPadding),
                ChartGeometry.Text(y - (height / 2)),
                ChartGeometry.Text(level.Width + (LevelBoxPadding * 2)),
                ChartGeometry.Text(height)));
        }
    }

    private void ArrangeAreas(string[] colors, List<bool> visible, CultureInfo culture)
    {
        axisAreas.Clear();
        if (TrackballMode != ChartTrackballMode.Hover)
        {
            return;
        }

        for (var k = 0; k < layout.AxisCount; k++)
        {
            var rows = new List<ChartTipRow>();
            var marks = new List<ChartTipMark>();
            for (var s = 0; s < seriesList.Count; s++)
            {
                var value = layout.GetValue(s, k);
                if (!visible[s] || !Double.IsFinite(value))
                {
                    continue;
                }

                var series = seriesList[s];
                var text = ChartFormat.Value(value, series.ValueFormat ?? ValueFormat, series.ValueFormatter ?? ValueFormatter, culture);
                rows.Add(new ChartTipRow(colors[s], series.Title, text));
                var (x, y) = layout.GetPoint(k, layout.GetRatio(value));
                marks.Add(new ChartTipMark(x, y, colors[s]));
            }

            var (endX, endY) = layout.GetPoint(k, 1);
            var tip = ChartTip.Create(layout.Axes[k].Header, rows, marks, new ChartTipLine(layout.CenterX, layout.CenterY, endX, endY));
            axisAreas.Add(new AxisAreaView(layout.CreateAxisArea(k), tip));
        }
    }

    private void ArrangeLegend(string[] colors)
    {
        legendItems.Clear();
        var listed = seriesList.Where(static x => x.LegendVisible && !String.IsNullOrEmpty(x.Title)).ToList();
        if (!(LegendVisible ?? (listed.Count >= 2)))
        {
            return;
        }

        for (var i = 0; i < seriesList.Count; i++)
        {
            var series = seriesList[i];
            if (series.LegendVisible && !String.IsNullOrEmpty(series.Title))
            {
                legendItems.Add(new ChartLegendItem(i.ToString(CultureInfo.InvariantCulture), series.Title, colors[i], null, !IsSeriesVisible(series)));
            }
        }
    }

    private void ToggleSeries(string key)
    {
        if (!LegendToggleEnabled)
        {
            return;
        }

        var series = seriesList[Int32.Parse(key, CultureInfo.InvariantCulture)];
        if (!hiddenSeries.Remove(series) && (seriesList.Count(IsSeriesVisible) > 1))
        {
            hiddenSeries.Add(series);
        }
    }

    private sealed record PolygonView(string Points, string Color, string Opacity, string Thickness);

    private sealed record AxisLabelView(string X, string Y, string Anchor, string Text);

    private sealed record LevelLabelView(string Y, string Text, string BoxX, string BoxY, string BoxWidth, string BoxHeight);

    private sealed record AxisAreaView(string Path, string Tip);
}

namespace MusselChart;

using MusselChart.Internal;

public sealed class MusselWidgetTests : BunitContext
{
    private static readonly ChartThreshold[] Levels =
    [
        new() { Level = ChartThresholdLevel.Good },
        new() { Value = 70, Level = ChartThresholdLevel.Warning },
        new() { Value = 85, Level = ChartThresholdLevel.Critical }
    ];

    [Fact]
    public void StatShowsFormattedValueAndUnit()
    {
        var cut = Render<MusselStatView>(parameters => parameters
            .Add(static p => p.Title, "CPU")
            .Add(static p => p.Value, 42.345)
            .Add(static p => p.ValueFormat, "N1")
            .Add(static p => p.Unit, "%")
            .Add(static p => p.Culture, CultureInfo.InvariantCulture));

        Assert.Equal("CPU", cut.Find(".mussel-stat-title").TextContent);
        Assert.Equal("42.3%", cut.Find(".mussel-stat-value").TextContent.Replace(" ", string.Empty, StringComparison.Ordinal).Trim());
    }

    [Fact]
    public void StatUsesThresholdColorForValue()
    {
        var cut = Render<MusselStatView>(parameters => parameters
            .Add(static p => p.Value, 90)
            .Add(static p => p.Thresholds, Levels)
            .Add(static p => p.ColorMode, ChartStatColorMode.Value));

        Assert.Equal("color: #F2495C", cut.Find(".mussel-stat-value").GetAttribute("style"));
    }

    [Fact]
    public void StatGradientUsesLightTextAndBackgroundTrend()
    {
        var cut = Render<MusselStatView>(parameters => parameters
            .Add(static p => p.Value, 10)
            .Add(static p => p.Fill, "#1F60C4")
            .Add(static p => p.ColorMode, ChartStatColorMode.Background)
            .Add(static p => p.FillMode, ChartFillMode.Gradient)
            .Add(static p => p.TrendValues, [1d, 3, 2])
            .Add(static p => p.TrendPlacement, ChartTrendPlacement.Background));

        Assert.Contains("mussel-stat-light-text", cut.Find(".mussel-stat").ClassName, StringComparison.Ordinal);
        Assert.Equal(2, cut.FindAll("stop").Count);
        Assert.Equal("0 0 2 100", cut.Find(".mussel-stat-trend-background").GetAttribute("viewBox"));
    }

    [Fact]
    public void StatShowsEmptyTextForNaN()
    {
        var cut = Render<MusselStatView>(parameters => parameters
            .Add(static p => p.Value, Double.NaN)
            .Add(static p => p.Unit, "%")
            .Add(static p => p.EmptyText, "No data"));

        Assert.Equal("No data", cut.Find(".mussel-stat-value").TextContent.Trim());
        Assert.Contains("mussel-stat-empty", cut.Find(".mussel-stat").ClassName, StringComparison.Ordinal);
    }

    [Fact]
    public void StatKeepsUserClass()
    {
        var cut = Render<MusselStatView>(parameters => parameters
            .Add(static p => p.Value, 1)
            .AddUnmatched("class", "tile"));

        Assert.Equal("mussel-stat tile", cut.Find("div").ClassName);
    }

    [Fact]
    public void StatHorizontalLayoutPlacesValueBesideTitle()
    {
        var cut = Render<MusselStatView>(parameters => parameters
            .Add(static p => p.Title, "CPU")
            .Add(static p => p.Value, 1)
            .Add(static p => p.LayoutMode, ChartStatLayoutMode.Horizontal));

        Assert.Equal("mussel-stat mussel-stat-horizontal", cut.Find("div").ClassName);
        Assert.Equal(["mussel-stat-title", "mussel-stat-value"], cut.Find(".mussel-stat-header").Children.Select(static x => x.ClassName));
    }

    [Fact]
    public void GaugeDrawsRatioValueAndTarget()
    {
        var cut = Render<MusselBarGaugeView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("Shop", 30, null, 40, 35)])
            .Add(static p => p.ValueMode, ChartBarGaugeValueMode.ValueOfMaximum)
            .Add(static p => p.ValueFormat, "N0")
            .Add(static p => p.Culture, CultureInfo.InvariantCulture));

        Assert.Equal("Shop", cut.Find(".mussel-gauge-label").TextContent);
        Assert.Equal("75%", cut.Find(".mussel-gauge-bar > rect[fill-opacity='0.35']").GetAttribute("width"));
        Assert.Equal("87.5%", cut.Find(".mussel-gauge-target").ParentElement!.GetAttribute("x"));
        Assert.Equal("30 / 40", cut.Find(".mussel-gauge-value text").TextContent);
    }

    [Fact]
    public void SegmentedGaugeUsesThresholdStops()
    {
        var cut = Render<MusselBarGaugeView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("C:", 90)])
            .Add(static p => p.DisplayMode, ChartBarGaugeMode.Segmented)
            .Add(static p => p.Thresholds, Levels));

        var stops = cut.FindAll("stop").Select(static x => (x.GetAttribute("offset"), x.GetAttribute("stop-color"))).ToList();
        Assert.Equal([("0", "#73BF69"), ("0.7", "#73BF69"), ("0.7", "#FADE2A"), ("0.85", "#FADE2A"), ("0.85", "#F2495C"), ("1", "#F2495C")], stops);
        Assert.NotNull(cut.Find("mask"));
        Assert.Equal("#F2495C", cut.Find(".mussel-gauge-value text").GetAttribute("fill"));
    }

    [Fact]
    public void SparklineDrawsLineOrBars()
    {
        var line = Render<MusselSparklineView>(parameters => parameters
            .Add(static p => p.Values, [1d, 2, 3, 4]));
        var bars = Render<MusselSparklineView>(parameters => parameters
            .Add(static p => p.Values, [1d, -2, 3])
            .Add(static p => p.Kind, ChartSparklineKind.Bar));

        Assert.Equal(4, line.Find("polyline").GetAttribute("points")!.Split(' ').Length);
        Assert.Equal("0 0 3 100", line.Find("svg").GetAttribute("viewBox"));
        Assert.Equal(3, bars.FindAll("rect").Count);
    }

    [Fact]
    public void ColorShiftKeepsHexAndBrightness()
    {
        Assert.True(ChartColor.TryParse("#5794F2", out var color));
        Assert.Equal((0x57, 0x94, 0xF2), color);
        Assert.True(ChartColor.TryParse("#FFF", out var white));
        Assert.Equal((255, 255, 255), white);
        Assert.False(ChartColor.TryParse("blue", out _));
        Assert.Equal("#808080", ChartColor.Shift("#808080", 30, 0));
        Assert.StartsWith("#", ChartColor.Shift("#5794F2", 8, -15), StringComparison.Ordinal);
        Assert.Equal(255, ChartColor.Brightness("#FFFFFF"));
    }
}

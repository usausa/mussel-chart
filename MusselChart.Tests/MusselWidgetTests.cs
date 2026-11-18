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

    [Theory]
    [InlineData(1e17)]
    [InlineData(2e-323)]
    [InlineData(1.7e308)]
    [InlineData(-1.7e308)]
    [InlineData(Double.MaxValue)]
    public void AFlatTrendSitsInTheMiddle(double value)
    {
        var cut = Render<MusselStatView>(parameters => parameters
            .Add(static p => p.Value, 1d)
            .Add(p => p.TrendValues, [value, value, value]));

        Assert.Equal("0,50 1,50 2,50", cut.Find(".mussel-stat-trend-line").GetAttribute("points"));
    }

    [Theory]
    [InlineData(Double.NaN)]
    [InlineData(Double.PositiveInfinity)]
    [InlineData(Double.NegativeInfinity)]
    public void StatShowsEmptyTextWithoutAFiniteValue(double value)
    {
        var cut = Render<MusselStatView>(parameters => parameters
            .Add(static p => p.Value, value)
            .Add(static p => p.Unit, "%")
            .Add(static p => p.EmptyText, "No data")
            .Add(static p => p.Thresholds, Levels)
            .Add(static p => p.ColorMode, ChartStatColorMode.Value));

        Assert.Equal("No data", cut.Find(".mussel-stat-value").TextContent.Trim());
        Assert.Null(cut.Find(".mussel-stat-value").GetAttribute("style"));
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
    public void GaugeWithoutABaseStepStartsInItsOwnColor()
    {
        var cut = Render<MusselBarGaugeView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("C:", 50, "#5794F2")])
            .Add(static p => p.Thresholds, [new ChartThreshold { Value = 70, Level = ChartThresholdLevel.Warning }, new ChartThreshold { Value = 85, Level = ChartThresholdLevel.Critical }]));

        var stops = cut.FindAll("stop").Select(static x => (x.GetAttribute("offset"), x.GetAttribute("stop-color"))).ToList();
        Assert.Equal([("0", "#5794F2"), ("0.7", "#5794F2")], stops[..2]);
        Assert.Equal("#5794F2", cut.Find(".mussel-gauge-value text").GetAttribute("fill"));
    }

    [Fact]
    public void EachGaugeRowStartsInItsOwnColorAndKeepsItsIdsWhenTheThresholdsChange()
    {
        var cut = Render<MusselBarGaugeView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 50, "#5794F2", 100), new ChartEntry("B", 90, "#B877D9", 100)])
            .Add(static p => p.Thresholds, [new ChartThreshold { Value = 70, Level = ChartThresholdLevel.Warning }, new ChartThreshold { Value = 85, Level = ChartThresholdLevel.Critical }]));
        var ids = cut.FindAll("linearGradient").Select(static x => x.Id).ToList();

        Assert.Equal(["#5794F2", "#B877D9"], cut.FindAll("linearGradient").Select(static x => x.QuerySelector("stop")!.GetAttribute("stop-color")));

        cut.Render(parameters => parameters.Add(static p => p.Thresholds, [new ChartThreshold { Value = 40, Color = "#FF9830" }]));

        Assert.Equal(ids, cut.FindAll("linearGradient").Select(static x => x.Id));
        Assert.All(cut.FindAll("linearGradient"), static x => Assert.Equal("#FF9830", x.QuerySelectorAll("stop")[^1].GetAttribute("stop-color")));
        Assert.Equal(["#5794F2", "#B877D9"], cut.FindAll("linearGradient").Select(static x => x.QuerySelector("stop")!.GetAttribute("stop-color")));
    }

    [Fact]
    public async Task AStatWithoutATrendDoesNotFollowItsValues()
    {
        var buffer = new ChartRingBuffer<double>(10);
        buffer.Push(1);
        var cut = Render<MusselStatView>(parameters => parameters
            .Add(static p => p.Value, 1d)
            .Add(p => p.TrendValues, buffer)
            .Add(static p => p.TrendPlacement, ChartTrendPlacement.None));
        var renders = cut.RenderCount;

        await cut.InvokeAsync(() => buffer.Push(2));
        await cut.InvokeAsync(static () => { });

        Assert.Equal(renders, cut.RenderCount);
        Assert.Empty(cut.FindAll(".mussel-stat-trend-line"));
    }

    [Fact]
    public void ValuesThatAreNotAListAreReadAgainOnEachRender()
    {
        var cut = Render<MusselSparklineView>(parameters => parameters.Add(static p => p.Values, Enumerable.Range(1, 3).Select(static x => (double)x)));

        cut.Render();

        Assert.Equal(3, cut.Find("polyline").GetAttribute("points")!.Split(' ').Length);
    }

    [Fact]
    public void SparklinesOfTheWidestValuesStayFinite()
    {
        var cut = Render<MusselSparklineView>(parameters => parameters.Add(static p => p.Values, new[] { 1e308, -1e308, 0 }));

        Assert.DoesNotContain("NaN", cut.Find("polyline").GetAttribute("points"), StringComparison.Ordinal);
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
    public void MissingValuesAreShownAsNoValue()
    {
        var stat = Render<MusselStatView>(parameters => parameters
            .Add(static p => p.Value, Double.NaN)
            .Add(static p => p.Thresholds, Levels)
            .Add(static p => p.ColorMode, ChartStatColorMode.Value));
        var gauge = Render<MusselBarGaugeView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", Double.NaN, "#5794F2", 100), new ChartEntry("B", Double.PositiveInfinity, "#5794F2", 100)])
            .Add(static p => p.Thresholds, Levels)
            .Add(static p => p.ValueMode, ChartBarGaugeValueMode.ValueOfMaximum));
        var area = Render<MusselSparklineView>(parameters => parameters
            .Add(static p => p.Values, [Double.NaN, Double.NaN, 3, 4, Double.NaN])
            .Add(static p => p.Kind, ChartSparklineKind.Area));

        Assert.Null(stat.Find(".mussel-stat-value").GetAttribute("style"));
        Assert.Equal(["-", "-"], gauge.FindAll(".mussel-gauge-value text").Select(static x => x.TextContent));
        Assert.Equal(["#5794F2", "#5794F2"], gauge.FindAll(".mussel-gauge-value text").Select(static x => x.GetAttribute("fill")));
        Assert.Equal(["0%", "0%"], gauge.FindAll(".mussel-gauge-bar > rect[fill-opacity='0.35']").Select(static x => x.GetAttribute("width")));
        var points = area.Find("polygon").GetAttribute("points")!.Split(' ');
        Assert.StartsWith("2,", points[0], StringComparison.Ordinal);
        Assert.StartsWith("3,", points[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void SparklineSwapsFixedEndsGivenTheWrongWayRound()
    {
        var reversed = Render<MusselSparklineView>(parameters => parameters
            .Add(static p => p.Values, [1d, 5, 3])
            .Add(static p => p.Minimum, 10)
            .Add(static p => p.Maximum, 0));
        var ordered = Render<MusselSparklineView>(parameters => parameters
            .Add(static p => p.Values, [1d, 5, 3])
            .Add(static p => p.Minimum, 0)
            .Add(static p => p.Maximum, 10));

        Assert.Equal(ordered.Find("polyline").GetAttribute("points"), reversed.Find("polyline").GetAttribute("points"));
    }

    [Fact]
    public void SparklineKeepsTheFixedEndWhenAllValuesAreOutside()
    {
        var below = Render<MusselSparklineView>(parameters => parameters
            .Add(static p => p.Values, [-5d, -3, -4])
            .Add(static p => p.Minimum, 0d));
        var above = Render<MusselSparklineView>(parameters => parameters
            .Add(static p => p.Values, [102d, 105])
            .Add(static p => p.Maximum, 100d));

        Assert.Equal("0,96 1,96 2,96", below.Find("polyline").GetAttribute("points"));
        Assert.Equal("0,4 1,4", above.Find("polyline").GetAttribute("points"));
    }

    [Fact]
    public void EightDigitColorsEndWithTheAlpha()
    {
        Assert.True(ChartColor.TryParse("#5794F2CC", out var color));
        Assert.Equal((0x57, 0x94, 0xF2), color);
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

    [Fact]
    public void ARingBufferIsCopiedBeforeTheBarGaugeReadsIt()
    {
        var buffer = new ChartRingBuffer<ChartEntry>(4);
        buffer.Push(new ChartEntry("A", 30, null, 100));
        buffer.Push(new ChartEntry("B", 50, null, 100));
        var seen = new HashSet<double>();

        Render<MusselBarGaugeView>(parameters => parameters
            .Add(static p => p.Items, buffer)
            .Add(static p => p.ValueMode, ChartBarGaugeValueMode.Value)
            .Add(p => p.ValueFormatter, x =>
            {
                seen.Add(x);
                buffer.Clear();
                return "v";
            }));

        Assert.Contains(50, seen);
    }
}

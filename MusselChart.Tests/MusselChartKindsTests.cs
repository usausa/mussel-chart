namespace MusselChart;

public sealed record CategoryItem(string Name, double Value, double Other = 0);

public sealed record PointItem(double X, double Y, double Size = 0);

public sealed class MusselChartKindsTests : BunitContext
{
    private static readonly CategoryItem[] Items =
    [
        new("A", 10, 30),
        new("B", 20, 20),
        new("C", 30, 10)
    ];

    public MusselChartKindsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void CategoriesKeepTheirOrderAcrossSeries()
    {
        CategoryItem[] second = [new("B", 1), new("D", 2)];

        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.Culture, CultureInfo.InvariantCulture)
            .AddChildContent<BarSeries<CategoryItem>>(series => series
                .Add(static p => p.Items, Items)
                .Add(static p => p.Category, static x => x.Name)
                .Add(static p => p.Y, static x => x.Value))
            .AddChildContent<LineSeries<CategoryItem>>(series => series
                .Add(static p => p.Items, second)
                .Add(static p => p.Category, static x => x.Name)
                .Add(static p => p.Y, static x => x.Value)));

        var root = cut.Find(".mussel-chart");
        Assert.Equal("category", root.GetAttribute("data-x-kind"));
        Assert.Equal("[\"A\",\"B\",\"C\",\"D\"]", root.GetAttribute("data-categories"));
        Assert.Equal(["0", "1", "2"], cut.FindAll(".mussel-bars rect").Select(static x => x.GetAttribute("data-x")));
        Assert.Equal("1,1 3,2", cut.Find("polyline").GetAttribute("points"));
        Assert.Contains("D", cut.FindAll(".mussel-axis text").Select(static x => x.TextContent));
    }

    [Fact]
    public void BarsOfDifferentSeriesSitSideBySide()
    {
        var cut = Render<MusselChartView>(parameters => parameters
            .AddChildContent<BarSeries<CategoryItem>>(series => series
                .Add(static p => p.Items, Items)
                .Add(static p => p.Category, static x => x.Name)
                .Add(static p => p.Y, static x => x.Value))
            .AddChildContent<BarSeries<CategoryItem>>(series => series
                .Add(static p => p.Items, Items)
                .Add(static p => p.Category, static x => x.Name)
                .Add(static p => p.Y, static x => x.Other)));

        var bars = cut.FindAll(".mussel-bars rect");
        var left = Double.Parse(bars[0].GetAttribute("x")!, CultureInfo.InvariantCulture);
        var width = Double.Parse(bars[0].GetAttribute("width")!, CultureInfo.InvariantCulture);
        var right = Double.Parse(bars[3].GetAttribute("x")!, CultureInfo.InvariantCulture);
        Assert.True(left < 0);
        Assert.True(right > left + width - 0.001);
        Assert.Equal(0, (left + right + width) / 2, 2);
    }

    [Fact]
    public void BarsOfOneGroupStackAndLeaveAGap()
    {
        var cut = Render<MusselChartView>(parameters => parameters
            .AddChildContent<NumericAxis>(axis => axis.Add(static p => p.Minimum, 0).Add(static p => p.Maximum, 40))
            .AddChildContent<BarSeries<CategoryItem>>(series => series
                .Add(static p => p.Items, Items)
                .Add(static p => p.Category, static x => x.Name)
                .Add(static p => p.Y, static x => x.Value)
                .Add(static p => p.StackGroup, "total"))
            .AddChildContent<BarSeries<CategoryItem>>(series => series
                .Add(static p => p.Items, Items)
                .Add(static p => p.Category, static x => x.Name)
                .Add(static p => p.Y, static x => x.Other)
                .Add(static p => p.StackGroup, "total")));

        var bars = cut.FindAll(".mussel-bars rect");
        Assert.Equal(6, bars.Count);
        Assert.Equal(bars[0].GetAttribute("x"), bars[3].GetAttribute("x"));
        Assert.Equal("0", bars[0].GetAttribute("y"));
        Assert.True(Double.Parse(bars[0].GetAttribute("height")!, CultureInfo.InvariantCulture) < 10);
        Assert.True(Double.Parse(bars[3].GetAttribute("y")!, CultureInfo.InvariantCulture) > 10);
        Assert.Equal("30", bars[3].GetAttribute("data-v"));
    }

    [Fact]
    public void NegativeValuesStackDownFromZero()
    {
        CategoryItem[] items = [new("A", 10, -5)];

        var cut = Render<MusselChartView>(parameters => parameters
            .AddChildContent<BarSeries<CategoryItem>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.Category, static x => x.Name)
                .Add(static p => p.Y, static x => x.Value)
                .Add(static p => p.StackGroup, "total"))
            .AddChildContent<BarSeries<CategoryItem>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.Category, static x => x.Name)
                .Add(static p => p.Y, static x => x.Other)
                .Add(static p => p.StackGroup, "total")));

        var bars = cut.FindAll(".mussel-bars rect");
        Assert.Equal("0", bars[0].GetAttribute("y"));
        Assert.Equal("10", bars[0].GetAttribute("height"));
        Assert.Equal("-5", bars[1].GetAttribute("y"));
        Assert.Equal("5", bars[1].GetAttribute("height"));
    }

    [Fact]
    public void PercentStacksFillTheAxisAndKeepTheValues()
    {
        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.Culture, CultureInfo.InvariantCulture)
            .Add(static p => p.StackMode, ChartStackMode.Percent)
            .AddChildContent<BarSeries<CategoryItem>>(series => series
                .Add(static p => p.Items, Items)
                .Add(static p => p.Category, static x => x.Name)
                .Add(static p => p.Y, static x => x.Value)
                .Add(static p => p.StackGroup, "total"))
            .AddChildContent<BarSeries<CategoryItem>>(series => series
                .Add(static p => p.Items, Items)
                .Add(static p => p.Category, static x => x.Name)
                .Add(static p => p.Y, static x => x.Other)
                .Add(static p => p.StackGroup, "total")));

        var bars = cut.FindAll(".mussel-bars rect");
        Assert.InRange(Double.Parse(bars[0].GetAttribute("height")!, CultureInfo.InvariantCulture), 24, 25);
        Assert.Equal("10", bars[0].GetAttribute("data-v"));
        var top = Double.Parse(bars[3].GetAttribute("y")!, CultureInfo.InvariantCulture) + Double.Parse(bars[3].GetAttribute("height")!, CultureInfo.InvariantCulture);
        Assert.Equal(100, top, 3);
        var labels = cut.FindAll(".mussel-axis text").Select(static x => x.TextContent).ToList();
        Assert.Contains(labels, static x => x.StartsWith("100", StringComparison.Ordinal) && x.EndsWith('%'));
    }

    [Fact]
    public void AreasOfAPercentStackCarryTheirValues()
    {
        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.StackMode, ChartStackMode.Percent)
            .AddChildContent<AreaSeries<CategoryItem>>(series => series
                .Add(static p => p.Items, Items)
                .Add(static p => p.Category, static x => x.Name)
                .Add(static p => p.Y, static x => x.Value)
                .Add(static p => p.StackGroup, "total"))
            .AddChildContent<AreaSeries<CategoryItem>>(series => series
                .Add(static p => p.Items, Items)
                .Add(static p => p.Category, static x => x.Name)
                .Add(static p => p.Y, static x => x.Other)
                .Add(static p => p.StackGroup, "total")));

        var lines = cut.FindAll("polyline");
        Assert.Equal("0,25 1,50 2,75", lines[0].GetAttribute("points"));
        Assert.Equal("10 20 30", lines[0].GetAttribute("data-values"));
        Assert.Equal("0,100 1,100 2,100", lines[1].GetAttribute("points"));
    }

    [Fact]
    public void HorizontalChartSwapsTheAxes()
    {
        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.Orientation, ChartOrientation.Horizontal)
            .AddChildContent<NumericAxis>(axis => axis.Add(static p => p.Minimum, 0).Add(static p => p.Maximum, 30))
            .AddChildContent<BarSeries<CategoryItem>>(series => series
                .Add(static p => p.Items, Items)
                .Add(static p => p.Category, static x => x.Name)
                .Add(static p => p.Y, static x => x.Value)));

        Assert.Equal("horizontal", cut.Find(".mussel-chart").GetAttribute("data-orientation"));
        Assert.StartsWith("matrix(0 ", cut.Find(".mussel-plot").GetAttribute("transform"), StringComparison.Ordinal);
        var names = cut.FindAll(".mussel-axis text").Where(static x => x.GetAttribute("text-anchor") == "end").Select(static x => x.TextContent);
        Assert.Equal(["A", "B", "C"], names);
        var values = cut.FindAll(".mussel-axis text").Where(static x => x.GetAttribute("text-anchor") == "middle").Select(static x => x.TextContent).ToList();
        Assert.Contains("30", values);
        Assert.Contains(cut.FindAll(".mussel-grid line"), static x => x.GetAttribute("x1") == x.GetAttribute("x2"));
    }

    [Fact]
    public void RangeBarsSpanFromLowToHighWithoutZero()
    {
        CategoryItem[] items = [new("A", 10, 14), new("B", 22, 16)];

        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.Culture, CultureInfo.InvariantCulture)
            .AddChildContent<RangeBarSeries<CategoryItem>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.Category, static x => x.Name)
                .Add(static p => p.Low, static x => x.Value)
                .Add(static p => p.High, static x => x.Other)));

        var bars = cut.FindAll(".mussel-bars rect");
        Assert.Equal("10", bars[0].GetAttribute("y"));
        Assert.Equal("4", bars[0].GetAttribute("height"));
        Assert.Equal("16", bars[1].GetAttribute("y"));
        Assert.Equal("6", bars[1].GetAttribute("height"));
        Assert.DoesNotContain("0", cut.FindAll(".mussel-axis text").Select(static x => x.TextContent));
    }

    [Fact]
    public void RangeBarWithoutBothValuesIsSkipped()
    {
        CategoryItem[] items = [new("A", Double.NaN, 14), new("B", 22, 16)];

        var cut = Render<MusselChartView>(parameters => parameters
            .AddChildContent<RangeBarSeries<CategoryItem>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.Category, static x => x.Name)
                .Add(static p => p.Low, static x => x.Value)
                .Add(static p => p.High, static x => x.Other)));

        Assert.Single(cut.FindAll(".mussel-bars rect"));
    }

    [Fact]
    public void ScatterDrawsMarkersOnANumericAxis()
    {
        PointItem[] items = [new(0, 0), new(10, 100), new(20, 50)];

        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.Culture, CultureInfo.InvariantCulture)
            .AddChildContent<NumericXAxis>(axis => axis.Add(static p => p.Minimum, 0).Add(static p => p.Maximum, 20))
            .AddChildContent<NumericAxis>(axis => axis.Add(static p => p.Minimum, 0).Add(static p => p.Maximum, 100))
            .AddChildContent<ScatterSeries<PointItem>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.XValue, static x => x.X)
                .Add(static p => p.Y, static x => x.Y)
                .Add(static p => p.MarkerSize, 10)));

        Assert.Equal("number", cut.Find(".mussel-chart").GetAttribute("data-x-kind"));
        Assert.Empty(cut.FindAll(".mussel-stroke"));
        var markers = cut.FindAll(".mussel-markers circle");
        Assert.Equal(3, markers.Count);
        Assert.Equal("5", markers[0].GetAttribute("r"));
        Assert.Equal("10", markers[1].GetAttribute("data-x"));
        Assert.Equal("100", markers[1].GetAttribute("data-y"));
        Assert.True(Double.Parse(markers[0].GetAttribute("cy")!, CultureInfo.InvariantCulture) > Double.Parse(markers[1].GetAttribute("cy")!, CultureInfo.InvariantCulture));
        Assert.Contains("20", cut.FindAll(".mussel-axis text").Select(static x => x.TextContent));
    }

    [Theory]
    [InlineData(ChartMarkerShape.Square)]
    [InlineData(ChartMarkerShape.Diamond)]
    public void ScatterShapesAreDrawnAsPaths(ChartMarkerShape shape)
    {
        PointItem[] items = [new(1, 1), new(2, 2)];

        var cut = Render<MusselChartView>(parameters => parameters
            .AddChildContent<ScatterSeries<PointItem>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.XValue, static x => x.X)
                .Add(static p => p.Y, static x => x.Y)
                .Add(static p => p.MarkerShape, shape)));

        Assert.Empty(cut.FindAll(".mussel-markers circle"));
        Assert.Equal(2, cut.FindAll(".mussel-markers path").Count);
    }

    [Fact]
    public void BubblesGrowByAreaAndLargerOnesComeFirst()
    {
        PointItem[] items = [new(1, 1, 25), new(2, 2, 100), new(3, 3, 0)];

        var cut = Render<MusselChartView>(parameters => parameters
            .AddChildContent<BubbleSeries<PointItem>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.XValue, static x => x.X)
                .Add(static p => p.Y, static x => x.Y)
                .Add(static p => p.Size, static x => x.Size)
                .Add(static p => p.MinBubbleSize, 0)
                .Add(static p => p.MaxBubbleSize, 40)));

        var markers = cut.FindAll(".mussel-markers circle");
        Assert.Equal(["100", "25", "0"], markers.Select(static x => x.GetAttribute("data-size")));
        Assert.Equal(["20", "10", "0"], markers.Select(static x => x.GetAttribute("r")));
    }

    [Fact]
    public void BubblesCanSizeFromTheSmallestValue()
    {
        PointItem[] items = [new(1, 1, 10), new(2, 2, 20)];

        var cut = Render<MusselChartView>(parameters => parameters
            .AddChildContent<BubbleSeries<PointItem>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.XValue, static x => x.X)
                .Add(static p => p.Y, static x => x.Y)
                .Add(static p => p.Size, static x => x.Size)
                .Add(static p => p.MinBubbleSize, 8)
                .Add(static p => p.MaxBubbleSize, 40)
                .Add(static p => p.SizeIncludesZero, false)));

        Assert.Equal(["20", "4"], cut.FindAll(".mussel-markers circle").Select(static x => x.GetAttribute("r")));
    }

    [Fact]
    public void BubblesWithoutASizeAreSkipped()
    {
        PointItem[] items = [new(1, 1, Double.NaN), new(2, 2, 20)];

        var cut = Render<MusselChartView>(parameters => parameters
            .AddChildContent<BubbleSeries<PointItem>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.XValue, static x => x.X)
                .Add(static p => p.Y, static x => x.Y)
                .Add(static p => p.Size, static x => x.Size)));

        Assert.Single(cut.FindAll(".mussel-markers circle"));
    }

    [Fact]
    public void BarsOnANumericAxisKeepTheEndsInsideThePlot()
    {
        PointItem[] items = [new(0, 1), new(10, 2), new(20, 3)];

        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.Culture, CultureInfo.InvariantCulture)
            .AddChildContent<BarSeries<PointItem>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.XValue, static x => x.X)
                .Add(static p => p.Y, static x => x.Y)));

        var labels = cut.FindAll(".mussel-axis text").Where(static x => x.GetAttribute("text-anchor") == "middle").Select(static x => x.TextContent).ToList();
        Assert.Equal(3, cut.FindAll(".mussel-bars rect").Count);
        Assert.Contains("0", labels);
        Assert.Contains("20", labels);
    }

    [Fact]
    public void LegendUsesDotsExceptForLines()
    {
        var cut = Render<MusselChartView>(parameters => parameters
            .AddChildContent<BarSeries<CategoryItem>>(series => series
                .Add(static p => p.Items, Items)
                .Add(static p => p.Category, static x => x.Name)
                .Add(static p => p.Y, static x => x.Value)
                .Add(static p => p.Title, "Bar"))
            .AddChildContent<LineSeries<CategoryItem>>(series => series
                .Add(static p => p.Items, Items)
                .Add(static p => p.Category, static x => x.Name)
                .Add(static p => p.Y, static x => x.Other)
                .Add(static p => p.Title, "Line")));

        var markers = cut.FindAll(".mussel-legend-marker");
        Assert.NotNull(markers[0].QuerySelector("circle"));
        Assert.NotNull(markers[1].QuerySelector("rect"));
    }

    [Fact]
    public void CategoryLabelsAreCutToTheMaximumLength()
    {
        CategoryItem[] items = [new("Yokohama Station", 1), new("Ueno", 2)];

        var cut = Render<MusselChartView>(parameters => parameters
            .AddChildContent<CategoryAxis>(axis => axis.Add(static p => p.LabelMaxLength, 8))
            .AddChildContent<BarSeries<CategoryItem>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.Category, static x => x.Name)
                .Add(static p => p.Y, static x => x.Value)));

        var labels = cut.FindAll(".mussel-axis text").Select(static x => x.TextContent).ToList();
        Assert.Contains("Yokohama…", labels);
        Assert.Contains("Ueno", labels);
    }

    [Fact]
    public void CategoriesOnTicksTouchThePlotEdges()
    {
        var cut = Render<MusselChartView>(parameters => parameters
            .AddChildContent<CategoryAxis>(axis => axis.Add(static p => p.LabelPlacement, ChartLabelPlacement.OnTicks))
            .AddChildContent<LineSeries<CategoryItem>>(series => series
                .Add(static p => p.Items, Items)
                .Add(static p => p.Category, static x => x.Name)
                .Add(static p => p.Y, static x => x.Value)));

        var left = cut.Find(".mussel-grid line").GetAttribute("x1");
        var first = cut.FindAll(".mussel-axis text").First(static x => x.TextContent == "A");
        Assert.Equal(left, first.GetAttribute("x"));
    }

    [Fact]
    public void RebuildsWhenTheCategoriesChange()
    {
        var items = new List<CategoryItem>(Items);
        var cut = Render<MusselChartView>(parameters => parameters
            .AddChildContent<BarSeries<CategoryItem>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.Category, static x => x.Name)
                .Add(static p => p.Y, static x => x.Value)));

        items.Clear();
        items.Add(new CategoryItem("Z", 1));
        cut.Render();

        Assert.Equal("[\"Z\"]", cut.Find(".mussel-chart").GetAttribute("data-categories"));
        Assert.Single(cut.FindAll(".mussel-bars rect"));
    }

    [Fact]
    public void ThresholdsFollowTheValueAxisOfAHorizontalChart()
    {
        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.Orientation, ChartOrientation.Horizontal)
            .Add(static p => p.Thresholds, [new ChartThreshold { Value = 15, Level = ChartThresholdLevel.Critical, Label = "Limit" }])
            .Add(static p => p.ThresholdsMode, ChartThresholdsMode.Lines)
            .AddChildContent<NumericAxis>(axis => axis.Add(static p => p.Minimum, 0).Add(static p => p.Maximum, 30))
            .AddChildContent<BarSeries<CategoryItem>>(series => series
                .Add(static p => p.Items, Items)
                .Add(static p => p.Category, static x => x.Name)
                .Add(static p => p.Y, static x => x.Value)));

        var line = cut.Find(".mussel-threshold");
        Assert.Equal(line.GetAttribute("x1"), line.GetAttribute("x2"));
    }
}

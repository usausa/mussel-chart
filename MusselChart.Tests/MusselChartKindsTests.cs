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
        Assert.Equal(["0", "1", "2"], cut.FindAll(".mussel-bars rect").Select(static x => x.GetAttribute("x")));
        Assert.Equal("1,0 3,1", cut.Find("polyline").GetAttribute("points"));
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

        var groups = cut.FindAll(".mussel-bars");
        var left = Double.Parse(groups[0].GetAttribute("data-offset")!, CultureInfo.InvariantCulture);
        var right = Double.Parse(groups[1].GetAttribute("data-offset")!, CultureInfo.InvariantCulture);
        var bars = cut.FindAll(".mussel-bars rect");
        var width = Double.Parse(bars[0].GetAttribute("width")!, CultureInfo.InvariantCulture);
        Assert.Equal(bars[0].GetAttribute("x"), bars[3].GetAttribute("x"));
        Assert.True(left < 0);
        Assert.Equal(-left, right, 4);
        Assert.True(right - left >= width);
        Assert.StartsWith("translate(-", groups[0].GetAttribute("transform"), StringComparison.Ordinal);
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
        var groups = cut.FindAll(".mussel-bars");
        Assert.Equal(groups[0].GetAttribute("transform"), groups[1].GetAttribute("transform"));
        Assert.Equal("10", cut.Find("svg.mussel-chart-svg").GetAttribute("data-value-base"));
        Assert.Equal("-10", bars[0].GetAttribute("y"));
        Assert.True(Double.Parse(bars[0].GetAttribute("height")!, CultureInfo.InvariantCulture) < 10);
        Assert.True(Double.Parse(bars[3].GetAttribute("y")!, CultureInfo.InvariantCulture) > 0);
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
        Assert.Equal("-5", cut.Find("svg.mussel-chart-svg").GetAttribute("data-value-base"));
        Assert.Equal("5", bars[0].GetAttribute("y"));
        Assert.Equal("10", bars[0].GetAttribute("height"));
        Assert.Equal("0", bars[1].GetAttribute("y"));
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
        Assert.Contains(labels, static x => x.StartsWith("100", StringComparison.Ordinal) && x.EndsWith('%', StringComparison.Ordinal));
    }

    [Fact]
    public void TooltipsOfAPercentStackDoNotTakeTheFormatOfTheAxis()
    {
        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.Culture, CultureInfo.InvariantCulture)
            .Add(static p => p.StackMode, ChartStackMode.Percent)
            .AddChildContent<NumericAxis>(axis => axis.Add(static p => p.LabelFormat, "{0:N0} %"))
            .AddChildContent<BarSeries<CategoryItem>>(series => series
                .Add(static p => p.Items, Items)
                .Add(static p => p.Category, static x => x.Name)
                .Add(static p => p.Y, static x => x.Value)
                .Add(static p => p.StackGroup, "total")));

        Assert.Equal(string.Empty, cut.Find(".mussel-series").GetAttribute("data-format"));
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
    public void RangeBarsOnAFixedValueAxisAreNotCut()
    {
        CategoryItem[] items = [new("A", -1000, 150)];

        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.Culture, CultureInfo.InvariantCulture)
            .AddChildContent<NumericAxis>(static axis => axis.Add(static p => p.Minimum, 100).Add(static p => p.Maximum, 200))
            .AddChildContent<RangeBarSeries<CategoryItem>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.Category, static x => x.Name)
                .Add(static p => p.Low, static x => x.Value)
                .Add(static p => p.High, static x => x.Other)));

        var valueBase = Double.Parse(cut.Find("svg.mussel-chart-svg").GetAttribute("data-value-base")!, CultureInfo.InvariantCulture);
        Assert.Equal(-1000 - valueBase, Double.Parse(cut.Find(".mussel-bars rect").GetAttribute("y")!, CultureInfo.InvariantCulture), 3);
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
        Assert.Equal("10", cut.Find("svg.mussel-chart-svg").GetAttribute("data-value-base"));
        Assert.Equal("0", bars[0].GetAttribute("y"));
        Assert.Equal("4", bars[0].GetAttribute("height"));
        Assert.Equal("6", bars[1].GetAttribute("y"));
        Assert.Equal("6", bars[1].GetAttribute("height"));
        Assert.DoesNotContain("0", cut.FindAll(".mussel-axis text").Select(static x => x.TextContent));
    }

    [Fact]
    public void RangeBarsKeepNineDigitsOfBothEndsForTheTooltip()
    {
        CategoryItem[] items = [new("A", 12345.67, 12350.12), new("B", -5, -10)];

        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.Culture, CultureInfo.InvariantCulture)
            .AddChildContent<RangeBarSeries<CategoryItem>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.Category, static x => x.Name)
                .Add(static p => p.Low, static x => x.Value)
                .Add(static p => p.High, static x => x.Other)));

        var bars = cut.FindAll(".mussel-bars rect");
        Assert.Equal("RangeBar", cut.Find(".mussel-series").GetAttribute("data-kind"));
        Assert.Equal("-10", cut.Find("svg.mussel-chart-svg").GetAttribute("data-value-base"));
        Assert.Equal(["12355.67", "0"], bars.Select(static x => x.GetAttribute("y")));
        Assert.Equal(["12350.12", "-5"], bars.Select(static x => x.GetAttribute("data-v")));
    }

    [Fact]
    public void RangeBarsOfTenDigitsKeepEveryDigitOfTheTop()
    {
        CategoryItem[] items = [new("A", 1234567880, 1234567955)];

        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.Culture, CultureInfo.InvariantCulture)
            .AddChildContent<RangeBarSeries<CategoryItem>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.Category, static x => x.Name)
                .Add(static p => p.Low, static x => x.Value)
                .Add(static p => p.High, static x => x.Other)));

        var bar = cut.Find(".mussel-bars rect");
        Assert.Equal("1234567880", cut.Find("svg.mussel-chart-svg").GetAttribute("data-value-base"));
        Assert.Equal(["0", "75"], [bar.GetAttribute("y"), bar.GetAttribute("height")]);
        Assert.Equal("1234567955", bar.GetAttribute("data-v"));
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
    [InlineData(10d, 5d)]
    [InlineData(3d, 3d)]
    [InlineData(Double.NaN, null)]
    [InlineData(Double.PositiveInfinity, null)]
    [InlineData(null, Double.NegativeInfinity)]
    public void TheValueAxisTakesThePointsOfTheRangeThatANumericXAxisShows(double? minimum, double? maximum)
    {
        var ends = RenderScatterWithXEnds(minimum, maximum);
        var plain = RenderScatterWithXEnds(null, null);

        Assert.DoesNotContain("NaN", ends.Markup, StringComparison.Ordinal);
        Assert.Equal(plain.Find(".mussel-plot").GetAttribute("transform"), ends.Find(".mussel-plot").GetAttribute("transform"));
        Assert.Equal(plain.FindAll(".mussel-axis text").Select(static x => x.TextContent), ends.FindAll(".mussel-axis text").Select(static x => x.TextContent));
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
        var paths = cut.FindAll(".mussel-markers path");
        Assert.Equal(2, paths.Count);
        Assert.All(paths, static x => Assert.False(String.IsNullOrEmpty(x.GetAttribute("data-x")) || String.IsNullOrEmpty(x.GetAttribute("data-y"))));
    }

    [Fact]
    public void AScatterSeriesTellsTheScriptItsTitleColorKindAndFormat()
    {
        PointItem[] items = [new(1, 1), new(2, 2)];

        var cut = Render<MusselChartView>(parameters => parameters
            .AddChildContent<ScatterSeries<PointItem>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.XValue, static x => x.X)
                .Add(static p => p.Y, static x => x.Y)
                .Add(static p => p.Title, "Points")
                .Add(static p => p.Color, "#123456")
                .Add(static p => p.ValueFormat, "N1")));

        var group = cut.Find(".mussel-series");
        Assert.Equal(["Points", "#123456", "Scatter", "N1"], [group.GetAttribute("data-title"), group.GetAttribute("data-color"), group.GetAttribute("data-kind"), group.GetAttribute("data-format")]);
    }

    [Fact]
    public void SquareBubblesTellTheScriptTheirSize()
    {
        PointItem[] items = [new(1, 1, 25), new(2, 2, 100)];

        var cut = Render<MusselChartView>(parameters => parameters
            .AddChildContent<BubbleSeries<PointItem>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.XValue, static x => x.X)
                .Add(static p => p.Y, static x => x.Y)
                .Add(static p => p.Size, static x => x.Size)
                .Add(static p => p.MarkerShape, ChartMarkerShape.Square)));

        Assert.Equal(["100", "25"], cut.FindAll(".mussel-markers path").Select(static x => x.GetAttribute("data-size")));
    }

    [Fact]
    public void BubblesGrowByAreaAndLargerOnesComeFirst()
    {
        PointItem[] items = [new(1, 1, 25), new(2, 2, 100), new(3, 3)];

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
    public void TheOldestBubbleLeavesTheBufferWhateverItsSize()
    {
        var buffer = new ChartRingBuffer<PointItem>(3);
        buffer.Push(new PointItem(1, 1, 100));
        buffer.Push(new PointItem(2, 2, 25));
        buffer.Push(new PointItem(3, 3, 50));
        var cut = Render<MusselChartView>(parameters => parameters
            .AddChildContent<BubbleSeries<PointItem>>(series => series
                .Add(static p => p.Items, buffer)
                .Add(static p => p.XValue, static x => x.X)
                .Add(static p => p.Y, static x => x.Y)
                .Add(static p => p.Size, static x => x.Size)));
        Assert.Equal(["100", "50", "25"], cut.FindAll(".mussel-markers circle").Select(static x => x.GetAttribute("data-size")));

        buffer.Push(new PointItem(4, 4, 75));
        cut.Render();

        Assert.Equal(["75", "50", "25"], cut.FindAll(".mussel-markers circle").Select(static x => x.GetAttribute("data-size")));
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
    public void ABubbleJustBeyondAFixedEndIsDrawnWhereItReachesThePlot()
    {
        var cut = RenderBubblesWithXEnds([new PointItem(0, 5, 100), new PointItem(10.1, 5, 100)]);

        var markers = cut.FindAll(".mussel-markers circle");
        Assert.Equal(["0", "10.1"], markers.Select(static x => x.GetAttribute("data-x")));
        Assert.Equal([null, "true"], markers.Select(static x => x.GetAttribute("data-outside")));
    }

    [Fact]
    public void ABubbleWhollyBeyondAFixedEndIsNotDrawn()
    {
        var cut = RenderBubblesWithXEnds([new PointItem(0, 5, 100), new PointItem(12, 5, 100)]);

        Assert.Equal(["0"], cut.FindAll(".mussel-markers circle").Select(static x => x.GetAttribute("data-x")));
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

    private IRenderedComponent<MusselChartView> RenderScatterWithXEnds(double? minimum, double? maximum) =>
        Render<MusselChartView>(parameters => parameters
            .Add(static p => p.Culture, CultureInfo.InvariantCulture)
            .AddChildContent<NumericXAxis>(axis => axis.Add(static p => p.Minimum, minimum).Add(static p => p.Maximum, maximum))
            .AddChildContent<ScatterSeries<PointItem>>(series => series
                .Add(static p => p.Items, [new PointItem(1, 50), new PointItem(3, 80), new PointItem(5, 100)])
                .Add(static p => p.XValue, static x => x.X)
                .Add(static p => p.Y, static x => x.Y)));

    private IRenderedComponent<MusselChartView> RenderBubblesWithXEnds(PointItem[] items) =>
        Render<MusselChartView>(parameters => parameters
            .Add(static p => p.Culture, CultureInfo.InvariantCulture)
            .AddChildContent<NumericXAxis>(static axis => axis.Add(static p => p.Minimum, 0).Add(static p => p.Maximum, 10))
            .AddChildContent<BubbleSeries<PointItem>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.XValue, static x => x.X)
                .Add(static p => p.Y, static x => x.Y)
                .Add(static p => p.Size, static x => x.Size)));
}

namespace MusselChart;

public sealed record Sample(DateTime Time, double Value, double Other = 0);

public sealed class MusselChartViewTests : BunitContext
{
    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public MusselChartViewTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void RendersLineInChunksOf32Points()
    {
        var buffer = CreateBuffer(100, 70);

        var cut = RenderLine(buffer);

        var lines = cut.FindAll("polyline");
        Assert.Equal(3, lines.Count);
        Assert.Equal(32, lines[0].GetAttribute("points")!.Split(' ').Length);
        Assert.Equal(8, lines[2].GetAttribute("points")!.Split(' ').Length);
    }

    [Fact]
    public void AppendChangesOnlyLastChunkAndTransform()
    {
        var buffer = CreateBuffer(100, 40);
        var cut = RenderLine(buffer);
        var first = cut.FindAll("polyline")[0].GetAttribute("points");
        var transform = cut.Find(".mussel-plot").GetAttribute("transform");

        buffer.Push(new Sample(Start.AddSeconds(40), 5));
        cut.Render();

        var lines = cut.FindAll("polyline");
        Assert.Equal(first, lines[0].GetAttribute("points"));
        Assert.EndsWith("40,5", lines[^1].GetAttribute("points"), StringComparison.Ordinal);
        Assert.NotEqual(transform, cut.Find(".mussel-plot").GetAttribute("transform"));
    }

    [Fact]
    public void DropsChunksThatLeaveTheBuffer()
    {
        var buffer = CreateBuffer(40, 40);
        var cut = RenderLine(buffer);
        Assert.Equal(2, cut.FindAll("polyline").Count);

        for (var i = 40; i < 80; i++)
        {
            buffer.Push(new Sample(Start.AddSeconds(i), i % 7));
        }

        cut.Render();

        var points = cut.FindAll("polyline").SelectMany(static x => x.GetAttribute("points")!.Split(' ')).Select(static x => Double.Parse(x.Split(',')[0], CultureInfo.InvariantCulture)).ToList();
        Assert.True(points.Min() >= 30);
        Assert.Equal(79, points.Max());
    }

    [Fact]
    public void StacksAreasOfSameGroup()
    {
        Sample[] items = [new(Start, 1, 2), new(Start.AddSeconds(1), 1, 2)];

        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .AddChildContent<AreaSeries<Sample>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)
                .Add(static p => p.StackGroup, "total")
                .Add(static p => p.Title, "A"))
            .AddChildContent<AreaSeries<Sample>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Other)
                .Add(static p => p.StackGroup, "total")
                .Add(static p => p.Title, "B")));

        var lines = cut.FindAll("polyline");
        Assert.Equal("0,1 1,1", lines[0].GetAttribute("points"));
        Assert.Equal("0,3 1,3", lines[1].GetAttribute("points"));
        Assert.Equal("0,3 1,3 1,1 0,1", cut.FindAll("polygon")[1].GetAttribute("points"));
    }

    [Fact]
    public void LegendTableShowsSelectedValues()
    {
        Sample[] items = [new(Start, 10), new(Start.AddSeconds(1), 30), new(Start.AddSeconds(2), 20)];

        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .Add(static p => p.Culture, CultureInfo.InvariantCulture)
            .Add(static p => p.LegendVisible, true)
            .Add(static p => p.LegendMode, ChartLegendMode.Table)
            .Add(static p => p.LegendValues, ChartLegendValues.Last | ChartLegendValues.Max)
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)
                .Add(static p => p.Title, "Load")
                .Add(static p => p.ValueFormat, "{0:N1}%")));

        Assert.Equal(["Name", "Last *", "Max"], cut.FindAll("th").Select(static x => x.TextContent));
        Assert.Equal(["20.0%", "30.0%"], cut.FindAll("td").Skip(1).Select(static x => x.TextContent));
    }

    [Fact]
    public void LegendToggleHidesSeries()
    {
        Sample[] items = [new(Start, 1, 2), new(Start.AddSeconds(1), 1, 2)];
        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .Add(static p => p.LegendToggleEnabled, true)
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)
                .Add(static p => p.Title, "A"))
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Other)
                .Add(static p => p.Title, "B")));

        cut.FindAll(".mussel-legend-item")[0].Click();

        Assert.Single(cut.FindAll(".mussel-series"));
        Assert.Single(cut.FindAll(".mussel-legend-hidden"));

        cut.FindAll(".mussel-legend-item")[1].Click();

        Assert.Single(cut.FindAll(".mussel-series"));
    }

    [Fact]
    public void DrawsThresholdLinesWithLevelClasses()
    {
        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .Add(static p => p.Thresholds, [new ChartThreshold { Level = ChartThresholdLevel.Good }, new ChartThreshold { Value = 50, Level = ChartThresholdLevel.Critical, Label = "Limit" }])
            .Add(static p => p.ThresholdsMode, ChartThresholdsMode.Lines | ChartThresholdsMode.Bands)
            .AddChildContent<NumericAxis>(axis => axis.Add(static p => p.Minimum, 0).Add(static p => p.Maximum, 100))
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, CreateBuffer(10, 10))
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)));

        Assert.Equal("Limit", cut.Find(".mussel-critical .mussel-threshold-label").TextContent);
        Assert.Equal(2, cut.FindAll(".mussel-band").Count);
    }

    [Fact]
    public void ShowsEmptyTextWithoutData()
    {
        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.EmptyText, "No data")
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, Array.Empty<Sample>())
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)));

        Assert.Equal("No data", cut.Find(".mussel-empty text").TextContent);
        Assert.Empty(cut.FindAll("polyline"));
    }

    [Fact]
    public async Task ResizeChangesWidth()
    {
        var cut = RenderLine(CreateBuffer(10, 10));

        await cut.InvokeAsync(() => cut.Instance.OnResize(812.7));

        Assert.Equal("812", cut.Find("svg").GetAttribute("width"));
        Assert.Equal("0 0 812 240", cut.Find("svg").GetAttribute("viewBox"));
    }

    [Fact]
    public void WindowSizeLimitsLegendAndRangeToLatestPoints()
    {
        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .Add(static p => p.Culture, CultureInfo.InvariantCulture)
            .Add(static p => p.LegendVisible, true)
            .Add(static p => p.LegendMode, ChartLegendMode.Table)
            .Add(static p => p.LegendValues, ChartLegendValues.Min)
            .AddChildContent<DateTimeAxis>(axis => axis.Add(static p => p.WindowSize, TimeSpan.FromSeconds(5)))
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, CreateBuffer(20, 20))
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)
                .Add(static p => p.Title, "A")
                .Add(static p => p.ValueFormat, "N0")));

        Assert.Equal("14", cut.FindAll("td")[1].TextContent);
    }

    [Fact]
    public void BarsUseOneRectPerItem()
    {
        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .AddChildContent<BarSeries<Sample>>(series => series
                .Add(static p => p.Items, CreateBuffer(10, 6))
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value - 3)));

        var bars = cut.FindAll(".mussel-bars rect");
        Assert.Equal(6, bars.Count);
        Assert.Equal("-3", bars[0].GetAttribute("y"));
        Assert.Equal("3", bars[0].GetAttribute("height"));
    }

    private IRenderedComponent<MusselChartView> RenderLine(ChartRingBuffer<Sample> buffer) =>
        Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, buffer)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)
                .Add(static p => p.Title, "Value")));

    private static ChartRingBuffer<Sample> CreateBuffer(int capacity, int count)
    {
        var buffer = new ChartRingBuffer<Sample>(capacity);
        for (var i = 0; i < count; i++)
        {
            buffer.Push(new Sample(Start.AddSeconds(i), i));
        }

        return buffer;
    }
}

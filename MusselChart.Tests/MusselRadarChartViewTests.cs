namespace MusselChart;

using MusselChart.Internal;

public sealed class MusselRadarChartViewTests : BunitContext
{
    private static readonly ChartEntry[] First =
    [
        new("A", 10),
        new("B", 20),
        new("C", 30),
        new("D", 40)
    ];

    private static readonly ChartEntry[] Second =
    [
        new("B", 15),
        new("E", 25)
    ];

    public MusselRadarChartViewTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void ItemNamesBecomeAxesAcrossTheSeries()
    {
        var cut = Render<MusselRadarChartView>(parameters => parameters
            .Add(static p => p.Height, 300)
            .AddChildContent<RadarSeries<ChartEntry>>(series => series.Add(static p => p.Items, First).Add(static p => p.Title, "One"))
            .AddChildContent<RadarSeries<ChartEntry>>(series => series.Add(static p => p.Items, Second).Add(static p => p.Title, "Two")));

        Assert.Equal(["A", "B", "C", "D", "E"], cut.FindAll(".mussel-radar-axis").Select(static x => x.TextContent));
        var polygons = cut.FindAll(".mussel-radar-fill");
        Assert.Equal(4, polygons[0].GetAttribute("points")!.Split(' ').Length);
        Assert.Equal(2, polygons[1].GetAttribute("points")!.Split(' ').Length);
        Assert.Equal(5, cut.FindAll(".mussel-radar-areas path").Count);
    }

    [Fact]
    public void LegendShowsTwoOrMoreSeriesAndHidesOne()
    {
        var cut = Render<MusselRadarChartView>(parameters => parameters
            .Add(static p => p.LegendToggleEnabled, true)
            .AddChildContent<RadarSeries<ChartEntry>>(series => series.Add(static p => p.Items, First).Add(static p => p.Title, "One"))
            .AddChildContent<RadarSeries<ChartEntry>>(series => series.Add(static p => p.Items, Second).Add(static p => p.Title, "Two")));

        Assert.Equal(["One", "Two"], cut.FindAll(".mussel-view-legend-name").Select(static x => x.TextContent));

        cut.FindAll(".mussel-view-legend-item")[0].Click();

        Assert.Single(cut.FindAll(".mussel-radar-fill"));
        Assert.Single(cut.FindAll(".mussel-view-legend-hidden"));

        cut.FindAll(".mussel-view-legend-item")[1].Click();

        Assert.Single(cut.FindAll(".mussel-radar-fill"));
    }

    [Fact]
    public void SingleSeriesHasNoLegendByDefault()
    {
        var cut = Render<MusselRadarChartView>(parameters => parameters
            .AddChildContent<RadarSeries<ChartEntry>>(series => series.Add(static p => p.Items, First).Add(static p => p.Title, "One")));

        Assert.Empty(cut.FindAll(".mussel-view-legend-item"));
    }

    [Fact]
    public void ValuesReachTheOuterRingAtTheMaximum()
    {
        var layout = new RadarLayout();
        IReadOnlyList<RadarPoint>[] series = [[new RadarPoint("A", 100), new RadarPoint("B", 0), new RadarPoint("C", 50)]];

        layout.Update(320, 320, new RadarLayoutOptions { Minimum = 0, Maximum = 100, Culture = CultureInfo.InvariantCulture }, series, [true], 5);

        Assert.Equal(1, layout.GetRatio(100));
        Assert.Equal(0, layout.GetRatio(0));
        Assert.Equal(0.5, layout.GetRatio(50));
        var (x, y) = layout.GetPoint(0, 1);
        Assert.Equal(layout.CenterX, x, 3);
        Assert.Equal(layout.CenterY - layout.Radius, y, 3);
    }

    [Fact]
    public void ItemsWithoutANameTakeAxesByPosition()
    {
        var layout = new RadarLayout();
        IReadOnlyList<RadarPoint>[] series = [[new RadarPoint(null, 1), new RadarPoint(null, 2), new RadarPoint(null, 3)]];

        layout.Update(320, 320, new RadarLayoutOptions { Culture = CultureInfo.InvariantCulture }, series, [true], 5);

        Assert.Equal(3, layout.AxisCount);
        Assert.Equal("2", layout.Axes[1].Header);
        Assert.False(layout.ShowsLabels);
    }

    [Fact]
    public void AnItemRepeatedInASeriesKeepsItsFirstValue()
    {
        var layout = new RadarLayout();
        IReadOnlyList<RadarPoint>[] series = [[new RadarPoint("A", 1), new RadarPoint("A", 9), new RadarPoint("B", 3)]];

        layout.Update(320, 320, new RadarLayoutOptions { Culture = CultureInfo.InvariantCulture }, series, [true], 5);

        Assert.Equal(2, layout.AxisCount);
        Assert.Equal(1, layout.GetValue(0, 0));
    }

    [Fact]
    public void FewerThanThreeAxesUseCircles()
    {
        var layout = new RadarLayout();
        IReadOnlyList<RadarPoint>[] series = [[new RadarPoint("A", 1), new RadarPoint("B", 3)]];

        layout.Update(320, 320, new RadarLayoutOptions { Culture = CultureInfo.InvariantCulture }, series, [true], 5);

        Assert.True(layout.IsCircle);
        Assert.Contains("a", layout.CreateGridPath(), StringComparison.Ordinal);
    }

    [Fact]
    public void PolygonGridHasARingForEachLevel()
    {
        var layout = new RadarLayout();
        IReadOnlyList<RadarPoint>[] series = [[new RadarPoint("A", 100), new RadarPoint("B", 50), new RadarPoint("C", 20)]];

        layout.Update(320, 320, new RadarLayoutOptions { Minimum = 0, Maximum = 100, LevelCount = 4, Culture = CultureInfo.InvariantCulture }, series, [true], 5);

        Assert.False(layout.IsCircle);
        var path = layout.CreateGridPath();
        var rings = path.Count(static x => x == 'Z');
        Assert.Equal(layout.Ticks.Count - 1, rings);
    }

    [Fact]
    public void LevelsAreLabeledExceptAtTheCenter()
    {
        var cut = Render<MusselRadarChartView>(parameters => parameters
            .Add(static p => p.Height, 400)
            .Add(static p => p.Minimum, 0)
            .Add(static p => p.Maximum, 100)
            .Add(static p => p.Culture, CultureInfo.InvariantCulture)
            .AddChildContent<RadarSeries<ChartEntry>>(series => series.Add(static p => p.Items, First)));

        var levels = cut.FindAll(".mussel-view-label").Select(static x => x.TextContent).ToList();
        Assert.Contains("100", levels);
        Assert.DoesNotContain("0", levels);
    }

    [Fact]
    public void AreaTooltipsListTheSeries()
    {
        var cut = Render<MusselRadarChartView>(parameters => parameters
            .Add(static p => p.Culture, CultureInfo.InvariantCulture)
            .AddChildContent<RadarSeries<ChartEntry>>(series => series.Add(static p => p.Items, First).Add(static p => p.Title, "One").Add(static p => p.Color, "#AA0000"))
            .AddChildContent<RadarSeries<ChartEntry>>(series => series.Add(static p => p.Items, Second).Add(static p => p.Title, "Two").Add(static p => p.Color, "#00AA00")));

        var tip = cut.FindAll(".mussel-radar-areas path")[1].GetAttribute("data-tip");
        Assert.Contains("\"t\":\"B\"", tip, StringComparison.Ordinal);
        Assert.Contains("[\"#AA0000\",\"One\",\"20\"]", tip, StringComparison.Ordinal);
        Assert.Contains("[\"#00AA00\",\"Two\",\"15\"]", tip, StringComparison.Ordinal);
    }

    [Fact]
    public void AreasAreLeftOutWithoutTheTrackball()
    {
        var cut = Render<MusselRadarChartView>(parameters => parameters
            .Add(static p => p.TrackballMode, ChartTrackballMode.None)
            .AddChildContent<RadarSeries<ChartEntry>>(series => series.Add(static p => p.Items, First)));

        Assert.Empty(cut.FindAll(".mussel-radar-areas path"));
    }

    [Fact]
    public void TypedItemsUseTheirDelegates()
    {
        (string Name, double Score)[] items = [("X", 1), ("Y", 2), ("Z", 3)];

        var cut = Render<MusselRadarChartView>(parameters => parameters
            .Add(static p => p.Height, 300)
            .AddChildContent<RadarSeries<(string Name, double Score)>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.Category, static x => x.Name)
                .Add(static p => p.Y, static x => x.Score)));

        Assert.Equal(["X", "Y", "Z"], cut.FindAll(".mussel-radar-axis").Select(static x => x.TextContent));
    }

    [Fact]
    public void ShowsEmptyTextWithoutData()
    {
        var cut = Render<MusselRadarChartView>(parameters => parameters
            .Add(static p => p.EmptyText, "No data"));

        Assert.Equal("No data", cut.Find(".mussel-empty text").TextContent);
    }
}

namespace MusselChart;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

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
    public async Task ArgumentsMadeOnEveryRenderAreReadOnce()
    {
        var evaluations = 0;
        void Content(RenderTreeBuilder builder)
        {
            evaluations++;
            builder.OpenComponent<RadarSeries<ChartEntry>>(0);
            builder.AddAttribute(1, nameof(RadarSeries<>.Items), evaluations < 20 ? First.ToArray() : First);
            builder.CloseComponent();
        }

        var cut = Render<MusselRadarChartView>(parameters => parameters
            .Add(static p => p.Height, 300)
            .Add(static p => p.ChildContent, Content));

        Assert.Equal((1, 1), (evaluations, cut.RenderCount));
        Assert.Single(cut.FindAll(".mussel-radar-fill"));

        cut.Render();
        await cut.InvokeAsync(() => cut.Instance.OnResize(500));

        Assert.Equal(2, evaluations);
    }

    [Fact]
    public void RadarsOfTheWidestValuesStayFinite()
    {
        ChartEntry[] items = [new("A", 1e308), new("B", -1e308), new("C", 0)];

        var cut = Render<MusselRadarChartView>(parameters => parameters
            .Add(static p => p.Height, 300)
            .AddChildContent<RadarSeries<ChartEntry>>(series => series.Add(static p => p.Items, items).Add(static p => p.Title, "One")));

        Assert.DoesNotContain("NaN", cut.Find(".mussel-radar-fill").GetAttribute("points"), StringComparison.Ordinal);
    }

    [Fact]
    public void AnEndThatIsNotFiniteLeavesNoNaN()
    {
        ChartEntry[] items = [new("A", 1), new("B", 2), new("C", 3)];
        var cut = Render<MusselRadarChartView>(parameters => parameters
            .Add(static p => p.Height, 300)
            .Add(static p => p.Minimum, Double.NegativeInfinity)
            .AddChildContent<RadarSeries<ChartEntry>>(series => series.Add(static p => p.Items, items).Add(static p => p.Title, "One")));

        Assert.DoesNotContain("NaN", cut.Markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(10d, 0d)]
    [InlineData(2d, 2d)]
    public void EndsThatAreOutOfOrderAreDropped(double minimum, double maximum)
    {
        ChartEntry[] items = [new("A", 1), new("B", 2), new("C", 3)];
        var ends = Render<MusselRadarChartView>(parameters => parameters
            .Add(static p => p.Height, 300)
            .Add(static p => p.Minimum, minimum)
            .Add(static p => p.Maximum, maximum)
            .AddChildContent<RadarSeries<ChartEntry>>(series => series.Add(static p => p.Items, items).Add(static p => p.Title, "One")));
        var plain = Render<MusselRadarChartView>(parameters => parameters
            .Add(static p => p.Height, 300)
            .AddChildContent<RadarSeries<ChartEntry>>(series => series.Add(static p => p.Items, items).Add(static p => p.Title, "One")));

        Assert.Equal(plain.Find(".mussel-radar-fill").GetAttribute("points"), ends.Find(".mussel-radar-fill").GetAttribute("points"));
    }

    [Fact]
    public void TheInnerRingsOfTheWidestValuesAreDrawn()
    {
        ChartEntry[] items = [new("A", 1e308), new("B", -1e308), new("C", 0)];

        var cut = Render<MusselRadarChartView>(parameters => parameters
            .Add(static p => p.Height, 300)
            .AddChildContent<RadarSeries<ChartEntry>>(series => series.Add(static p => p.Items, items).Add(static p => p.Title, "One")));

        Assert.True(cut.Find(".mussel-grid-line").GetAttribute("d")!.Count(static x => x == 'Z') > 1);
    }

    [Fact]
    public void SeriesComeAndGoWithTheirComponents()
    {
        static RenderFragment Children(bool second, bool wrapped) => builder =>
        {
            builder.OpenComponent<RadarSeries<ChartEntry>>(0);
            builder.AddAttribute(1, nameof(RadarSeries<>.Items), First);
            builder.AddAttribute(2, nameof(RadarSeries<>.Title), "One");
            builder.CloseComponent();
            if (second)
            {
                builder.OpenComponent<RadarSeries<ChartEntry>>(3);
                builder.AddAttribute(4, nameof(RadarSeries<>.Items), Second);
                builder.AddAttribute(5, nameof(RadarSeries<>.Title), "Two");
                builder.CloseComponent();
            }

            if (wrapped)
            {
                builder.OpenComponent<WrappedRadarSeries>(6);
                builder.AddAttribute(7, nameof(WrappedRadarSeries.Items), Second);
                builder.CloseComponent();
            }
        };

        var cut = Render<MusselRadarChartView>(parameters => parameters.Add(static p => p.Height, 300).Add(static p => p.ChildContent, Children(true, false)));
        var both = cut.FindAll(".mussel-radar-fill").Count;
        cut.Render(parameters => parameters.Add(static p => p.ChildContent, Children(false, false)));
        var removed = cut.FindAll(".mussel-radar-fill").Count;
        cut.Render(parameters => parameters.Add(static p => p.ChildContent, Children(false, true)));
        var added = cut.FindAll(".mussel-radar-fill").Count;

        Assert.Equal((2, 1, 2), (both, removed, added));
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
    public void SeriesBeyondThePaletteAreGray()
    {
        var cut = Render<MusselRadarChartView>(parameters => parameters
            .Add(static p => p.Palette, ["#AA0000"])
            .AddChildContent<RadarSeries<ChartEntry>>(series => series.Add(static p => p.Items, First))
            .AddChildContent<RadarSeries<ChartEntry>>(series => series.Add(static p => p.Items, Second)));

        Assert.Equal(["#AA0000", ChartColor.Other], cut.FindAll(".mussel-radar-fill").Select(static x => x.GetAttribute("fill")));
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
    public void HiddenSeriesComeBackWhenTheyCanNoLongerBeToggled()
    {
        var cut = Render<MusselRadarChartView>(parameters => parameters
            .Add(static p => p.LegendToggleEnabled, true)
            .AddChildContent<RadarSeries<ChartEntry>>(series => series.Add(static p => p.Items, First).Add(static p => p.Title, "One"))
            .AddChildContent<RadarSeries<ChartEntry>>(series => series.Add(static p => p.Items, Second).Add(static p => p.Title, "Two")));
        cut.FindAll(".mussel-view-legend-item")[0].Click();
        Assert.Single(cut.FindAll(".mussel-radar-fill"));

        cut.Render(static parameters => parameters.Add(static p => p.LegendToggleEnabled, false));

        Assert.Equal(2, cut.FindAll(".mussel-radar-fill").Count);
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
        List<List<RadarPoint>> series = [[new RadarPoint("A", 100), new RadarPoint("B", 0), new RadarPoint("C", 50)]];

        layout.Update(320, 320, new RadarLayoutOptions { Minimum = 0, Maximum = 100, Culture = CultureInfo.InvariantCulture }, series, [true], 5);

        Assert.Equal(1, layout.GetRatio(100));
        Assert.Equal(0, layout.GetRatio(0));
        Assert.Equal(0.5, layout.GetRatio(50));
        var (x, y) = layout.GetPoint(0, 1);
        Assert.Equal(layout.CenterX, x, 3);
        Assert.Equal(layout.CenterY - layout.Radius, y, 3);
    }

    [Fact]
    public void ZerosStayAtTheCenterWithoutARange()
    {
        var layout = new RadarLayout();
        List<List<RadarPoint>> series = [[new RadarPoint("A", 0), new RadarPoint("B", 0), new RadarPoint("C", 0)]];

        layout.Update(320, 320, new RadarLayoutOptions { Culture = CultureInfo.InvariantCulture }, series, [true], 5);

        Assert.Equal(0, layout.GetRatio(0));
    }

    [Fact]
    public void ItemsWithoutANameTakeAxesByPosition()
    {
        var layout = new RadarLayout();
        List<List<RadarPoint>> series = [[new RadarPoint(null, 1), new RadarPoint(null, 2), new RadarPoint(null, 3)]];

        layout.Update(320, 320, new RadarLayoutOptions { Culture = CultureInfo.InvariantCulture }, series, [true], 5);

        Assert.Equal(3, layout.AxisCount);
        Assert.Equal("2", layout.Axes[1].Header);
        Assert.False(layout.ShowsLabels);
    }

    [Fact]
    public void AnItemRepeatedInASeriesKeepsItsFirstValue()
    {
        var layout = new RadarLayout();
        List<List<RadarPoint>> series = [[new RadarPoint("A", 1), new RadarPoint("A", 9), new RadarPoint("B", 3)]];

        layout.Update(320, 320, new RadarLayoutOptions { Culture = CultureInfo.InvariantCulture }, series, [true], 5);

        Assert.Equal(2, layout.AxisCount);
        Assert.Equal(1, layout.GetValue(0, 0));
    }

    [Fact]
    public void FewerThanThreeAxesUseCircles()
    {
        var layout = new RadarLayout();
        List<List<RadarPoint>> series = [[new RadarPoint("A", 1), new RadarPoint("B", 3)]];

        layout.Update(320, 320, new RadarLayoutOptions { Culture = CultureInfo.InvariantCulture }, series, [true], 5);

        Assert.True(layout.IsCircle);
        Assert.Contains("a", layout.CreateGridPath(), StringComparison.Ordinal);
    }

    [Fact]
    public void PolygonGridHasARingForEachLevel()
    {
        var layout = new RadarLayout();
        List<List<RadarPoint>> series = [[new RadarPoint("A", 100), new RadarPoint("B", 50), new RadarPoint("C", 20)]];

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
    public void TheFormatOfASeriesWinsOverTheFormatterOfTheChart()
    {
        var cut = Render<MusselRadarChartView>(parameters => parameters
            .Add(static p => p.Culture, CultureInfo.InvariantCulture)
            .Add(static p => p.ValueFormatter, static _ => "chart")
            .AddChildContent<RadarSeries<ChartEntry>>(series => series.Add(static p => p.Items, First).Add(static p => p.Title, "One").Add(static p => p.Color, "#AA0000").Add(static p => p.ValueFormat, "{0:N1}")));

        var tip = cut.FindAll(".mussel-radar-areas path")[1].GetAttribute("data-tip");
        Assert.Contains("[\"#AA0000\",\"One\",\"20.0\"]", tip, StringComparison.Ordinal);
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

    [Fact]
    public void ShowsEmptyTextWhenOnlyHiddenSeriesHaveValues()
    {
        ChartEntry[] empty = [new("A", Double.NaN), new("B", Double.NaN), new("C", Double.NaN)];
        var cut = Render<MusselRadarChartView>(parameters => parameters
            .Add(static p => p.EmptyText, "No data")
            .Add(static p => p.LegendToggleEnabled, true)
            .AddChildContent<RadarSeries<ChartEntry>>(series => series.Add(static p => p.Items, First).Add(static p => p.Title, "One"))
            .AddChildContent<RadarSeries<ChartEntry>>(series => series.Add(static p => p.Items, empty).Add(static p => p.Title, "Two")));
        Assert.Equal(string.Empty, cut.Find(".mussel-empty text").TextContent);

        cut.FindAll(".mussel-view-legend-item")[0].Click();

        Assert.Equal("No data", cut.Find(".mussel-empty text").TextContent);
    }

    [Fact]
    public void ShowsEmptyTextWhenARepeatedNameKeepsAValueThatIsNotFinite()
    {
        ChartEntry[] repeated = [new("A", Double.NaN), new("A", 5)];
        var cut = Render<MusselRadarChartView>(parameters => parameters
            .Add(static p => p.EmptyText, "No data")
            .AddChildContent<RadarSeries<ChartEntry>>(series => series.Add(static p => p.Items, repeated)));

        Assert.Equal("No data", cut.Find(".mussel-empty text").TextContent);
    }

    [Fact]
    public void ARingBufferIsCopiedBeforeItIsRead()
    {
        var buffer = new ChartRingBuffer<ChartEntry>(4);
        foreach (var entry in First)
        {
            buffer.Push(entry);
        }

        var seen = new HashSet<ChartEntry>();

        Render<MusselRadarChartView>(parameters => parameters
            .AddChildContent<RadarSeries<ChartEntry>>(series => series
                .Add(static p => p.Items, buffer)
                .Add(p => p.Y, x =>
                {
                    seen.Add(x);
                    buffer.Clear();
                    return x.Value;
                })));

        Assert.Equal(4, seen.Count);
    }

    [Fact]
    public void SeriesThatShareABufferReadTheSameItems()
    {
        var buffer = new ChartRingBuffer<ChartEntry>(8);
        foreach (var entry in First)
        {
            buffer.Push(entry);
        }

        var (first, second, pushed) = (0, 0, false);
        Render<MusselRadarChartView>(parameters => parameters
            .AddChildContent<RadarSeries<ChartEntry>>(series => series
                .Add(static p => p.Items, buffer)
                .Add(p => p.Y, x =>
                {
                    first++;
                    if (!pushed)
                    {
                        pushed = true;
                        buffer.Push(new ChartEntry("Extra", 1));
                    }

                    return x.Value;
                }))
            .AddChildContent<RadarSeries<ChartEntry>>(series => series
                .Add(static p => p.Items, buffer)
                .Add(p => p.Y, x =>
                {
                    second++;
                    return x.Value;
                })));

        Assert.Equal(first, second);
    }

    public sealed class WrappedRadarSeries : ComponentBase
    {
        [Parameter]
        public IReadOnlyList<ChartEntry> Items { get; set; } = [];

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<RadarSeries<ChartEntry>>(0);
            builder.AddAttribute(1, nameof(RadarSeries<>.Items), Items);
            builder.AddAttribute(2, nameof(RadarSeries<>.Title), "Wrapped");
            builder.CloseComponent();
        }
    }
}

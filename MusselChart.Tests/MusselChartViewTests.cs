namespace MusselChart;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

using MusselChart.Internal;

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
        Assert.Equal("0,0 1,0", lines[0].GetAttribute("points"));
        Assert.Equal("0,2 1,2", lines[1].GetAttribute("points"));
        Assert.Equal("0,2 1,2 1,0 0,0", cut.FindAll("polygon")[1].GetAttribute("points"));
    }

    [Fact]
    public void AreasOfOneBufferStackOnTheSamePoints()
    {
        var buffer = new ChartRingBuffer<Sample>(10);
        buffer.Push(new(Start, 1, 2));
        var pushed = false;

        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .AddChildContent<AreaSeries<Sample>>(series => series
                .Add(static p => p.Items, buffer)
                .Add(static p => p.X, static x => x.Time)
                .Add(p => p.Y, x =>
                {
                    if (!pushed)
                    {
                        pushed = true;
                        buffer.Push(new(Start.AddSeconds(1), 1, 2));
                    }

                    return x.Value;
                })
                .Add(static p => p.StackGroup, "total"))
            .AddChildContent<AreaSeries<Sample>>(series => series
                .Add(static p => p.Items, buffer)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Other)
                .Add(static p => p.StackGroup, "total")));
        cut.Render();

        var lines = cut.FindAll("polyline");
        Assert.Equal("0,0 1,0", lines[0].GetAttribute("points"));
        Assert.Equal("0,2 1,2", lines[1].GetAttribute("points"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SeriesOfAFullBufferKeepTheSameOldestPoint(bool timeAxis)
    {
        Func<Sample, string?>? category = timeAxis ? null : static _ => null;
        var buffer = new ChartRingBuffer<Sample>(3);
        for (var i = 0; i < 3; i++)
        {
            buffer.Push(new Sample(Start.AddSeconds(i), i));
        }

        var pending = new Queue<Sample>();
        var first = new List<double>();
        var second = new List<double>();
        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .Add(static p => p.LegendVisible, true)
            .Add(static p => p.LegendMode, ChartLegendMode.Table)
            .Add(static p => p.LegendValues, ChartLegendValues.Min)
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, buffer)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Category, category)
                .Add(p => p.Y, x =>
                {
                    if (pending.TryDequeue(out var sample))
                    {
                        buffer.Push(sample);
                    }

                    return x.Value;
                })
                .Add(static p => p.Title, "first")
                .Add(p => p.ValueFormatter, x =>
                {
                    first.Add(x);
                    return string.Empty;
                }))
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, buffer)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Category, category)
                .Add(static p => p.Y, static x => x.Value)
                .Add(static p => p.Title, "second")
                .Add(p => p.ValueFormatter, x =>
                {
                    second.Add(x);
                    return string.Empty;
                })));
        first.Clear();
        second.Clear();

        pending.Enqueue(new Sample(Start.AddSeconds(4), 4));
        buffer.Push(new Sample(Start.AddSeconds(3), 3));

        return cut.WaitForAssertionAsync(() =>
        {
            Assert.Equal(2, second.Count);
            Assert.Equal(first, second);
        });
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
    public void TheFormatOfASeriesWinsOverTheFormatterOfTheAxis()
    {
        Sample[] items = [new(Start, 10), new(Start.AddSeconds(1), 30)];

        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .Add(static p => p.Culture, CultureInfo.InvariantCulture)
            .Add(static p => p.LegendVisible, true)
            .Add(static p => p.LegendMode, ChartLegendMode.Table)
            .Add(static p => p.LegendValues, ChartLegendValues.Last)
            .AddChildContent<NumericAxis>(static axis => axis.Add(static p => p.LabelFormatter, static _ => "axis"))
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)
                .Add(static p => p.Title, "Load")
                .Add(static p => p.ValueFormat, "{0:N1}%")));

        Assert.Equal(["30.0%"], cut.FindAll("td").Skip(1).Select(static x => x.TextContent));
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
    public void HiddenSeriesComeBackWhenTheyCanNoLongerBeToggled()
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

        cut.Render(static parameters => parameters.Add(static p => p.LegendToggleEnabled, false));
        var untoggled = cut.FindAll(".mussel-series").Count;
        cut.Render(static parameters => parameters.Add(static p => p.LegendToggleEnabled, true));
        cut.FindAll(".mussel-legend-item")[0].Click();
        cut.Render(parameters => parameters.AddChildContent<LineSeries<Sample>>(series => series
            .Add(static p => p.Items, items)
            .Add(static p => p.X, static x => x.Time)
            .Add(static p => p.Y, static x => x.Value)
            .Add(static p => p.Title, "A")));

        Assert.Equal(2, untoggled);
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
    public async Task AResizeAfterTheViewIsGoneDoesNothing()
    {
        var cut = RenderLine(CreateBuffer(10, 10));
        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());

        await cut.InvokeAsync(() => new ChartScriptBridge(cut.Instance).OnResize(812));

        Assert.Contains("mussel-unmeasured", cut.Find("svg").ClassList);
    }

    [Fact]
    public async Task StaysHiddenWithItsHeightUntilTheWidthIsMeasured()
    {
        var cut = RenderLine(CreateBuffer(10, 10));

        Assert.Contains("mussel-unmeasured", cut.Find("svg").ClassList);
        Assert.Equal("240", cut.Find("svg").GetAttribute("height"));

        await cut.InvokeAsync(() => cut.Instance.OnResize(0));

        Assert.Contains("mussel-unmeasured", cut.Find("svg").ClassList);

        await cut.InvokeAsync(() => cut.Instance.OnResize(812));

        Assert.DoesNotContain("mussel-unmeasured", cut.Find("svg").ClassList);
        Assert.Equal("240", cut.Find("svg").GetAttribute("height"));
    }

    [Fact]
    public async Task MeasuringTheDefaultWidthAlsoShowsTheChart()
    {
        var cut = RenderLine(CreateBuffer(10, 10));

        await cut.InvokeAsync(() => cut.Instance.OnResize(640));

        Assert.DoesNotContain("mussel-unmeasured", cut.Find("svg").ClassList);
        Assert.Equal("640", cut.Find("svg").GetAttribute("width"));
    }

    [Fact]
    public void PrerenderingStaysHiddenUntilTheChartStarts()
    {
        SetRendererInfo(new RendererInfo("Static", false));
        var buffer = CreateBuffer(10, 10);
        var prerendered = Render<MusselChartView>(parameters => parameters
            .SetAssignedRenderMode(RenderMode.InteractiveServer)
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, buffer)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)));
        var rendered = RenderLine(buffer);

        Assert.Contains("mussel-prerendering", prerendered.Find("svg").ClassList);
        Assert.DoesNotContain("mussel-unmeasured", prerendered.Find("svg").ClassList);
        Assert.Contains("mussel-unmeasured", rendered.Find("svg").ClassList);
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
        Assert.Equal("-3", cut.Find("svg.mussel-chart-svg").GetAttribute("data-value-base"));
        Assert.Equal("0", bars[0].GetAttribute("y"));
        Assert.Equal("3", bars[0].GetAttribute("height"));
    }

    [Fact]
    public void BarsInTheBufferKeepTheirAttributesWhenAPointIsAdded()
    {
        var buffer = CreateBuffer(100, 6);
        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .AddChildContent<DateTimeAxis>(axis => axis.Add(static p => p.WindowSize, TimeSpan.FromSeconds(60)))
            .AddChildContent<BarSeries<Sample>>(series => series
                .Add(static p => p.Items, buffer)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value + 1)));
        var first = cut.FindAll(".mussel-bars rect")[0];
        var (x, y, height, value) = (first.GetAttribute("x"), first.GetAttribute("y"), first.GetAttribute("height"), first.GetAttribute("data-v"));

        buffer.Push(new Sample(Start.AddSeconds(6), 5));
        cut.Render();

        var bars = cut.FindAll(".mussel-bars rect");
        Assert.Equal(7, bars.Count);
        Assert.Equal(x, bars[0].GetAttribute("x"));
        Assert.Equal(y, bars[0].GetAttribute("y"));
        Assert.Equal(height, bars[0].GetAttribute("height"));
        Assert.Equal(value, bars[0].GetAttribute("data-v"));
        Assert.Equal("6", bars[^1].GetAttribute("x"));
    }

    [Fact]
    public void StackedBarsLeaveTheBufferTogether()
    {
        var first = CreateBuffer(3, 3);
        var second = CreateBuffer(3, 3);
        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .AddChildContent<BarSeries<Sample>>(series => series
                .Add(static p => p.Items, first)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value + 1)
                .Add(static p => p.StackGroup, "total"))
            .AddChildContent<BarSeries<Sample>>(series => series
                .Add(static p => p.Items, second)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value + 1)
                .Add(static p => p.StackGroup, "total")));

        for (var i = 3; i < 6; i++)
        {
            first.Push(new Sample(Start.AddSeconds(i), i));
            second.Push(new Sample(Start.AddSeconds(i), i));
            cut.Render();
        }

        var bars = cut.FindAll(".mussel-bars rect");
        Assert.Equal(6, bars.Count);
        Assert.Equal(["3", "4", "5"], bars.Take(3).Select(static x => x.GetAttribute("x")));
        Assert.Equal(["4", "5", "6"], bars.Take(3).Select(static x => x.GetAttribute("data-v")));
        Assert.Equal("1", cut.Find("svg.mussel-chart-svg").GetAttribute("data-value-base"));
        Assert.InRange(Double.Parse(bars[3].GetAttribute("y")!, CultureInfo.InvariantCulture), 3, 4);
    }

    [Fact]
    public async Task ArgumentsMadeOnEveryRenderAreReadOnce()
    {
        Sample[] samples = [new(Start, 1), new(Start.AddSeconds(1), 2)];
        var evaluations = 0;
        void Content(RenderTreeBuilder builder)
        {
            evaluations++;
            builder.OpenComponent<LineSeries<Sample>>(0);
            builder.AddAttribute(1, nameof(LineSeries<>.Items), evaluations < 20 ? samples.ToArray() : samples);
            builder.AddAttribute(2, nameof(LineSeries<>.X), (Func<Sample, DateTime>)(static x => x.Time));
            builder.AddAttribute(3, nameof(LineSeries<>.Y), (Func<Sample, double>)(static x => x.Value));
            builder.CloseComponent();
        }

        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .Add(static p => p.ChildContent, Content));

        Assert.Equal((1, 1), (evaluations, cut.RenderCount));
        Assert.Single(cut.FindAll("polyline"));

        cut.Render();
        await cut.InvokeAsync(() => cut.Instance.OnResize(500));

        Assert.Equal(2, evaluations);
    }

    [Fact]
    public void WrappedArgumentsMadeOnEveryRenderStop()
    {
        Sample[] samples = [new(Start, 1), new(Start.AddSeconds(1), 2)];
        var counter = new RenderCounter();
        void Content(RenderTreeBuilder builder)
        {
            builder.OpenComponent<OuterSeries>(0);
            builder.AddAttribute(1, nameof(OuterSeries.Samples), samples);
            builder.AddAttribute(2, nameof(OuterSeries.Counter), counter);
            builder.CloseComponent();
        }

        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .Add(static p => p.ChildContent, Content));
        cut.Render();

        Assert.Equal(2, counter.Count);
        Assert.Single(cut.FindAll("polyline"));
    }

    [Fact]
    public void TooltipValuesKeepNineDigits()
    {
        Sample[] items = [new(Start, 1234567, 12345.67), new(Start.AddSeconds(1), 7654321, 76543.21)];

        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value))
            .AddChildContent<AreaSeries<Sample>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Other)
                .Add(static p => p.StackGroup, "total"))
            .AddChildContent<BarSeries<Sample>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)));

        Assert.Equal("12345.67", cut.Find("svg.mussel-chart-svg").GetAttribute("data-value-base"));
        Assert.Equal("0,1222221.33 1,7641975.33", cut.FindAll("polyline")[0].GetAttribute("points"));
        Assert.Equal("12345.67 76543.21", cut.Find("[data-values]").GetAttribute("data-values"));
        Assert.Equal(["1234567", "7654321"], cut.FindAll(".mussel-bars rect").Select(static x => x.GetAttribute("data-v")));
        Assert.Equal(["1234567", "7654321"], cut.FindAll(".mussel-bars rect").Select(static x => x.GetAttribute("height")));
    }

    [Fact]
    public void TooltipValuesOfTenDigitsKeepEveryDigit()
    {
        Sample[] items = [new(Start, 1234567955, 1234567955.5), new(Start.AddSeconds(1), 1234567890, 1234567890.25)];

        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .AddChildContent<AreaSeries<Sample>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Other)
                .Add(static p => p.StackGroup, "total"))
            .AddChildContent<BarSeries<Sample>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)));

        Assert.Equal("1234567955.5 1234567890.25", cut.Find("[data-values]").GetAttribute("data-values"));
        Assert.Equal(["1234567955", "1234567890"], cut.FindAll(".mussel-bars rect").Select(static x => x.GetAttribute("data-v")));
    }

    [Theory]
    [InlineData("2026-10-25T00:58:30Z", "02:59:30", "02:00:00", "03:00:00", "60", "1792890000,120")]
    [InlineData("2026-03-29T00:58:30Z", "01:59:30", "03:00:00", "02:00:00", "120", "1774746000,60")]
    public void TheTimeAxisCrossesAChangeOfTheOffset(string start, string before, string after, string absent, string offset, string change)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        var first = DateTime.Parse(start, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
        Sample[] items = [new(first, 1), new(first.AddMinutes(3), 2)];

        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, zone)
            .Add(static p => p.Culture, CultureInfo.InvariantCulture)
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)));

        var labels = cut.FindAll(".mussel-axis text[text-anchor='middle']").Select(static x => x.TextContent).ToList();
        Assert.Contains(before, labels);
        Assert.Contains(after, labels);
        Assert.Equal(labels.IndexOf(before) + 1, labels.IndexOf(after));
        Assert.DoesNotContain(absent, labels);
        var root = cut.Find(".mussel-chart");
        Assert.Equal((offset, change), (root.GetAttribute("data-offset"), root.GetAttribute("data-offset-changes")));
    }

    [Fact]
    public void TheTimeAxisListsEveryChangeOfTheOffset()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        Sample[] items = [new(new DateTime(2025, 11, 17, 5, 0, 0, DateTimeKind.Utc), 1), new(new DateTime(2026, 11, 16, 5, 0, 0, DateTimeKind.Utc), 2)];

        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, zone)
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)));

        var root = cut.Find(".mussel-chart");
        Assert.Equal(("-300", "1772953200,-300 1793512800,-240"), (root.GetAttribute("data-offset"), root.GetAttribute("data-offset-changes")));
    }

    [Fact]
    public void TheBaseOfTheTimeAxisIsInUnixSeconds()
    {
        Sample[] items = [new(new DateTime(2027, 3, 1, 9, 30, 0, DateTimeKind.Utc), 1), new(new DateTime(2027, 3, 2, 9, 30, 0, DateTimeKind.Utc), 2)];

        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)));

        Assert.Equal("1803893400", cut.Find("svg.mussel-chart-svg").GetAttribute("data-base"));
    }

    [Fact]
    public void LargeValuesKeepTheScaleOfThePlot()
    {
        Sample[] items = [new(Start, 0), new(Start.AddSeconds(1), 1e9)];

        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)));

        var matrix = cut.Find(".mussel-plot").GetAttribute("transform")!;
        var scale = Double.Parse(matrix.Split(' ')[3], CultureInfo.InvariantCulture);
        Assert.True(scale < 0, matrix);
    }

    [Fact]
    public void StackedAreasKeepNineDigitsOnTheBottomEdge()
    {
        Sample[] items = [new(Start, 1234567.8, 1), new(Start.AddSeconds(1), 7654321.2, 1)];

        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .AddChildContent<AreaSeries<Sample>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)
                .Add(static p => p.StackGroup, "total"))
            .AddChildContent<AreaSeries<Sample>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Other)
                .Add(static p => p.StackGroup, "total")));

        Assert.Equal("1", cut.Find("svg.mussel-chart-svg").GetAttribute("data-value-base"));
        Assert.EndsWith("1,7654320.2 0,1234566.8", cut.FindAll("polygon")[1].GetAttribute("points"), StringComparison.Ordinal);
    }

    [Fact]
    public void LargeValuesAreWrittenFromTheirBase()
    {
        Sample[] items = [new(Start, 1234567890), new(Start.AddSeconds(1), 1234567891), new(Start.AddSeconds(2), 1234567955)];

        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)));

        Assert.Equal("1234567890", cut.Find("svg.mussel-chart-svg").GetAttribute("data-value-base"));
        Assert.Equal("0,0 1,1 2,65", cut.Find("polyline").GetAttribute("points"));
        var matrix = cut.Find(".mussel-plot").GetAttribute("transform")!.TrimEnd(')').Split(' ');
        Assert.InRange(Double.Parse(matrix[^1], CultureInfo.InvariantCulture), -1000, 1000);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("total")]
    public void BarsFromZeroKeepTheirTopsWhenTheValueAxisLeavesZeroOut(string? group)
    {
        Sample[] items = [new(Start, 1234567955), new(Start.AddSeconds(1), 1234567900)];

        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .AddChildContent<NumericAxis>(static axis => axis.Add(static p => p.Minimum, 1234567800).Add(static p => p.Maximum, 1234568000))
            .AddChildContent<BarSeries<Sample>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)
                .Add(static p => p.StackGroup, group)));

        var valueBase = Double.Parse(cut.Find("svg.mussel-chart-svg").GetAttribute("data-value-base")!, CultureInfo.InvariantCulture);
        Assert.All(cut.FindAll(".mussel-bars rect"), rect =>
        {
            var y = Double.Parse(rect.GetAttribute("y")!, CultureInfo.InvariantCulture);
            var height = Double.Parse(rect.GetAttribute("height")!, CultureInfo.InvariantCulture);
            var top = Double.Parse(rect.GetAttribute("data-v")!, CultureInfo.InvariantCulture) - valueBase;
            Assert.InRange(y + height - top, -0.01, 0.01);
            Assert.InRange((float)y + (float)height - top, -0.01, 0.01);
        });
    }

    [Fact]
    public void LargeNumbersAreWrittenFromTheirBase()
    {
        PointItem[] line = [new(1e6, 1234567890), new(1e6 + 1, 1234567891), new(1e6 + 2, 1234567955)];
        PointItem[] points = [new(1e6 + 1, 1234567900)];

        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.Culture, CultureInfo.InvariantCulture)
            .AddChildContent<LineSeries<PointItem>>(series => series
                .Add(static p => p.Items, line)
                .Add(static p => p.XValue, static x => x.X)
                .Add(static p => p.Y, static x => x.Y))
            .AddChildContent<ScatterSeries<PointItem>>(series => series
                .Add(static p => p.Items, points)
                .Add(static p => p.XValue, static x => x.X)
                .Add(static p => p.Y, static x => x.Y)));

        var svg = cut.Find("svg.mussel-chart-svg");
        Assert.Equal(("1000000", "1234567890"), (svg.GetAttribute("data-base"), svg.GetAttribute("data-value-base")));
        Assert.Equal("0,0 1,1 2,65", cut.Find("polyline").GetAttribute("points"));
        var marker = cut.Find(".mussel-markers circle");
        Assert.Equal(("1", "10"), (marker.GetAttribute("data-x"), marker.GetAttribute("data-y")));
        var matrix = cut.Find(".mussel-plot").GetAttribute("transform")!.TrimEnd(')').Split(' ');
        Assert.All(matrix[^2..], static x => Assert.InRange(Double.Parse(x, CultureInfo.InvariantCulture), -1000, 1000));
    }

    [Theory]
    [InlineData(ChartOrientation.Vertical)]
    [InlineData(ChartOrientation.Horizontal)]
    public void MarkersSitWhereThePlotPutsTheirValues(ChartOrientation orientation)
    {
        PointItem[] points = [new(1e6, 1234567890), new(1e6 + 1, 1234567955), new(1e6 + 3, 1234567900)];

        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.Culture, CultureInfo.InvariantCulture)
            .Add(static p => p.Orientation, orientation)
            .AddChildContent<ScatterSeries<PointItem>>(series => series
                .Add(static p => p.Items, points)
                .Add(static p => p.XValue, static x => x.X)
                .Add(static p => p.Y, static x => x.Y)));

        var transform = cut.Find(".mussel-plot").GetAttribute("transform")!;
        var markers = cut.FindAll(".mussel-markers circle");
        Assert.Equal(3, markers.Count);
        Assert.All(markers, marker =>
        {
            var (x, y) = Transform(transform, Number(marker, "data-x"), Number(marker, "data-y"));
            Assert.Equal(Number(marker, "cx"), x, 0.01);
            Assert.Equal(Number(marker, "cy"), y, 0.01);
        });
    }

    [Fact]
    public void TheLivePointSitsOnTheLastPointOfTheLine()
    {
        Sample[] items = [new(Start, 1234567890), new(Start.AddSeconds(1), 1234567955), new(Start.AddSeconds(2), 1234567900)];

        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)
                .Add(static p => p.LiveIndicatorVisible, true)));

        var last = cut.Find("polyline").GetAttribute("points")!.Split(' ')[^1].Split(',');
        var (x, y) = Transform(cut.Find(".mussel-plot").GetAttribute("transform")!, Double.Parse(last[0], CultureInfo.InvariantCulture), Double.Parse(last[1], CultureInfo.InvariantCulture));
        var live = cut.Find(".mussel-live circle");
        Assert.Equal(Number(live, "cx"), x, 0.05);
        Assert.Equal(Number(live, "cy"), y, 0.05);
    }

    [Fact]
    public void TheValueBaseComesFromTheVisibleSeries()
    {
        Sample[] shown = [new(Start, 1234567890), new(Start.AddSeconds(1), 1234567955)];
        Sample[] hidden = [new(Start, 5), new(Start.AddSeconds(1), 6)];

        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, shown)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value))
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, hidden)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)
                .Add(static p => p.IsVisible, false)));

        Assert.Equal("1234567890", cut.Find("svg.mussel-chart-svg").GetAttribute("data-value-base"));
        Assert.Equal("0,0 1,65", cut.Find("polyline").GetAttribute("points"));
    }

    [Fact]
    public void TheBasesOfXComeFromTheVisibleSeries()
    {
        PointItem[] shown = [new(1700000000, 5), new(1700000001, 6)];
        PointItem[] hidden = [new(0, 5), new(1, 6)];
        Sample[] recent = [new(Start, 5), new(Start.AddSeconds(1), 6)];
        Sample[] older = [new(Start.AddDays(-400), 5)];

        var numbers = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.Culture, CultureInfo.InvariantCulture)
            .AddChildContent<LineSeries<PointItem>>(series => series
                .Add(static p => p.Items, shown)
                .Add(static p => p.XValue, static x => x.X)
                .Add(static p => p.Y, static x => x.Y))
            .AddChildContent<LineSeries<PointItem>>(series => series
                .Add(static p => p.Items, hidden)
                .Add(static p => p.XValue, static x => x.X)
                .Add(static p => p.Y, static x => x.Y)
                .Add(static p => p.IsVisible, false)));
        var times = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, recent)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value))
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, older)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)
                .Add(static p => p.IsVisible, false)));

        Assert.Equal("1700000000", numbers.Find("svg.mussel-chart-svg").GetAttribute("data-base"));
        Assert.Equal("0,0 1,1", numbers.Find("polyline").GetAttribute("points"));
        Assert.Equal(((Start - DateTime.UnixEpoch).Ticks / TimeSpan.TicksPerSecond).ToString(CultureInfo.InvariantCulture), times.Find("svg.mussel-chart-svg").GetAttribute("data-base"));
    }

    [Fact]
    public void ALiveChartKeepsTheBaseOfTheFirstValuesItReads()
    {
        var buffer = new ChartRingBuffer<Sample>(10);
        buffer.Push(new Sample(Start, Double.NaN));
        var cut = RenderLine(buffer);

        buffer.Push(new Sample(Start.AddSeconds(1), 1234567955));
        cut.Render();
        buffer.Push(new Sample(Start.AddSeconds(2), 1234567890));
        cut.Render();

        Assert.Equal("1234567955", cut.Find("svg.mussel-chart-svg").GetAttribute("data-value-base"));
        Assert.Equal("1,0 2,-65", cut.Find("polyline").GetAttribute("points"));
    }

    [Fact]
    public void TimesAtTheEndsOfDateTimeStillDraw()
    {
        Sample[] unset = [new(default, 1), new(default, 2)];
        Sample[] last = [new(DateTime.MaxValue, 1), new(DateTime.MaxValue.AddDays(-1), 2)];
        var tokyo = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");

        var start = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .AddChildContent<BarSeries<Sample>>(series => series
                .Add(static p => p.Items, unset)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)));
        var end = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, tokyo)
            .AddChildContent<DateTimeAxis>(static axis => axis.Add(static p => p.Maximum, DateTime.MaxValue))
            .AddChildContent<BarSeries<Sample>>(series => series
                .Add(static p => p.Items, last)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)));

        Assert.NotEmpty(start.FindAll(".mussel-axis text"));
        Assert.NotEmpty(end.FindAll(".mussel-axis text"));
    }

    [Fact]
    public void ChangedThicknessReachesTheLines()
    {
        var cut = RenderLine(CreateBuffer(10, 3));
        Assert.Equal("2", cut.Find("polyline").GetAttribute("stroke-width"));

        cut.FindComponent<LineSeries<Sample>>().Render(static parameters => parameters.Add(static p => p.Thickness, 5d));

        Assert.Equal("5", cut.Find("polyline").GetAttribute("stroke-width"));
    }

    [Fact]
    public void ChangedBarWidthMovesAllBars()
    {
        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .AddChildContent<BarSeries<Sample>>(series => series
                .Add(static p => p.Items, CreateBuffer(10, 6))
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value + 1)));
        var transform = cut.Find(".mussel-bars").GetAttribute("transform");
        var width = Double.Parse(cut.Find(".mussel-bars rect").GetAttribute("width")!, CultureInfo.InvariantCulture);

        cut.FindComponent<BarSeries<Sample>>().Render(static parameters => parameters.Add(static p => p.MaxBarWidth, 8d));

        Assert.NotEqual(transform, cut.Find(".mussel-bars").GetAttribute("transform"));
        Assert.True(Double.Parse(cut.Find(".mussel-bars rect").GetAttribute("width")!, CultureInfo.InvariantCulture) < width);
    }

    [Fact]
    public void TheInvariantCultureGoesToTheTooltipAsAFixedLanguage()
    {
        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .Add(static p => p.Culture, CultureInfo.InvariantCulture)
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, CreateBuffer(10, 10))
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)));

        Assert.Equal("en-US", cut.Find(".mussel-chart").GetAttribute("data-culture"));
    }

    [Fact]
    public void TooltipsShowTheDateWhenTheRangeCrossesMidnight()
    {
        var first = new DateTime(2026, 3, 1, 23, 0, 0, DateTimeKind.Utc);
        var items = Enumerable.Range(0, 5).Select(x => new Sample(first.AddMinutes(x * 30), x)).ToArray();

        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .Add(static p => p.Culture, CultureInfo.GetCultureInfo("en-US"))
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)));

        Assert.Equal("M/d HH:mm", cut.Find(".mussel-chart").GetAttribute("data-time-format"));
    }

    [Theory]
    [InlineData("de-DE", "^[0-9]+/7$", "d/M")]
    [InlineData("en-US", "^7/[0-9]+$", "M/d")]
    public void DefaultTimeLabelsFollowTheOrderOfTheCultureWithTheSeparatorsOfTheTooltip(string culture, string pattern, string format)
    {
        var items = Enumerable.Range(0, 7).Select(static x => new Sample(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(x), x + 1)).ToArray();

        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .Add(static p => p.Culture, new CultureInfo(culture))
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)));

        var dates = cut.FindAll(".mussel-axis text").Select(static x => x.TextContent).Where(static x => x.Contains('/', StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(dates);
        Assert.All(dates, x => Assert.Matches(pattern, x));
        Assert.Equal(format, cut.Find(".mussel-chart").GetAttribute("data-time-format"));
    }

    [Fact]
    public void TimesSkippedBySummerTimeMoveForward()
    {
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            DateTime.MinValue.Date,
            DateTime.MaxValue.Date,
            TimeSpan.FromHours(1),
            TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 5, DayOfWeek.Sunday),
            TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 3, 0, 0), 10, 5, DayOfWeek.Sunday));
        var zone = TimeZoneInfo.CreateCustomTimeZone("Summer", TimeSpan.FromHours(1), "Summer", "Winter", "Summer", [rule]);
        Sample[] items = [new(new DateTime(2026, 3, 29, 1, 30, 0), 1), new(new DateTime(2026, 3, 29, 2, 30, 0), 2), new(new DateTime(2026, 3, 29, 3, 30, 0), 3)];

        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, zone)
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)));

        Assert.Equal("0,0 3600,1 3600,2", cut.Find("polyline").GetAttribute("points"));
    }

    private IRenderedComponent<MusselChartView> RenderLine(ChartRingBuffer<Sample> buffer) =>
        Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, buffer)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)
                .Add(static p => p.Title, "Value")));

    private static (double X, double Y) Transform(string transform, double x, double y)
    {
        var m = transform["matrix(".Length..^1].Split(' ').Select(static v => Double.Parse(v, CultureInfo.InvariantCulture)).ToArray();
        return ((m[0] * x) + (m[2] * y) + m[4], (m[1] * x) + (m[3] * y) + m[5]);
    }

    private static double Number(AngleSharp.Dom.IElement element, string name) => Double.Parse(element.GetAttribute(name)!, CultureInfo.InvariantCulture);

    private static ChartRingBuffer<Sample> CreateBuffer(int capacity, int count)
    {
        var buffer = new ChartRingBuffer<Sample>(capacity);
        for (var i = 0; i < count; i++)
        {
            buffer.Push(new Sample(Start.AddSeconds(i), i));
        }

        return buffer;
    }

    public sealed class RenderCounter
    {
        public int Count { get; set; }
    }

    public sealed class OuterSeries : ComponentBase
    {
        [Parameter]
        public IReadOnlyList<Sample> Samples { get; set; } = [];

        [Parameter]
        public RenderCounter Counter { get; set; } = new();

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<InnerSeries>(0);
            builder.AddAttribute(1, nameof(InnerSeries.Samples), Samples);
            builder.AddAttribute(2, nameof(InnerSeries.Counter), Counter);
            builder.CloseComponent();
        }
    }

    public sealed class InnerSeries : ComponentBase
    {
        [Parameter]
        public IReadOnlyList<Sample> Samples { get; set; } = [];

        [Parameter]
        public RenderCounter Counter { get; set; } = new();

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            Counter.Count++;
            builder.OpenComponent<LineSeries<Sample>>(0);
            builder.AddAttribute(1, nameof(LineSeries<>.Items), Counter.Count < 20 ? [.. Samples] : Samples);
            builder.AddAttribute(2, nameof(LineSeries<>.X), (Func<Sample, DateTime>)(static x => x.Time));
            builder.AddAttribute(3, nameof(LineSeries<>.Y), (Func<Sample, double>)(static x => x.Value));
            builder.CloseComponent();
        }
    }
}

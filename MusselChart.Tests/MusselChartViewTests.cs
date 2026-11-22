namespace MusselChart;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

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

    [Fact]
    public void StacksOfSeriesThatReadDifferentBuffersKeepTheOrderOfTheSeries()
    {
        var first = new ChartRingBuffer<Sample>(10);
        var second = new ChartRingBuffer<Sample>(10);
        first.Push(new Sample(Start, 1));
        second.Push(new Sample(Start, 2));
        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .AddChildContent<AreaSeries<Sample>>(series => series
                .Add(static p => p.Items, first)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)
                .Add(static p => p.StackGroup, "total"))
            .AddChildContent<AreaSeries<Sample>>(series => series
                .Add(static p => p.Items, second)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)
                .Add(static p => p.StackGroup, "total")));

        second.Push(new Sample(Start.AddSeconds(1), 20));
        cut.Render();
        first.Push(new Sample(Start.AddSeconds(1), 10));
        cut.Render();

        var lines = cut.FindAll("polyline");
        Assert.Equal("0,0 1,9", lines[0].GetAttribute("points"));
        Assert.Equal("0,2 1,29", lines[1].GetAttribute("points"));
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
    public void AThresholdWithAColorPaintsItsLineLabelAndBandWithoutALevelClass()
    {
        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .Add(static p => p.Thresholds, [new ChartThreshold { Value = 50, Level = ChartThresholdLevel.Critical, Color = "#8F3BB8", Label = "SLA" }])
            .Add(static p => p.ThresholdsMode, ChartThresholdsMode.Lines | ChartThresholdsMode.Bands)
            .AddChildContent<NumericAxis>(axis => axis.Add(static p => p.Minimum, 0).Add(static p => p.Maximum, 100))
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, CreateBuffer(10, 10))
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)));

        Assert.Equal("#8F3BB8", cut.Find(".mussel-threshold").GetAttribute("stroke"));
        Assert.Equal("#8F3BB8", cut.Find(".mussel-threshold-label").GetAttribute("fill"));
        Assert.Equal("#8F3BB8", cut.Find(".mussel-band").GetAttribute("fill"));
        Assert.Empty(cut.FindAll(".mussel-critical, .mussel-neutral"));
    }

    [Theory]
    [InlineData(10d, 0d, null, null)]
    [InlineData(5d, 5d, null, null)]
    [InlineData(Double.NegativeInfinity, null, null, null)]
    [InlineData(null, Double.NaN, null, null)]
    [InlineData(null, null, Double.NegativeInfinity, null)]
    [InlineData(null, null, Double.NaN, null)]
    [InlineData(null, null, null, Double.PositiveInfinity)]
    public void TheValueAxisDropsEndsThatAreOutOfOrderOrNotFinite(double? minimum, double? maximum, double? softMinimum, double? softMaximum)
    {
        var ends = RenderWithValueAxis(minimum, maximum, softMinimum, softMaximum);
        var plain = RenderWithValueAxis(null, null, null, null);

        Assert.DoesNotContain("NaN", ends.Markup, StringComparison.Ordinal);
        Assert.Equal(plain.Find(".mussel-plot").GetAttribute("transform"), ends.Find(".mussel-plot").GetAttribute("transform"));
        Assert.Equal(plain.FindAll(".mussel-axis text").Select(static x => x.TextContent), ends.FindAll(".mussel-axis text").Select(static x => x.TextContent));
    }

    [Theory]
    [InlineData(null, -5d, "-6", "-5")]
    [InlineData(20d, null, "20", "21")]
    public void TheValueAxisKeepsAnEndThatTheValuesAreBeyond(double? minimum, double? maximum, string first, string last)
    {
        var cut = RenderWithValueAxis(minimum, maximum, null, null);

        var labels = cut.FindAll(".mussel-axis text[text-anchor='end']").Select(static x => x.TextContent).ToList();
        Assert.Equal([first, last], [labels[0], labels[^1]]);
    }

    [Theory]
    [InlineData(9, 0)]
    [InlineData(5, 5)]
    [InlineData(1, 0)]
    public void ATimeAxisDropsEndsThatAreOutOfOrder(int minimum, int maximum)
    {
        var ends = RenderWithTimeAxis(Start.AddSeconds(minimum), Start.AddSeconds(maximum));
        var plain = RenderWithTimeAxis(null, null);

        Assert.Equal(plain.Find(".mussel-plot").GetAttribute("transform"), ends.Find(".mussel-plot").GetAttribute("transform"));
        Assert.Equal(plain.FindAll(".mussel-axis text").Select(static x => x.TextContent), ends.FindAll(".mussel-axis text").Select(static x => x.TextContent));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ATimeAxisFixedAtAnEndOfDateTimeKeepsASecond(bool last)
    {
        var cut = last ? RenderWithTimeAxis(DateTime.MaxValue, null) : RenderWithTimeAxis(null, DateTime.MinValue);

        Assert.DoesNotContain("NaN", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Infinity", cut.Markup, StringComparison.Ordinal);
        var scale = Double.Parse(cut.Find(".mussel-plot").GetAttribute("transform")!["matrix(".Length..].Split(' ')[0], CultureInfo.InvariantCulture);
        Assert.InRange(scale, 100, 1000);
    }

    [Fact]
    public void TheValueAxisKeepsAnEndWhereOneIsBelowItsPrecision()
    {
        Sample[] items = [new(Start, 2e16), new(Start.AddSeconds(1), 3e16)];

        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .Add(static p => p.Culture, CultureInfo.InvariantCulture)
            .AddChildContent<NumericAxis>(static axis => axis.Add(static p => p.Maximum, 1e16))
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)));

        Assert.DoesNotContain("NaN", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Infinity", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("10,000T", cut.FindAll(".mussel-axis text[text-anchor='end']").Select(static x => x.TextContent));
    }

    [Fact]
    public void AWindowSizeLeavesOutTheMinimumEvenOneAfterTheMaximum()
    {
        var ends = RenderWithTimeAxis(Start.AddSeconds(9), Start.AddSeconds(6), TimeSpan.FromSeconds(5));
        var window = RenderWithTimeAxis(null, Start.AddSeconds(6), TimeSpan.FromSeconds(5));

        Assert.Equal(window.Find(".mussel-plot").GetAttribute("transform"), ends.Find(".mussel-plot").GetAttribute("transform"));
        Assert.Equal(window.FindAll(".mussel-axis text").Select(static x => x.TextContent), ends.FindAll(".mussel-axis text").Select(static x => x.TextContent));
    }

    [Theory]
    [InlineData(null, -10, "23:59:49", "23:59:50")]
    [InlineData(20, null, "00:00:20", "00:00:21")]
    public void ATimeAxisKeepsAnEndThatThePointsAreBeyond(int? minimum, int? maximum, string first, string last)
    {
        var cut = RenderWithTimeAxis(minimum is { } low ? Start.AddSeconds(low) : null, maximum is { } high ? Start.AddSeconds(high) : null);

        var transform = cut.Find(".mussel-plot").GetAttribute("transform")!;
        var labels = cut.FindAll(".mussel-axis text[text-anchor='middle']").Select(static x => x.TextContent).ToList();
        Assert.True(Transform(transform, 1, 0).X > Transform(transform, 0, 0).X);
        Assert.Equal([first, last], [labels[0], labels[^1]]);
    }

    [Fact]
    public void TheEndsOfATimeAxisStayWhereTheyAreSetForDailyBars()
    {
        Sample[] items = [.. Enumerable.Range(0, 7).Select(static x => new Sample(Start.AddDays(x), x + 1))];

        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .AddChildContent<DateTimeAxis>(static axis => axis.Add(static p => p.Minimum, Start).Add(static p => p.Maximum, Start.AddDays(6)))
            .AddChildContent<BarSeries<Sample>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)));

        var (start, end, _) = PlotEnds(cut);
        Assert.Equal(0, start, 1d);
        Assert.Equal(TimeSpan.FromDays(6).TotalSeconds, end, 1d);
    }

    [Fact]
    public void AWindowOfBubblesKeepsItsSizeAndEndsPastTheLastBubble()
    {
        Sample[] items = [.. Enumerable.Range(0, 10).Select(static x => new Sample(Start.AddSeconds(x), x, 100))];

        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .AddChildContent<DateTimeAxis>(static axis => axis.Add(static p => p.WindowSize, TimeSpan.FromSeconds(60)))
            .AddChildContent<BubbleSeries<Sample>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)
                .Add(static p => p.Size, static x => x.Other)));

        var (start, end, scale) = PlotEnds(cut);
        var last = cut.FindAll(".mussel-markers circle")[^1];
        Assert.Equal(60, end - start, 0.01);
        Assert.Equal("9", last.GetAttribute("data-x"));
        Assert.Equal(9 + ((Number(last, "r") + 1) / scale), end, 0.01);
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
    public void SeriesBeyondThePaletteAreGray()
    {
        Sample[] items = [new(Start, 1), new(Start.AddSeconds(1), 2)];
        void Line(ComponentParameterCollectionBuilder<LineSeries<Sample>> series) => series
            .Add(static p => p.Items, items)
            .Add(static p => p.X, static x => x.Time)
            .Add(static p => p.Y, static x => x.Value);

        var few = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.Palette, ["#AA0000", "#00AA00"])
            .AddChildContent<LineSeries<Sample>>(Line)
            .AddChildContent<LineSeries<Sample>>(Line)
            .AddChildContent<LineSeries<Sample>>(Line));
        var empty = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.Palette, Array.Empty<string>())
            .AddChildContent<LineSeries<Sample>>(Line));

        Assert.Equal(["#AA0000", "#00AA00", ChartColor.Other], few.FindAll(".mussel-series").Select(static x => x.GetAttribute("data-color")));
        Assert.Equal([ChartColor.Other], empty.FindAll(".mussel-series").Select(static x => x.GetAttribute("data-color")));
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
    public void MarkersInTheWindowKeepTheirAttributesWhenAPointIsAdded()
    {
        var buffer = CreateBuffer(100, 6);
        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .AddChildContent<DateTimeAxis>(static axis => axis.Add(static p => p.WindowSize, TimeSpan.FromSeconds(5)))
            .AddChildContent<NumericAxis>(static axis => axis.Add(static p => p.Minimum, 0).Add(static p => p.Maximum, 10))
            .AddChildContent<ScatterSeries<Sample>>(series => series
                .Add(static p => p.Items, buffer)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)));
        var origin = cut.Find(".mussel-markers").GetAttribute("transform");
        var kept = cut.FindAll(".mussel-markers circle").Skip(1).Select(static x => x.OuterHtml).ToArray();

        buffer.Push(new Sample(Start.AddSeconds(6), 5));
        cut.Render();

        var markers = cut.FindAll(".mussel-markers circle");
        Assert.Equal(kept, markers.SkipLast(1).Select(static x => x.OuterHtml));
        Assert.Equal("6", markers[^1].GetAttribute("data-x"));
        Assert.NotEqual(origin, cut.Find(".mussel-markers").GetAttribute("transform"));
        AssertMarkersSitOnThePlot(cut);
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
        var svg = cut.Find("svg.mussel-chart-svg");
        Assert.Equal(6, bars.Count);
        Assert.Equal(((Start.AddSeconds(3).Ticks - DateTime.UnixEpoch.Ticks) / TimeSpan.TicksPerSecond).ToString(CultureInfo.InvariantCulture), svg.GetAttribute("data-base"));
        Assert.Equal(["0", "1", "2"], bars.Take(3).Select(static x => x.GetAttribute("x")));
        Assert.Equal(["4", "5", "6"], bars.Take(3).Select(static x => x.GetAttribute("data-v")));
        Assert.Equal("4", svg.GetAttribute("data-value-base"));
        Assert.InRange(Double.Parse(bars[3].GetAttribute("y")!, CultureInfo.InvariantCulture), 0, 1);
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
    public void LabelsOfANarrowValueAxisKeepTheDecimalsThatTellThemApart()
    {
        Sample[] large = [new(Start, 1234567890), new(Start.AddSeconds(1), 1234567955)];
        Sample[] small = [new(Start, 20.001), new(Start.AddSeconds(1), 20.009)];

        var largeChart = RenderValues(large, ChartLegendValues.Last);
        var smallChart = RenderValues(small, ChartLegendValues.None);

        var largeLabels = largeChart.FindAll(".mussel-axis text[text-anchor='end']").Select(static x => x.TextContent).ToList();
        Assert.Equal(["1.23456788B", "1.2345679B", "1.23456792B", "1.23456794B", "1.23456796B"], largeLabels);
        Assert.Equal("1.23B", largeChart.FindAll("td")[1].TextContent);
        var smallLabels = smallChart.FindAll(".mussel-axis text[text-anchor='end']").Select(static x => x.TextContent).ToList();
        Assert.Equal(["20", "20.002", "20.004", "20.006", "20.008", "20.01"], smallLabels);
    }

    [Theory]
    [InlineData(9d, "0,2,4,6,8,10")]
    [InlineData(1d, "0,0.2,0.4,0.6,0.8,1")]
    [InlineData(50000d, "0,10K,20K,30K,40K,50K")]
    public void LabelsOfAnOrdinaryValueAxisAreWrittenAsBefore(double maximum, string expected)
    {
        Sample[] items = [new(Start, 0), new(Start.AddSeconds(1), maximum)];

        var cut = RenderValues(items, ChartLegendValues.None);

        Assert.Equal(expected.Split(','), cut.FindAll(".mussel-axis text[text-anchor='end']").Select(static x => x.TextContent));
    }

    [Fact]
    public void LabelsOfANarrowNumericXAxisKeepTheDecimalsThatTellThemApart()
    {
        PointItem[] points = [new(20.001, 1), new(20.009, 2)];

        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.Culture, CultureInfo.GetCultureInfo("en-US"))
            .AddChildContent<ScatterSeries<PointItem>>(series => series
                .Add(static p => p.Items, points)
                .Add(static p => p.XValue, static x => x.X)
                .Add(static p => p.Y, static x => x.Y)));

        var labels = cut.FindAll(".mussel-axis text[text-anchor='middle']").Select(static x => x.TextContent).ToList();
        Assert.NotEmpty(labels);
        Assert.Equal(labels.Count, labels.Distinct(StringComparer.Ordinal).Count());
        Assert.All(labels, static x => Assert.StartsWith("20.00", x, StringComparison.Ordinal));
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
        var rects = cut.FindAll(".mussel-bars rect");
        Assert.Equal(2, rects.Count);
        Assert.All(rects, rect =>
        {
            var y = Double.Parse(rect.GetAttribute("y")!, CultureInfo.InvariantCulture);
            var height = Double.Parse(rect.GetAttribute("height")!, CultureInfo.InvariantCulture);
            var top = Double.Parse(rect.GetAttribute("data-v")!, CultureInfo.InvariantCulture) - valueBase;
            Assert.InRange(y + height - top, -0.01, 0.01);
            Assert.InRange((float)y + (float)height - top, -0.01, 0.01);
            Assert.True(height > 0);
            Assert.True(y < 1234567800 - valueBase);
        });
    }

    [Fact]
    public async Task DisposingTellsTheScriptAndReleasesIt()
    {
        var instance = new LateModule();
        var runtime = new LateRuntime { Module = { Returned = instance } };
        Services.AddSingleton<IJSRuntime>(runtime);
        var cut = Render<MusselChartView>(parameters => parameters.Add(static p => p.Height, 240));
        await cut.InvokeAsync(() => runtime.Import.SetResult(runtime.Module));
        await cut.WaitForAssertionAsync(() => Assert.Equal(["attach"], runtime.Module.Calls));

        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());

        Assert.Equal(["attach"], runtime.Module.Calls);
        Assert.Equal(["dispose"], instance.Calls);
        Assert.True(instance.Released);
        Assert.True(runtime.Module.Released);
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
        var origin = cut.Find(".mussel-markers").GetAttribute("transform")!["translate(".Length..^1].Split(' ');
        Assert.All(origin, static x => Assert.InRange(Double.Parse(x, CultureInfo.InvariantCulture), -1000, 1000));
        Assert.InRange(Number(marker, "cx"), -1000, 1000);
        Assert.InRange(Number(marker, "cy"), -1000, 1000);
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

        Assert.Equal(3, cut.FindAll(".mussel-markers circle").Count);
        AssertMarkersSitOnThePlot(cut);
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
        Assert.Equal(Number(live, "cx"), x, 0.06);
        Assert.Equal(Number(live, "cy"), y, 0.06);
    }

    [Fact]
    public void TheLivePointIsLeftOutWhenTheLastPointIsOutOfTheAxes()
    {
        Sample[] above = [new(Start, 50), new(Start.AddSeconds(1), 150)];
        var high = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .AddChildContent<NumericAxis>(static axis => axis.Add(static p => p.Minimum, 0).Add(static p => p.Maximum, 100))
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, above)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)
                .Add(static p => p.LiveIndicatorVisible, true)));
        var late = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .AddChildContent<DateTimeAxis>(static axis => axis.Add(static p => p.WindowSize, TimeSpan.FromMinutes(3)).Add(static p => p.Maximum, Start.AddSeconds(10)))
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, CreateBuffer(100, 40))
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)
                .Add(static p => p.LiveIndicatorVisible, true)));

        Assert.Empty(high.FindAll(".mussel-live"));
        Assert.Empty(late.FindAll(".mussel-live"));
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
    public void AChangedSelectorReadsEveryPointOfTheBufferAgain()
    {
        var buffer = new ChartRingBuffer<Sample>(10);
        for (var i = 0; i < 3; i++)
        {
            buffer.Push(new Sample(Start.AddSeconds(i), i, 10 * i));
        }

        var cut = RenderLine(buffer);
        var instance = cut.FindComponent<LineSeries<Sample>>().Instance;

        cut.Render(parameters => parameters.AddChildContent<LineSeries<Sample>>(series => series
            .Add(static p => p.Items, buffer)
            .Add(static p => p.X, static x => x.Time)
            .Add(static p => p.Y, static x => x.Other)));
        buffer.Push(new Sample(Start.AddSeconds(3), 3, 30));
        cut.Render();

        Assert.Same(instance, cut.FindComponent<LineSeries<Sample>>().Instance);
        Assert.Equal("0,0 1,10 2,20 3,30", cut.Find("polyline").GetAttribute("points"));
    }

    [Fact]
    public void ChangedRangesAndSizesReadEveryPointOfTheBufferAgain()
    {
        var buffer = new ChartRingBuffer<Sample>(10);
        buffer.Push(new Sample(Start, 1, 5));
        buffer.Push(new Sample(Start.AddSeconds(1), 2, 6));
        var ranges = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .AddChildContent<RangeBarSeries<Sample>>(series => series
                .Add(static p => p.Items, buffer)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Low, static x => x.Value)
                .Add(static p => p.High, static x => x.Other)));
        var bubbles = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .AddChildContent<BubbleSeries<Sample>>(series => series
                .Add(static p => p.Items, buffer)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)
                .Add(static p => p.Size, static x => x.Value)));

        ranges.FindComponent<RangeBarSeries<Sample>>().Render(static parameters => parameters.Add(static p => p.High, static x => x.Other * 10));
        bubbles.FindComponent<BubbleSeries<Sample>>().Render(static parameters => parameters.Add(static p => p.Size, static x => x.Other));

        Assert.Equal(["50", "60"], ranges.FindAll(".mussel-bars rect").Select(static x => x.GetAttribute("data-v")));
        Assert.Equal(["6", "5"], bubbles.FindAll(".mussel-markers circle").Select(static x => x.GetAttribute("data-size")));
    }

    [Fact]
    public void AChangedTimeZoneReadsEveryPointOfTheBufferAgain()
    {
        var local = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var buffer = new ChartRingBuffer<Sample>(10);
        for (var i = 0; i < 3; i++)
        {
            buffer.Push(new Sample(local.AddSeconds(i), i));
        }

        var cut = RenderLine(buffer);

        cut.Render(static parameters => parameters.Add(static p => p.TimeZone, TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo")));
        buffer.Push(new Sample(local.AddSeconds(3), 3));
        cut.Render();

        var utc = new DateTime(2025, 12, 31, 15, 0, 0, DateTimeKind.Utc);
        Assert.Equal(((utc.Ticks - DateTime.UnixEpoch.Ticks) / TimeSpan.TicksPerSecond).ToString(CultureInfo.InvariantCulture), cut.Find("svg.mussel-chart-svg").GetAttribute("data-base"));
        Assert.Equal("0,0 1,1 2,2 3,3", cut.Find("polyline").GetAttribute("points"));
    }

    [Fact]
    public void SelectorsMadeAgainOnEveryRenderDoNotReadThePointsAgain()
    {
        var buffer = CreateBuffer(100, 10);
        void Content(RenderTreeBuilder builder)
        {
            builder.OpenComponent<SelectorSeries>(0);
            builder.AddAttribute(1, nameof(SelectorSeries.Samples), buffer);
            builder.CloseComponent();
        }

        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .Add(static p => p.ChildContent, Content));
        var series = cut.FindComponent<SelectorSeries>().Instance;
        var reads = series.Reads;

        cut.Render();
        cut.Render();

        Assert.Equal(10, reads);
        Assert.Equal(reads, series.Reads);
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

    [Fact]
    public void TimeLabelsOfALongFormatDoNotOverlap()
    {
        var first = new DateTime(2026, 7, 1, 9, 0, 0, DateTimeKind.Utc);
        var items = Enumerable.Range(0, 9).Select(x => new Sample(first.AddHours(x), x)).ToArray();

        var formatted = RenderTimes(items, static axis => axis.Add(static p => p.LabelFormat, "yyyy/MM/dd HH:mm"));
        var formatter = RenderTimes(items, static axis => axis.Add(static p => p.LabelFormatter, static x => x.ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture)));

        AssertApart(formatted);
        AssertApart(formatter);

        static void AssertApart(IRenderedComponent<MusselChartView> cut)
        {
            var labels = cut.FindAll(".mussel-axis text[text-anchor='middle']").Select(static x => (X: Number(x, "x"), Width: ChartText.Measure(x.TextContent, 11))).ToList();
            Assert.True(labels.Count > 1);
            for (var i = 1; i < labels.Count; i++)
            {
                Assert.True(labels[i].X - labels[i - 1].X >= (labels[i].Width + labels[i - 1].Width) / 2, $"{labels[i - 1].X} {labels[i].X}");
            }
        }
    }

    [Theory]
    [InlineData("en-US", 30, "yyyy/M/d HH:mm")]
    [InlineData("en-US", 43200, "yyyy/M/d")]
    [InlineData("de-DE", 43200, "d/M/yyyy")]
    public void TooltipsShowTheYearWhenTheRangeCrossesAYear(string culture, int minutes, string format)
    {
        var first = new DateTime(2025, 12, 31, 22, 0, 0, DateTimeKind.Utc);
        var items = Enumerable.Range(0, 5).Select(x => new Sample(first.AddMinutes(x * minutes), x)).ToArray();

        var cut = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .Add(static p => p.Culture, new CultureInfo(culture))
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)));

        Assert.Equal(format, cut.Find(".mussel-chart").GetAttribute("data-time-format"));
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

    [Fact]
    public void TheChartTellsTheScriptHowToTrack()
    {
        Sample[] items = [new(Start, 1), new(Start.AddSeconds(1), 2)];
        var shown = Render<MusselChartView>(parameters => parameters
            .AddChildContent<LineSeries<Sample>>(series => series.Add(static p => p.Items, items).Add(static p => p.X, static x => x.Time).Add(static p => p.Y, static x => x.Value)));
        var hidden = Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TrackballMode, ChartTrackballMode.None)
            .Add(static p => p.TooltipVisible, false)
            .AddChildContent<LineSeries<Sample>>(series => series.Add(static p => p.Items, items).Add(static p => p.X, static x => x.Time).Add(static p => p.Y, static x => x.Value)));

        Assert.Equal(["hover", "true"], [shown.Find(".mussel-chart").GetAttribute("data-trackball"), shown.Find(".mussel-chart").GetAttribute("data-tooltip")]);
        Assert.Equal(["none", "false"], [hidden.Find(".mussel-chart").GetAttribute("data-trackball"), hidden.Find(".mussel-chart").GetAttribute("data-tooltip")]);
    }

    private IRenderedComponent<MusselChartView> RenderLine(ChartRingBuffer<Sample> buffer) =>
        Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, buffer)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)
                .Add(static p => p.Title, "Value")));

    private IRenderedComponent<MusselChartView> RenderTimes(Sample[] items, Action<ComponentParameterCollectionBuilder<DateTimeAxis>> axis) =>
        Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .Add(static p => p.Culture, CultureInfo.InvariantCulture)
            .AddChildContent(axis)
            .AddChildContent<NumericAxis>(static numeric => numeric.Add(static p => p.Minimum, 0).Add(static p => p.Maximum, 100))
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)));

    private IRenderedComponent<MusselChartView> RenderValues(Sample[] items, ChartLegendValues legendValues) =>
        Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .Add(static p => p.Culture, CultureInfo.GetCultureInfo("en-US"))
            .Add(static p => p.LegendVisible, legendValues != ChartLegendValues.None)
            .Add(static p => p.LegendMode, ChartLegendMode.Table)
            .Add(static p => p.LegendValues, legendValues)
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, items)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)
                .Add(static p => p.Title, "Value")));

    private IRenderedComponent<MusselChartView> RenderWithValueAxis(double? minimum, double? maximum, double? softMinimum, double? softMaximum) =>
        Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .AddChildContent<NumericAxis>(axis => axis
                .Add(static p => p.Minimum, minimum)
                .Add(static p => p.Maximum, maximum)
                .Add(static p => p.SoftMinimum, softMinimum)
                .Add(static p => p.SoftMaximum, softMaximum))
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, CreateBuffer(10, 10))
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)));

    private IRenderedComponent<MusselChartView> RenderWithTimeAxis(DateTime? minimum, DateTime? maximum, TimeSpan? window = null) =>
        Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .Add(static p => p.Culture, CultureInfo.InvariantCulture)
            .AddChildContent<DateTimeAxis>(axis => axis.Add(static p => p.Minimum, minimum).Add(static p => p.Maximum, maximum).Add(static p => p.WindowSize, window))
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, CreateBuffer(10, 10))
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)));

    private static (double X, double Y) Transform(string transform, double x, double y)
    {
        var m = transform["matrix(".Length..^1].Split(' ').Select(static v => Double.Parse(v, CultureInfo.InvariantCulture)).ToArray();
        return ((m[0] * x) + (m[2] * y) + m[4], (m[1] * x) + (m[3] * y) + m[5]);
    }

    private static (double X, double Y) Translate(string transform, double x, double y)
    {
        var t = transform["translate(".Length..^1].Split(' ').Select(static v => Double.Parse(v, CultureInfo.InvariantCulture)).ToArray();
        return (x + t[0], y + t[1]);
    }

    private static (double Start, double End, double Scale) PlotEnds(IRenderedComponent<MusselChartView> cut)
    {
        var transform = cut.Find(".mussel-plot").GetAttribute("transform")!;
        var grid = cut.Find(".mussel-grid line");
        var origin = Transform(transform, 0, 0).X;
        var scale = Transform(transform, 1, 0).X - origin;
        return ((Number(grid, "x1") - origin) / scale, (Number(grid, "x2") - origin) / scale, scale);
    }

    private static void AssertMarkersSitOnThePlot(IRenderedComponent<MusselChartView> cut)
    {
        var transform = cut.Find(".mussel-plot").GetAttribute("transform")!;
        var origin = cut.Find(".mussel-markers").GetAttribute("transform")!;
        Assert.All(cut.FindAll(".mussel-markers circle"), marker =>
        {
            var (x, y) = Transform(transform, Number(marker, "data-x"), Number(marker, "data-y"));
            var (cx, cy) = Translate(origin, Number(marker, "cx"), Number(marker, "cy"));
            Assert.Equal(x, cx, 0.01);
            Assert.Equal(y, cy, 0.01);
        });
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

    public sealed class SelectorSeries : ComponentBase
    {
        [Parameter]
        public IReadOnlyList<Sample> Samples { get; set; } = [];

        public int Reads { get; private set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<LineSeries<Sample>>(0);
            builder.AddAttribute(1, nameof(LineSeries<>.Items), Samples);
            builder.AddAttribute(2, nameof(LineSeries<>.X), (Func<Sample, DateTime>)(static x => x.Time));
            builder.AddAttribute(3, nameof(LineSeries<>.Y), (Func<Sample, double>)(x => Read(x.Value)));
            builder.CloseComponent();
        }

        private double Read(double value)
        {
            Reads++;
            return value;
        }
    }
}

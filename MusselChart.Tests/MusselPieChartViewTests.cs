namespace MusselChart;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using MusselChart.Internal;

public sealed class MusselPieChartViewTests : BunitContext
{
    private static readonly ChartEntry[] Entries =
    [
        new("A", 50),
        new("B", 30),
        new("C", 20)
    ];

    public MusselPieChartViewTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void DrawsASliceForEachValueAndTheTotalInTheHole()
    {
        var cut = Render<MusselPieChartView>(parameters => parameters
            .Add(static p => p.Items, Entries)
            .Add(static p => p.CenterLabel, "Total")
            .Add(static p => p.Culture, CultureInfo.InvariantCulture));

        Assert.Equal(3, cut.FindAll(".mussel-slice").Count);
        Assert.Equal("100", cut.Find(".mussel-pie-center").TextContent);
        Assert.Equal("Total", cut.Find(".mussel-pie-caption").TextContent);
        Assert.Equal(["A", "B", "C"], cut.FindAll(".mussel-view-legend-name").Select(static x => x.TextContent));
    }

    [Fact]
    public void SlicesTakeThePaletteInDataOrderAndAnExplicitColorFirst()
    {
        var cut = Render<MusselPieChartView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 1), new ChartEntry("B", 1, "#112233"), new ChartEntry("C", 1)])
            .Add(static p => p.Palette, ["#AA0000", "#00AA00", "#0000AA"]));

        Assert.Equal(["#AA0000", "#112233", "#0000AA"], cut.FindAll(".mussel-slice").Select(static x => x.GetAttribute("fill")));
    }

    [Fact]
    public void ValuesThatAreNotPositiveAreSkipped()
    {
        var cut = Render<MusselPieChartView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 1), new ChartEntry("B", 0), new ChartEntry("C", -5), new ChartEntry("D", Double.NaN)]));

        Assert.Single(cut.FindAll(".mussel-slice"));
    }

    [Fact]
    public void SmallestItemsMergeOverTheLimit()
    {
        var cut = Render<MusselPieChartView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 40), new ChartEntry("B", 30), new ChartEntry("C", 20), new ChartEntry("D", 10)])
            .Add(static p => p.MaxSlices, 3)
            .Add(static p => p.OthersLabel, "Rest")
            .Add(static p => p.LegendValueMode, ChartLegendValueMode.Value)
            .Add(static p => p.Culture, CultureInfo.InvariantCulture));

        var slices = cut.FindAll(".mussel-slice");
        Assert.Equal(3, slices.Count);
        Assert.Equal(ChartColor.Other, slices[2].GetAttribute("fill"));
        Assert.Equal(["A", "B", "Rest"], cut.FindAll(".mussel-view-legend-name").Select(static x => x.TextContent));
        Assert.Equal(["40", "30", "30"], cut.FindAll(".mussel-view-legend-value").Select(static x => x.TextContent));
    }

    [Fact]
    public void SmallSharesMergeBelowTheThreshold()
    {
        var cut = Render<MusselPieChartView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 90), new ChartEntry("B", 6), new ChartEntry("C", 4)])
            .Add(static p => p.OthersThreshold, 0.1));

        Assert.Equal(2, cut.FindAll(".mussel-slice").Count);
    }

    [Fact]
    public void SortKeepsTheColorsOfTheData()
    {
        var cut = Render<MusselPieChartView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 1), new ChartEntry("B", 3)])
            .Add(static p => p.SortMode, ChartSortMode.Descending)
            .Add(static p => p.Palette, ["#AA0000", "#00AA00"]));

        Assert.Equal(["#00AA00", "#AA0000"], cut.FindAll(".mussel-slice").Select(static x => x.GetAttribute("fill")));
    }

    [Fact]
    public void LegendHidesASliceAndKeepsItsPlace()
    {
        var cut = Render<MusselPieChartView>(parameters => parameters
            .Add(static p => p.Items, Entries)
            .Add(static p => p.LegendToggleEnabled, true)
            .Add(static p => p.LegendValueMode, ChartLegendValueMode.Percent)
            .Add(static p => p.Culture, CultureInfo.InvariantCulture));

        cut.FindAll(".mussel-view-legend-item")[0].Click();

        Assert.Equal(2, cut.FindAll(".mussel-slice").Count);
        Assert.Equal("50", cut.Find(".mussel-pie-center").TextContent);
        var items = cut.FindAll(".mussel-view-legend-item");
        Assert.Equal(3, items.Count);
        Assert.Contains("mussel-view-legend-hidden", items[0].ClassName, StringComparison.Ordinal);
        Assert.Equal("-", items[0].QuerySelector(".mussel-view-legend-value")!.TextContent);

        items[0].Click();

        Assert.Equal(3, cut.FindAll(".mussel-slice").Count);
    }

    [Fact]
    public void TheLastSliceCannotBeHidden()
    {
        var cut = Render<MusselPieChartView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 1), new ChartEntry("B", 1)])
            .Add(static p => p.LegendToggleEnabled, true));

        cut.FindAll(".mussel-view-legend-item")[0].Click();
        cut.FindAll(".mussel-view-legend-item")[1].Click();

        Assert.Single(cut.FindAll(".mussel-slice"));
    }

    [Fact]
    public void LabelsThatDoNotFitInsideAreLeftOut()
    {
        var cut = Render<MusselPieChartView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 90), new ChartEntry("B", 1)])
            .Add(static p => p.LabelsMode, ChartPieLabelsMode.Inside)
            .Add(static p => p.Culture, CultureInfo.InvariantCulture));

        Assert.Single(cut.FindAll(".mussel-pie-label"));
    }

    [Fact]
    public void LabelsInsideUseTheContrastColor()
    {
        var cut = Render<MusselPieChartView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 1, "#FFFFFF"), new ChartEntry("B", 1, "#000000")])
            .Add(static p => p.InnerRadius, 0)
            .Add(static p => p.LabelsMode, ChartPieLabelsMode.Inside));

        Assert.Equal(["#0B0B0B", "#FFFFFF"], cut.FindAll(".mussel-pie-label").Select(static x => x.GetAttribute("fill")));
    }

    [Fact]
    public void LabelsOutsideGetLeaderLines()
    {
        var cut = Render<MusselPieChartView>(parameters => parameters
            .Add(static p => p.Items, Entries)
            .Add(static p => p.LabelsMode, ChartPieLabelsMode.Outside)
            .Add(static p => p.LabelContent, ChartPieLabelContent.Label));

        Assert.Equal(3, cut.FindAll(".mussel-pie-leader").Count);
        var labels = cut.FindAll(".mussel-pie-outside");
        Assert.Equal(["A", "B", "C"], labels.Select(static x => x.TextContent).Order(StringComparer.Ordinal));
        Assert.Contains(labels, static x => x.GetAttribute("text-anchor") == "start");
        Assert.Contains(labels, static x => x.GetAttribute("text-anchor") == "end");
    }

    [Fact]
    public void PieWithoutAHoleHasNoCenter()
    {
        var cut = Render<MusselPieChartView>(parameters => parameters
            .Add(static p => p.Items, Entries)
            .Add(static p => p.InnerRadius, 0));

        Assert.Empty(cut.FindAll(".mussel-pie-center"));
    }

    [Fact]
    public void CenterCanBeTurnedOffOrFixed()
    {
        var hidden = Render<MusselPieChartView>(parameters => parameters
            .Add(static p => p.Items, Entries)
            .Add(static p => p.CenterValueMode, ChartCenterValueMode.None));
        var value = Render<MusselPieChartView>(parameters => parameters
            .Add(static p => p.Items, Entries)
            .Add(static p => p.CenterValue, 1234)
            .Add(static p => p.CenterValueFormat, "N0")
            .Add(static p => p.Culture, CultureInfo.InvariantCulture));

        Assert.Empty(hidden.FindAll(".mussel-pie-center"));
        Assert.Equal("1,234", value.Find(".mussel-pie-center").TextContent);
    }

    [Theory]
    [InlineData(Double.NaN)]
    [InlineData(Double.PositiveInfinity)]
    [InlineData(Double.NegativeInfinity)]
    public void ACenterValueThatIsNotFiniteHidesTheCenter(double center)
    {
        var cut = Render<MusselPieChartView>(parameters => parameters
            .Add(static p => p.Items, Entries)
            .Add(static p => p.CenterValue, center)
            .Add(static p => p.CenterLabel, "Total"));

        Assert.Empty(cut.FindAll(".mussel-pie-center"));
        Assert.Empty(cut.FindAll(".mussel-pie-caption"));
    }

    [Fact]
    public void ANearlyFullSliceWithoutGapsKeepsTheHole()
    {
        ChartEntry[] items = [new("A", 99999), new("B", 1)];

        var cut = Render<MusselPieChartView>(parameters => parameters
            .Add(static p => p.Items, items)
            .Add(static p => p.Height, 240)
            .Add(static p => p.SegmentGap, 0));

        var path = cut.FindAll(".mussel-slice")[0].GetAttribute("d") ?? string.Empty;
        Assert.Equal(2, path.Count(static x => x == 'M'));
    }

    [Fact]
    public void TheCenterFormatWinsOverTheFormatterOfTheValues()
    {
        var cut = Render<MusselPieChartView>(parameters => parameters
            .Add(static p => p.Items, Entries)
            .Add(static p => p.CenterValue, 1234)
            .Add(static p => p.ValueFormatter, static _ => "value")
            .Add(static p => p.CenterValueFormat, "N0")
            .Add(static p => p.Culture, CultureInfo.InvariantCulture));

        Assert.Equal("1,234", cut.Find(".mussel-pie-center").TextContent);
    }

    [Fact]
    public void ShowsEmptyTextWithoutData()
    {
        var cut = Render<MusselPieChartView>(parameters => parameters
            .Add(static p => p.Items, Array.Empty<ChartEntry>())
            .Add(static p => p.EmptyText, "No data"));

        Assert.Equal("No data", cut.Find(".mussel-empty text").TextContent);
        Assert.Empty(cut.FindAll(".mussel-slice"));
    }

    [Fact]
    public void TooltipOfASliceCarriesItsValueAndShare()
    {
        var cut = Render<MusselPieChartView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 75), new ChartEntry("B", 25)])
            .Add(static p => p.Culture, CultureInfo.InvariantCulture));

        var tip = cut.FindAll(".mussel-slice")[0].GetAttribute("data-tip");
        Assert.Contains("\"A\"", tip, StringComparison.Ordinal);
        Assert.Contains("75", tip, StringComparison.Ordinal);
    }

    [Fact]
    public void AttributesGoToTheRootElement()
    {
        var cut = Render<MusselPieChartView>(parameters => parameters
            .Add(static p => p.Items, Entries)
            .AddUnmatched("class", "mine")
            .AddUnmatched("id", "pie"));

        var root = cut.Find("#pie");
        Assert.Contains("mussel-view", root.ClassName, StringComparison.Ordinal);
        Assert.Contains("mine", root.ClassName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResizeChangesTheViewBox()
    {
        var cut = Render<MusselPieChartView>(parameters => parameters
            .Add(static p => p.Items, Entries));

        await cut.InvokeAsync(() => cut.Instance.OnResize(320.4));

        Assert.Equal("0 0 320 240", cut.Find("svg.mussel-view-svg").GetAttribute("viewBox"));
    }

    [Fact]
    public async Task StaysHiddenWithItsHeightUntilTheWidthIsMeasured()
    {
        var cut = Render<MusselPieChartView>(parameters => parameters
            .Add(static p => p.Items, Entries));

        Assert.Contains("mussel-unmeasured", cut.Find(".mussel-view-plot").ClassList);
        Assert.Equal("240", cut.Find("svg.mussel-view-svg").GetAttribute("height"));

        await cut.InvokeAsync(() => cut.Instance.OnResize(320));

        Assert.DoesNotContain("mussel-unmeasured", cut.Find(".mussel-view-plot").ClassList);
        Assert.Equal("240", cut.Find("svg.mussel-view-svg").GetAttribute("height"));
    }

    [Fact]
    public void PrerenderingStaysHiddenUntilTheChartStarts()
    {
        SetRendererInfo(new RendererInfo("Static", false));
        var cut = Render<MusselPieChartView>(parameters => parameters
            .SetAssignedRenderMode(RenderMode.InteractiveServer)
            .Add(static p => p.Items, Entries));

        Assert.Contains("mussel-prerendering", cut.Find(".mussel-view-plot").ClassList);
        Assert.DoesNotContain("mussel-unmeasured", cut.Find(".mussel-view-plot").ClassList);
    }

    [Fact]
    public void HalfDonutSitsOnItsFlatSide()
    {
        var layout = new PieLayout();

        layout.Update(400, 200, new PieLayoutOptions { StartAngle = 270, SweepAngle = 180, InnerRadius = 0.5, Culture = CultureInfo.InvariantCulture }, Entries);

        Assert.Equal(192, layout.CenterY, 3);
        Assert.Equal(184, layout.OuterRadius, 3);
        Assert.Equal(92, layout.InnerRadius, 3);
    }

    [Fact]
    public async Task TheScriptThatArrivesAfterTheViewIsGoneIsReleased()
    {
        var runtime = new LateRuntime();
        Services.AddSingleton<IJSRuntime>(runtime);
        var cut = Render<MusselPieChartView>(parameters => parameters.Add(static p => p.Items, Entries));

        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());
        await cut.InvokeAsync(() => runtime.Import.SetResult(runtime.Module));

        await cut.WaitForAssertionAsync(() => Assert.True(runtime.Module.Released));
        Assert.Empty(runtime.Module.Calls);
    }

    [Fact]
    public async Task DisposingTellsTheScriptAndReleasesIt()
    {
        var instance = new LateModule();
        var runtime = new LateRuntime { Module = { Returned = instance } };
        Services.AddSingleton<IJSRuntime>(runtime);
        var cut = Render<MusselPieChartView>(parameters => parameters.Add(static p => p.Items, Entries));
        await cut.InvokeAsync(() => runtime.Import.SetResult(runtime.Module));
        await cut.WaitForAssertionAsync(() => Assert.Equal(["attach"], runtime.Module.Calls));

        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());

        Assert.Equal(["attach"], runtime.Module.Calls);
        Assert.Equal(["dispose"], instance.Calls);
        Assert.True(instance.Released);
        Assert.True(runtime.Module.Released);
    }

    [Fact]
    public async Task AFailedDisposeStillReleasesTheScript()
    {
        var instance = new LateModule { Fail = "dispose" };
        var runtime = new LateRuntime { Module = { Returned = instance } };
        Services.AddSingleton<IJSRuntime>(runtime);
        var cut = Render<MusselPieChartView>(parameters => parameters.Add(static p => p.Items, Entries));
        await cut.InvokeAsync(() => runtime.Import.SetResult(runtime.Module));
        await cut.WaitForAssertionAsync(() => Assert.Equal(["attach"], runtime.Module.Calls));

        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());

        Assert.Equal(["attach"], runtime.Module.Calls);
        Assert.Equal(["dispose"], instance.Calls);
        Assert.True(instance.Released);
        Assert.True(runtime.Module.Released);
    }

    [Fact]
    public async Task DisposingWhileTheScriptIsAttachingReleasesWhatAttachReturns()
    {
        var instance = new LateModule();
        var runtime = new LateRuntime { Module = { Hold = "attach", Returned = instance } };
        Services.AddSingleton<IJSRuntime>(runtime);
        var cut = Render<MusselPieChartView>(parameters => parameters.Add(static p => p.Items, Entries));
        await cut.InvokeAsync(() => runtime.Import.SetResult(runtime.Module));
        await cut.WaitForAssertionAsync(() => Assert.Equal(["attach"], runtime.Module.Calls));

        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());
        await cut.InvokeAsync(runtime.Module.Resume.SetResult);

        await cut.WaitForAssertionAsync(() => Assert.True(instance.Released));
        Assert.Equal(["dispose"], instance.Calls);
        Assert.True(runtime.Module.Released);
    }

    [Fact]
    public void SlicesFillTheSweep()
    {
        var layout = new PieLayout();

        layout.Update(300, 300, new PieLayoutOptions { Culture = CultureInfo.InvariantCulture }, Entries);

        Assert.Equal(0, layout.Slices[0].Start);
        Assert.Equal(0.5, layout.Slices[0].End, 6);
        Assert.Equal(0.8, layout.Slices[1].End, 6);
        Assert.Equal(1, layout.Slices[2].End);
        Assert.Equal(100, layout.Total);
    }

    [Fact]
    public void ARingBufferIsCopiedBeforeItIsRead()
    {
        var buffer = new ChartRingBuffer<ChartEntry>(4);
        buffer.Push(new ChartEntry("A", 30, null, 100));
        buffer.Push(new ChartEntry("B", 50, null, 100));
        var seen = new HashSet<double>();

        Render<MusselPieChartView>(parameters => parameters
            .Add(static p => p.Items, buffer)
            .Add(p => p.ValueFormatter, x =>
            {
                seen.Add(x);
                buffer.Clear();
                return "v";
            }));

        Assert.Contains(50, seen);
    }
}

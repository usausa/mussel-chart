namespace MusselChart;

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
    public void HalfDonutSitsOnItsFlatSide()
    {
        var layout = new PieLayout();

        layout.Update(400, 200, new PieLayoutOptions { StartAngle = 270, SweepAngle = 180, InnerRadius = 0.5, Culture = CultureInfo.InvariantCulture }, Entries);

        Assert.Equal(192, layout.CenterY, 3);
        Assert.Equal(184, layout.OuterRadius, 3);
        Assert.Equal(92, layout.InnerRadius, 3);
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
}

public sealed class ChartGeometryTests
{
    [Fact]
    public void FullCircleIsDrawnWithTwoArcs()
    {
        var path = ChartGeometry.Arc(0, 0, 10, 0, 360);

        Assert.Equal("M0 -10A10 10 0 1 1 0 10A10 10 0 1 1 0 -10Z", path);
    }

    [Fact]
    public void ArcGoesClockwiseFromTwelveOClock()
    {
        var path = ChartGeometry.Arc(0, 0, 10, 0, 90);

        Assert.Equal("M0 -10A10 10 0 0 1 10 0", path);
    }

    [Fact]
    public void WholeRingHasAHoleOfItsOwn()
    {
        var path = ChartGeometry.Slice(0, 0, 10, 5, 0, 360, 0, 0);

        Assert.Equal(2, path.Count(static x => x == 'M'));
    }

    [Fact]
    public void SliceOfADonutMovesItsEdgesByTheGap()
    {
        var path = ChartGeometry.Slice(0, 0, 100, 50, 0, 90, 1, 1);

        Assert.StartsWith("M1 -99.99", path, StringComparison.Ordinal);
        Assert.Contains("L49.99 -1", path, StringComparison.Ordinal);
        Assert.EndsWith("Z", path, StringComparison.Ordinal);
    }

    [Fact]
    public void SliceOfAPieMeetsAtTheMovedEdges()
    {
        var path = ChartGeometry.Slice(0, 0, 100, 0, 0, 90, 1, 1);

        Assert.EndsWith("L1 -1Z", path, StringComparison.Ordinal);
    }

    [Fact]
    public void SliceSmallerThanItsGapsIsNotDrawn()
    {
        Assert.Empty(ChartGeometry.Slice(0, 0, 10, 5, 0, 1, 2, 2));
    }

    [Fact]
    public void UnitBoundsCoverTheSweptSector()
    {
        var (left, top, right, bottom) = ChartGeometry.GetUnitBounds(270, 180, 0.5);

        Assert.Equal(-1, left, 6);
        Assert.Equal(-1, top, 6);
        Assert.Equal(1, right, 6);
        Assert.Equal(0, bottom, 6);
    }
}

public sealed class ChartTextTests
{
    [Fact]
    public void FullWidthCharactersAreWiderThanNarrowOnes()
    {
        Assert.True(ChartText.Measure("あ", 10) > ChartText.Measure("a", 10));
        Assert.True(ChartText.Measure("a", 10) > ChartText.Measure("i", 10));
        Assert.Equal(0, ChartText.Measure(null, 10));
    }

    [Fact]
    public void TruncateEndsWithAnEllipsis()
    {
        Assert.Equal("Short", ChartText.Truncate("Short", 10, 1000));
        var text = ChartText.Truncate("A very long text to be cut", 10, 60);
        Assert.EndsWith(ChartText.Ellipsis, text, StringComparison.Ordinal);
        Assert.True(ChartText.Measure(text, 10) <= 60);
        Assert.Empty(ChartText.Truncate("Long", 10, 1));
    }

    [Fact]
    public void TipEscapesItsTexts()
    {
        var tip = ChartTip.Create("a\"b", [new ChartTipRow("#000000", "c\\d", "e\nf")], [new ChartTipMark(1.25, 2, "#FFFFFF")], new ChartTipLine(1, 2, 3, 4));

        Assert.Equal("{\"t\":\"a\\\"b\",\"r\":[[\"#000000\",\"c\\\\d\",\"e\\nf\"]],\"m\":[[1.2,2,\"#FFFFFF\"]],\"l\":[1,2,3,4]}", tip.Replace("1.3", "1.2", StringComparison.Ordinal));
    }

    [Fact]
    public void ThinningKeepsBothEnds()
    {
        var labels = Enumerable.Range(0, 11).Select(static i => new LabelExtent(i * 10, (i * 10) + 18)).ToList();
        var selected = new List<int>();

        LabelThinning.Select(labels, 4, Int32.MaxValue, selected);

        Assert.Equal(0, selected[0]);
        Assert.Equal(10, selected[^1]);
        Assert.True(selected.Count < labels.Count);
    }
}

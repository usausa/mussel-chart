namespace MusselChart;

using MusselChart.Internal;

public sealed record HeatItem(string Row, string Column, double Value);

public sealed record DayItem(DateTime Date, double Value);

public sealed class MusselHeatmapViewTests : BunitContext
{
    private static readonly HeatItem[] Items =
    [
        new("Mon", "0", 1),
        new("Mon", "1", 2),
        new("Tue", "0", 3)
    ];

    public MusselHeatmapViewTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<MusselHeatmapView<HeatItem>> RenderMatrix(HeatItem[] items) =>
        Render<MusselHeatmapView<HeatItem>>(parameters => parameters
            .Add(static p => p.Items, items)
            .Add(static p => p.Row, static x => x.Row)
            .Add(static p => p.Column, static x => x.Column)
            .Add(static p => p.Value, static x => x.Value)
            .Add(static p => p.Culture, CultureInfo.InvariantCulture));

    [Fact]
    public void MatrixDrawsEveryCellAndEmptyOnesInTheTrack()
    {
        var cut = RenderMatrix(Items);

        var cells = cut.FindAll(".mussel-heat-cells rect");
        Assert.Equal(4, cells.Count);
        Assert.Equal("mussel-heat-empty", cells[3].ClassList[0]);
        Assert.Null(cells[3].GetAttribute("fill"));
        Assert.Equal(3, cells.Count(static x => x.GetAttribute("fill") is not null));
    }

    [Fact]
    public void TheFirstValueOfACellWins()
    {
        var cut = RenderMatrix([new HeatItem("A", "1", 0), new HeatItem("A", "2", 10), new HeatItem("A", "1", 10)]);

        var cells = cut.FindAll(".mussel-heat-cells rect");
        Assert.Equal(2, cells.Count);
        Assert.Equal(ChartPalettes.OrangesLight[0], cells[0].GetAttribute("fill"));
    }

    [Fact]
    public void ColorsSpanTheRange()
    {
        var cut = RenderMatrix([new HeatItem("A", "1", 0), new HeatItem("A", "2", 10)]);

        var cells = cut.FindAll(".mussel-heat-cells rect");
        Assert.Equal(ChartPalettes.OrangesLight[0], cells[0].GetAttribute("fill"));
        Assert.Equal(ChartPalettes.OrangesLight[^1], cells[1].GetAttribute("fill"));
    }

    [Fact]
    public void FixedRangeMovesTheColors()
    {
        var cut = Render<MusselHeatmapView<HeatItem>>(parameters => parameters
            .Add(static p => p.Items, [new HeatItem("A", "1", 50)])
            .Add(static p => p.Row, static x => x.Row)
            .Add(static p => p.Column, static x => x.Column)
            .Add(static p => p.Value, static x => x.Value)
            .Add(static p => p.Minimum, 50)
            .Add(static p => p.Maximum, 100));

        Assert.Equal(ChartPalettes.OrangesLight[0], cut.Find(".mussel-heat-cells rect").GetAttribute("fill"));
    }

    [Fact]
    public void CellsLeaveTheGapOnTheirRightAndBottom()
    {
        var cut = Render<MusselHeatmapView<HeatItem>>(parameters => parameters
            .Add(static p => p.Items, [new HeatItem("A", "1", 1), new HeatItem("A", "2", 2)])
            .Add(static p => p.Column, static x => x.Column)
            .Add(static p => p.Value, static x => x.Value)
            .Add(static p => p.CellGap, 3)
            .Add(static p => p.LegendVisible, false));

        var cells = cut.FindAll(".mussel-heat-cells rect");
        var first = Double.Parse(cells[0].GetAttribute("x")!, CultureInfo.InvariantCulture) + Double.Parse(cells[0].GetAttribute("width")!, CultureInfo.InvariantCulture);
        var second = Double.Parse(cells[1].GetAttribute("x")!, CultureInfo.InvariantCulture);
        Assert.Equal(3, second - first);
    }

    [Fact]
    public void NamesFollowTheOrderOfAppearance()
    {
        var cut = RenderMatrix([new HeatItem("Tue", "b", 1), new HeatItem("Mon", "a", 2)]);

        var labels = cut.FindAll(".mussel-view-label").Select(static x => x.TextContent).ToList();
        Assert.True(labels.IndexOf("Tue") < labels.IndexOf("Mon"));
        Assert.True(labels.IndexOf("b") < labels.IndexOf("a"));
    }

    [Fact]
    public void TooltipShowsTheCellAndItsValue()
    {
        var cut = RenderMatrix(Items);

        var cells = cut.FindAll(".mussel-heat-cells rect");
        Assert.Contains("\"t\":\"Mon 1\"", cells[1].GetAttribute("data-tip"), StringComparison.Ordinal);
        Assert.Contains("\"2\"", cells[1].GetAttribute("data-tip"), StringComparison.Ordinal);
        Assert.Contains("\"r\":[]", cells[3].GetAttribute("data-tip"), StringComparison.Ordinal);
        Assert.Equal("frame", cells[0].GetAttribute("data-mark"));
    }

    [Fact]
    public void TooltipsAreLeftOutWithoutTheTrackball()
    {
        var cut = Render<MusselHeatmapView<HeatItem>>(parameters => parameters
            .Add(static p => p.Items, Items)
            .Add(static p => p.Row, static x => x.Row)
            .Add(static p => p.Column, static x => x.Column)
            .Add(static p => p.Value, static x => x.Value)
            .Add(static p => p.TrackballMode, ChartTrackballMode.None));

        Assert.Null(cut.Find(".mussel-heat-cells rect").GetAttribute("data-tip"));
    }

    [Fact]
    public void ScaleHasTheStepsOfThePaletteAndItsEnds()
    {
        var cut = RenderMatrix([new HeatItem("A", "1", 0), new HeatItem("A", "2", 100)]);

        Assert.True(cut.FindAll(".mussel-heat-scale rect").Count > 1);
        var labels = cut.FindAll(".mussel-view-label").Select(static x => x.TextContent).ToList();
        Assert.Contains("0", labels);
        Assert.Contains("100", labels);
    }

    [Fact]
    public void ScaleCanBeTurnedOff()
    {
        var cut = Render<MusselHeatmapView<HeatItem>>(parameters => parameters
            .Add(static p => p.Items, Items)
            .Add(static p => p.Row, static x => x.Row)
            .Add(static p => p.Column, static x => x.Column)
            .Add(static p => p.Value, static x => x.Value)
            .Add(static p => p.LegendVisible, false));

        Assert.Empty(cut.FindAll(".mussel-heat-scale rect"));
    }

    [Theory]
    [InlineData(ChartLegendPosition.Left)]
    [InlineData(ChartLegendPosition.Right)]
    [InlineData(ChartLegendPosition.Top)]
    public void ScaleCanSitOnAnySide(ChartLegendPosition position)
    {
        var cut = Render<MusselHeatmapView<HeatItem>>(parameters => parameters
            .Add(static p => p.Items, [new HeatItem("A", "1", 0), new HeatItem("A", "2", 100)])
            .Add(static p => p.Row, static x => x.Row)
            .Add(static p => p.Column, static x => x.Column)
            .Add(static p => p.Value, static x => x.Value)
            .Add(static p => p.LegendPosition, position));

        Assert.True(cut.FindAll(".mussel-heat-scale rect").Count > 1);
    }

    [Fact]
    public void EntriesMakeOneRow()
    {
        var cut = Render<MusselHeatmapView<ChartEntry>>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 1), new ChartEntry("B", 2)]));

        Assert.Equal(2, cut.FindAll(".mussel-heat-cells rect").Count);
    }

    [Fact]
    public void CalendarHasAColumnPerWeekAndARowPerDay()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.DateTimeFormat.FirstDayOfWeek = DayOfWeek.Sunday;
        var start = new DateTime(2026, 3, 4, 0, 0, 0, DateTimeKind.Unspecified);
        var items = Enumerable.Range(0, 20).Where(static i => i != 3).Select(i => new DayItem(start.AddDays(i), i)).ToArray();

        var cut = Render<MusselHeatmapView<DayItem>>(parameters => parameters
            .Add(static p => p.Items, items)
            .Add(static p => p.LayoutMode, ChartHeatmapLayoutMode.Calendar)
            .Add(static p => p.Date, static x => x.Date)
            .Add(static p => p.Value, static x => x.Value)
            .Add(static p => p.Culture, culture));

        var cells = cut.FindAll(".mussel-heat-cells rect");
        Assert.Equal(20, cells.Count);
        Assert.Single(cells, static x => x.ClassList.Contains("mussel-heat-empty"));
        Assert.Contains(cells, static x => x.GetAttribute("data-tip")!.Contains("03/04/2026 (Wed)", StringComparison.Ordinal));
        var labels = cut.FindAll(".mussel-view-label").Select(static x => x.TextContent).ToList();
        Assert.Contains("Mon", labels);
        Assert.Contains("Mar", labels);
    }

    [Fact]
    public void CalendarStartsOnTheFirstDayOfTheWeek()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.DateTimeFormat.FirstDayOfWeek = DayOfWeek.Monday;
        var layout = new HeatmapLayout();

        layout.Update(600, 200, new HeatmapLayoutOptions { LayoutMode = ChartHeatmapLayoutMode.Calendar, Culture = culture }, [new HeatmapEntry(null, null, new DateTime(2026, 3, 4, 0, 0, 0, DateTimeKind.Unspecified), 1), new HeatmapEntry(null, null, new DateTime(2026, 3, 17, 0, 0, 0, DateTimeKind.Unspecified), 2)]);

        Assert.Equal(new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Unspecified), layout.CalendarStart);
        Assert.Equal(7, layout.RowCount);
        Assert.Equal(3, layout.ColumnCount);
        Assert.False(layout.IsPresent(0));
        Assert.Equal(new DateTime(2026, 3, 4, 0, 0, 0, DateTimeKind.Unspecified), layout.GetDate(layout.ColumnCount * 2));
        Assert.Equal(layout.CellWidth, layout.CellHeight);
    }

    [Fact]
    public void ShowsEmptyTextWithoutData()
    {
        var cut = Render<MusselHeatmapView<HeatItem>>(parameters => parameters
            .Add(static p => p.Items, Array.Empty<HeatItem>())
            .Add(static p => p.Row, static x => x.Row)
            .Add(static p => p.Column, static x => x.Column)
            .Add(static p => p.Value, static x => x.Value)
            .Add(static p => p.EmptyText, "No data"));

        Assert.Equal("No data", cut.Find(".mussel-empty text").TextContent);
        Assert.Empty(cut.FindAll(".mussel-heat-cells rect"));
    }

    [Fact]
    public void PaletteSplitsTheRangeIntoSteps()
    {
        Assert.Equal(0, HeatmapPalette.GetIndex(0, 0, 100, 64));
        Assert.Equal(63, HeatmapPalette.GetIndex(100, 0, 100, 64));
        Assert.Equal(32, HeatmapPalette.GetIndex(50, 0, 100, 64));
        Assert.Equal(0, HeatmapPalette.GetIndex(-5, 0, 100, 64));
        Assert.Equal(63, HeatmapPalette.GetIndex(500, 0, 100, 64));
        Assert.Equal(0, HeatmapPalette.GetIndex(1, 1, 1, 64));
    }

    [Fact]
    public void PaletteKeepsItsEndsAndHasAllSteps()
    {
        var colors = HeatmapPalette.Build(ChartPalettes.OrangesLight);

        Assert.Equal(HeatmapPalette.Steps, colors.Length);
        Assert.Equal("#FFF5EB", colors[0]);
        Assert.Equal("#7F2704", colors[^1]);
    }
}

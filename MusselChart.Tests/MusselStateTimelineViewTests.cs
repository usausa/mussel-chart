namespace MusselChart;

using MusselChart.Internal;

public sealed record StateItem(string? Row, DateTime Start, object? State, DateTime? End = null);

public sealed class MusselStateTimelineViewTests : BunitContext
{
    private static readonly DateTime Origin = new(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc);

    private static readonly ChartValueMapping[] Mappings =
    [
        new() { Value = "Up", Text = "Running", Level = ChartThresholdLevel.Good },
        new() { Value = "Down", Color = "#112233" },
        new() { Value = 1, Text = "One", Level = ChartThresholdLevel.Warning }
    ];

    private static readonly ChartThreshold[] Levels =
    [
        new() { Level = ChartThresholdLevel.Good },
        new() { Value = 60, Level = ChartThresholdLevel.Warning, Label = "Busy" },
        new() { Value = 85, Level = ChartThresholdLevel.Critical }
    ];

    public MusselStateTimelineViewTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<MusselStateTimelineView<StateItem>> RenderItems(StateItem[] items, Action<ComponentParameterCollectionBuilder<MusselStateTimelineView<StateItem>>>? configure = null) =>
        Render<MusselStateTimelineView<StateItem>>(parameters =>
        {
            parameters
                .Add(static p => p.Items, items)
                .Add(static p => p.Row, static x => x.Row)
                .Add(static p => p.Start, static x => x.Start)
                .Add(static p => p.End, static x => x.End)
                .Add(static p => p.State, static x => x.State)
                .Add(static p => p.Culture, CultureInfo.InvariantCulture)
                .Add(static p => p.TimeZone, TimeZoneInfo.Utc);
            configure?.Invoke(parameters);
        });

    [Fact]
    public void EachStateLastsUntilTheNextStartOfItsRow()
    {
        var cut = RenderItems(
            [new StateItem("A", Origin, "x"), new StateItem("A", Origin.AddHours(1), "y"), new StateItem("B", Origin, "z")],
            p => p.Add(static x => x.Maximum, Origin.AddHours(2)));

        var bands = cut.FindAll(".mussel-timeline-bands rect");
        Assert.Equal(3, bands.Count);
        var firstEnd = Double.Parse(bands[0].GetAttribute("x")!, CultureInfo.InvariantCulture) + Double.Parse(bands[0].GetAttribute("width")!, CultureInfo.InvariantCulture);
        Assert.Equal(Double.Parse(bands[1].GetAttribute("x")!, CultureInfo.InvariantCulture), firstEnd);
        var rows = cut.FindAll(".mussel-view-label").Select(static x => x.TextContent).Where(static x => x is "A" or "B").ToList();
        Assert.Equal(["A", "B"], rows);
    }

    [Fact]
    public void EqualStatesNextToEachOtherMerge()
    {
        StateItem[] items = [new("A", Origin, "x"), new("A", Origin.AddHours(1), "x"), new("A", Origin.AddHours(2), "y")];

        var merged = RenderItems(items, p => p.Add(static x => x.Maximum, Origin.AddHours(3)));
        var separate = RenderItems(items, p => p.Add(static x => x.Maximum, Origin.AddHours(3)).Add(static x => x.MergeValues, false));

        Assert.Equal(2, merged.FindAll(".mussel-timeline-bands rect").Count);
        Assert.Equal(3, separate.FindAll(".mussel-timeline-bands rect").Count);
    }

    [Fact]
    public void AnEndLeavesAGapThatKeepsStatesApart()
    {
        var cut = RenderItems(
            [new StateItem("A", Origin, "x", Origin.AddHours(1)), new StateItem("A", Origin.AddHours(2), "x", Origin.AddHours(3))]);

        var bands = cut.FindAll(".mussel-timeline-bands rect");
        Assert.Equal(2, bands.Count);
        var firstEnd = Double.Parse(bands[0].GetAttribute("x")!, CultureInfo.InvariantCulture) + Double.Parse(bands[0].GetAttribute("width")!, CultureInfo.InvariantCulture);
        Assert.True(Double.Parse(bands[1].GetAttribute("x")!, CultureInfo.InvariantCulture) > firstEnd);
    }

    [Fact]
    public void AnItemWithoutAStateEndsThePreviousOne()
    {
        var cut = RenderItems(
            [new StateItem("A", Origin, "x"), new StateItem("A", Origin.AddHours(1), null), new StateItem("A", Origin.AddHours(2), "y")],
            p => p.Add(static x => x.Maximum, Origin.AddHours(3)));

        Assert.Equal(2, cut.FindAll(".mussel-timeline-bands rect").Count);
    }

    [Fact]
    public void MappingsGiveTheTextAndTheColor()
    {
        var cut = RenderItems(
            [new StateItem("A", Origin, "Up"), new StateItem("A", Origin.AddHours(1), "Down"), new StateItem("A", Origin.AddHours(2), 1)],
            p => p.Add(static x => x.Maximum, Origin.AddHours(3)).Add(static x => x.ValueMappings, Mappings));

        var bands = cut.FindAll(".mussel-timeline-bands rect");
        Assert.Equal(ChartColor.Level(ChartThresholdLevel.Good), bands[0].GetAttribute("fill"));
        Assert.Equal("#112233", bands[1].GetAttribute("fill"));
        Assert.Equal(ChartColor.Level(ChartThresholdLevel.Warning), bands[2].GetAttribute("fill"));
        Assert.Equal(["Running", "Down", "One"], cut.FindAll(".mussel-view-legend-name").Select(static x => x.TextContent));
        Assert.Contains("Running", cut.FindAll(".mussel-timeline-text").Select(static x => x.TextContent));
    }

    [Fact]
    public void ThresholdsMergeNumbersOfOneStep()
    {
        var cut = RenderItems(
            [new StateItem("A", Origin, 10d), new StateItem("A", Origin.AddHours(1), 20), new StateItem("A", Origin.AddHours(2), 70), new StateItem("A", Origin.AddHours(3), 90m)],
            p => p.Add(static x => x.Maximum, Origin.AddHours(4)).Add(static x => x.Thresholds, Levels));

        var bands = cut.FindAll(".mussel-timeline-bands rect");
        Assert.Equal(3, bands.Count);
        Assert.Equal(ChartColor.Level(ChartThresholdLevel.Good), bands[0].GetAttribute("fill"));
        Assert.Equal(ChartColor.Level(ChartThresholdLevel.Critical), bands[2].GetAttribute("fill"));
        Assert.Equal(["< 60", "Busy", "85+"], cut.FindAll(".mussel-view-legend-name").Select(static x => x.TextContent));
    }

    [Fact]
    public void NumbersWithoutThresholdsTakeAColorScaleAndNoLegend()
    {
        var cut = RenderItems(
            [new StateItem("A", Origin, 0d), new StateItem("A", Origin.AddHours(1), 50d), new StateItem("A", Origin.AddHours(2), 100d)],
            p => p.Add(static x => x.Maximum, Origin.AddHours(3)));

        var colors = cut.FindAll(".mussel-timeline-bands rect").Select(static x => x.GetAttribute("fill")).ToList();
        Assert.Equal(ChartColor.Level(ChartThresholdLevel.Good), colors[0]);
        Assert.Equal(ChartColor.Level(ChartThresholdLevel.Critical), colors[2]);
        Assert.Equal(3, colors.Distinct(StringComparer.Ordinal).Count());
        Assert.Empty(cut.FindAll(".mussel-view-legend-item"));
    }

    [Fact]
    public void TextOfABandFollowsTheOptions()
    {
        StateItem[] items = [new("A", Origin, "A long state name"), new("A", Origin.AddHours(1), "y")];

        var never = RenderItems(items, p => p.Add(static x => x.Maximum, Origin.AddHours(2)).Add(static x => x.ShowValue, ChartVisibilityMode.Never));
        var centered = RenderItems(items, p => p.Add(static x => x.Maximum, Origin.AddHours(2)).Add(static x => x.AlignValue, ChartValueAlignment.Center));
        var right = RenderItems(items, p => p.Add(static x => x.Maximum, Origin.AddHours(2)).Add(static x => x.AlignValue, ChartValueAlignment.Right));

        Assert.Empty(never.FindAll(".mussel-timeline-text"));
        Assert.All(centered.FindAll(".mussel-timeline-text"), static x => Assert.Equal("middle", x.GetAttribute("text-anchor")));
        Assert.All(right.FindAll(".mussel-timeline-text"), static x => Assert.Equal("end", x.GetAttribute("text-anchor")));
    }

    [Fact]
    public void LongTextIsCutToTheBand()
    {
        var cut = RenderItems(
            [new StateItem("A", Origin, "A state name that is far too long for this band"), new StateItem("A", Origin.AddSeconds(1), "y")],
            p => p.Add(static x => x.Maximum, Origin.AddSeconds(5)));

        Assert.EndsWith(ChartText.Ellipsis, cut.FindAll(".mussel-timeline-text")[0].TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void BandsAreClippedToTheRange()
    {
        var cut = RenderItems(
            [new StateItem("A", Origin, "x"), new StateItem("A", Origin.AddHours(1), "y")],
            p => p.Add(static x => x.Minimum, Origin.AddMinutes(90)).Add(static x => x.Maximum, Origin.AddHours(2)));

        Assert.Single(cut.FindAll(".mussel-timeline-bands rect"));
    }

    [Fact]
    public void TooltipHasTheTimesTheStateAndTheDuration()
    {
        var cut = RenderItems(
            [new StateItem("A", Origin, "x"), new StateItem("A", Origin.AddMinutes(150), "y")],
            p => p.Add(static x => x.Maximum, Origin.AddHours(4)));

        var band = cut.FindAll(".mussel-timeline-bands rect")[0];
        var tip = band.GetAttribute("data-tip");
        Assert.Contains("08:00 – 10:30", tip, StringComparison.Ordinal);
        Assert.Contains("\"A\",\"x\"", tip, StringComparison.Ordinal);
        Assert.Contains("2h 30m", tip, StringComparison.Ordinal);
        Assert.Equal("overlay", band.GetAttribute("data-mark"));
    }

    [Fact]
    public void AxisShowsTheTimesOfTheRange()
    {
        var cut = RenderItems(
            [new StateItem(null, Origin, "x")],
            p => p.Add(static x => x.Maximum, Origin.AddHours(6)));

        var labels = cut.FindAll(".mussel-view-label").Select(static x => x.TextContent).ToList();
        Assert.Contains("12:00", labels);
        Assert.NotEmpty(cut.FindAll("line.mussel-grid-line"));
    }

    [Fact]
    public void ShowsEmptyTextWithoutData()
    {
        var cut = RenderItems([], p => p.Add(static x => x.EmptyText, "No data"));

        Assert.Equal("No data", cut.Find(".mussel-empty text").TextContent);
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(500, "500ms")]
    [InlineData(45_000, "45s")]
    [InlineData(150_000, "2m 30s")]
    [InlineData(9_000_000, "2h 30m")]
    [InlineData(93_600_000, "1d 2h")]
    [InlineData(1_209_600_000, "2w")]
    public void DurationIsWrittenLikeGrafana(long milliseconds, string expected)
    {
        Assert.Equal(expected, StateTimelineLayout.FormatDuration(milliseconds * TimeSpan.TicksPerMillisecond));
    }
}

public sealed class SparklineWinLossTests : BunitContext
{
    [Fact]
    public void WinsFillTheUpperHalfAndLossesTheLower()
    {
        var cut = Render<MusselSparklineView>(parameters => parameters
            .Add(static p => p.Values, [1d, -2, 0, Double.NaN, 3])
            .Add(static p => p.Kind, ChartSparklineKind.WinLoss)
            .Add(static p => p.Height, 20));

        var bars = cut.FindAll("rect");
        Assert.Equal(3, bars.Count);
        Assert.Equal("0", bars[0].GetAttribute("y"));
        Assert.Equal("47.5", bars[0].GetAttribute("height"));
        Assert.Equal("52.5", bars[1].GetAttribute("y"));
        Assert.Equal("0 0 5 100", cut.Find("svg").GetAttribute("viewBox"));
    }

    [Fact]
    public void LossesUseTheCriticalColorUnlessSet()
    {
        var plain = Render<MusselSparklineView>(parameters => parameters
            .Add(static p => p.Values, [1d, -2])
            .Add(static p => p.Kind, ChartSparklineKind.WinLoss)
            .Add(static p => p.Color, "#00AA00"));
        var colored = Render<MusselSparklineView>(parameters => parameters
            .Add(static p => p.Values, [1d, -2])
            .Add(static p => p.Kind, ChartSparklineKind.WinLoss)
            .Add(static p => p.NegativeColor, "#AA0000"));

        var bars = plain.FindAll("rect");
        Assert.Equal("#00AA00", bars[0].GetAttribute("fill"));
        Assert.Equal("mussel-sparkline-loss", bars[1].ClassList[0]);
        Assert.Null(bars[1].GetAttribute("fill"));
        Assert.Equal("#AA0000", colored.FindAll("rect")[1].GetAttribute("fill"));
    }

    [Fact]
    public void NegativeBarsKeepTheColorUnlessSet()
    {
        var plain = Render<MusselSparklineView>(parameters => parameters
            .Add(static p => p.Values, [1d, -2])
            .Add(static p => p.Kind, ChartSparklineKind.Bar)
            .Add(static p => p.Color, "#00AA00"));
        var colored = Render<MusselSparklineView>(parameters => parameters
            .Add(static p => p.Values, [1d, -2])
            .Add(static p => p.Kind, ChartSparklineKind.Bar)
            .Add(static p => p.Color, "#00AA00")
            .Add(static p => p.NegativeColor, "#AA0000"));

        Assert.Equal("#00AA00", plain.FindAll("rect")[1].GetAttribute("fill"));
        Assert.Equal("#AA0000", colored.FindAll("rect")[1].GetAttribute("fill"));
    }
}

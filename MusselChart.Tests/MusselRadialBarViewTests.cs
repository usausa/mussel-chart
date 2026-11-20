namespace MusselChart;

using System.Text.Json;
using System.Text.RegularExpressions;

using MusselChart.Internal;

public sealed class MusselRadialBarViewTests : BunitContext
{
    private static readonly ChartThreshold[] Levels =
    [
        new() { Level = ChartThresholdLevel.Good },
        new() { Value = 70, Level = ChartThresholdLevel.Warning },
        new() { Value = 85, Level = ChartThresholdLevel.Critical }
    ];

    public MusselRadialBarViewTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void DrawsARingWithATrackForEachItem()
    {
        var cut = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 50), new ChartEntry("B", 25)])
            .Add(static p => p.Palette, ["#AA0000", "#00AA00"])
            .Add(static p => p.Culture, CultureInfo.InvariantCulture));

        var rings = cut.FindAll(".mussel-ring");
        Assert.Equal(2, rings.Count);
        Assert.Single(rings[0].QuerySelectorAll(".mussel-radial-track"));
        Assert.Equal("#AA0000", rings[0].QuerySelector("path:not(.mussel-radial-track)")!.GetAttribute("stroke"));
        Assert.Equal("#00AA00", rings[1].QuerySelector("path:not(.mussel-radial-track)")!.GetAttribute("stroke"));
    }

    [Fact]
    public void RingsBeyondThePaletteAreGray()
    {
        var cut = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 50), new ChartEntry("B", 25)])
            .Add(static p => p.Palette, ["#AA0000"]));

        Assert.Equal(["#AA0000", ChartColor.Other], cut.FindAll(".mussel-ring").Select(static x => x.QuerySelector("path:not(.mussel-radial-track)")!.GetAttribute("stroke")));
    }

    [Fact]
    public void TrackCanBeLeftOut()
    {
        var cut = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 50)])
            .Add(static p => p.TrackVisible, false));

        Assert.Empty(cut.FindAll(".mussel-radial-track"));
    }

    [Fact]
    public void CenterShowsTheFirstRingAsAPercentage()
    {
        var cut = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("CPU", 25, null, 50)])
            .Add(static p => p.Culture, CultureInfo.GetCultureInfo("ja-JP")));

        Assert.Equal("50%", cut.Find(".mussel-radial-center").TextContent);
        Assert.Equal("CPU", cut.Find(".mussel-radial-caption").TextContent);
    }

    [Fact]
    public void CenterCanShowTheTotalOrAText()
    {
        var total = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 10), new ChartEntry("B", 20)])
            .Add(static p => p.CenterValueMode, ChartCenterValueMode.Total)
            .Add(static p => p.CenterLabel, "Total")
            .Add(static p => p.Culture, CultureInfo.InvariantCulture));
        var text = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 10)])
            .Add(static p => p.CenterText, "OK"));
        var none = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 10)])
            .Add(static p => p.CenterValueMode, ChartCenterValueMode.None));

        Assert.Equal("30", total.Find(".mussel-radial-center").TextContent);
        Assert.Equal("Total", total.Find(".mussel-radial-caption").TextContent);
        Assert.Equal("OK", text.Find(".mussel-radial-center").TextContent);
        Assert.Empty(none.FindAll(".mussel-radial-center"));
    }

    [Fact]
    public void ATotalThatIsNotFiniteShowsNoCenter()
    {
        var cut = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 1e308), new ChartEntry("B", 1e308)])
            .Add(static p => p.CenterValueMode, ChartCenterValueMode.Total)
            .Add(static p => p.CenterLabel, "Total"));

        Assert.Empty(cut.FindAll(".mussel-radial-center"));
        Assert.Empty(cut.FindAll(".mussel-radial-caption"));
    }

    [Fact]
    public void ARingOfTheWidestRangeShowsTheSameShareAsAnOrdinaryOne()
    {
        var wide = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 1e308)])
            .Add(static p => p.Minimum, -1e308)
            .Add(static p => p.Maximum, 0)
            .Add(static p => p.OverflowMode, ChartOverflowMode.Wrap)
            .Add(static p => p.LegendValueMode, ChartLegendValueMode.Percent));
        var plain = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 1)])
            .Add(static p => p.Minimum, -1)
            .Add(static p => p.Maximum, 0)
            .Add(static p => p.OverflowMode, ChartOverflowMode.Wrap)
            .Add(static p => p.LegendValueMode, ChartLegendValueMode.Percent));

        Assert.Equal(plain.Find(".mussel-radial-center").TextContent, wide.Find(".mussel-radial-center").TextContent);
        Assert.Equal(plain.Find(".mussel-view-legend-value").TextContent, wide.Find(".mussel-view-legend-value").TextContent);
        Assert.Equal(plain.FindAll("path").Select(static x => x.GetAttribute("d")), wide.FindAll("path").Select(static x => x.GetAttribute("d")));
    }

    [Fact]
    public void TooltipsAreLeftOutWithoutTheTrackball()
    {
        var cut = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 90), new ChartEntry("B", 10)])
            .Add(static p => p.TrackballMode, ChartTrackballMode.None));

        Assert.NotEmpty(cut.FindAll(".mussel-ring"));
        Assert.All(cut.FindAll(".mussel-ring"), static x => Assert.Null(x.GetAttribute("data-tip")));
    }

    [Fact]
    public void TooltipsAreLeftOutWhenTheTooltipIsHidden()
    {
        var cut = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 90), new ChartEntry("B", 10)])
            .Add(static p => p.TooltipVisible, false));

        Assert.NotEmpty(cut.FindAll(".mussel-ring"));
        Assert.All(cut.FindAll(".mussel-ring"), static x => Assert.Null(x.GetAttribute("data-tip")));
    }

    [Fact]
    public void EachRingTipsOnlyItsOwnRow()
    {
        var cut = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 90), new ChartEntry("B", 10)]));

        using var tip = JsonDocument.Parse(cut.FindAll(".mussel-ring")[1].GetAttribute("data-tip")!);
        Assert.Equal(1, tip.RootElement.GetProperty("r").GetArrayLength());
    }

    [Fact]
    public void ThresholdsColorTheRingByItsPercentage()
    {
        var cut = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 90), new ChartEntry("B", 10)])
            .Add(static p => p.Thresholds, Levels));

        var rings = cut.FindAll(".mussel-ring");
        Assert.Equal(ChartColor.Level(ChartThresholdLevel.Critical), rings[0].QuerySelector("path:not(.mussel-radial-track)")!.GetAttribute("stroke"));
        Assert.Equal(ChartColor.Level(ChartThresholdLevel.Good), rings[1].QuerySelector("path:not(.mussel-radial-track)")!.GetAttribute("stroke"));
    }

    [Fact]
    public void OverflowWrapsOverTheFirstLapInADarkerColor()
    {
        var cut = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 120, "#5794F2")])
            .Add(static p => p.OverflowMode, ChartOverflowMode.Wrap));

        var arcs = cut.Find(".mussel-ring").QuerySelectorAll("path");
        Assert.Equal(2, arcs.Length);
        Assert.Equal("#5794F2", arcs[0].GetAttribute("stroke"));
        Assert.NotEqual("#5794F2", arcs[1].GetAttribute("stroke"));
    }

    [Fact]
    public void ClampStopsAtTheMaximum()
    {
        var cut = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 120)]));

        Assert.Single(cut.Find(".mussel-ring").QuerySelectorAll("path"));
    }

    [Fact]
    public void SegmentedLightsTheCellsUpToTheValue()
    {
        var cut = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 50)])
            .Add(static p => p.DisplayMode, ChartRadialBarMode.Segmented)
            .Add(static p => p.SegmentCount, 10));

        var ring = cut.Find(".mussel-ring");
        Assert.Equal(10, ring.QuerySelectorAll("path").Length);
        Assert.Equal(5, ring.QuerySelectorAll(".mussel-radial-unlit").Length);
    }

    [Fact]
    public void SegmentedLeavesOutTheCellsThatAreNotLitWithoutTheTrack()
    {
        var cut = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 50)])
            .Add(static p => p.DisplayMode, ChartRadialBarMode.Segmented)
            .Add(static p => p.SegmentCount, 10)
            .Add(static p => p.TrackVisible, false));

        var ring = cut.Find(".mussel-ring");
        Assert.Equal(5, ring.QuerySelectorAll("path").Length);
        Assert.Empty(ring.QuerySelectorAll(".mussel-radial-unlit"));
    }

    [Fact]
    public void ShowsEmptyTextWithoutData()
    {
        var cut = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, Array.Empty<ChartEntry>())
            .Add(static p => p.EmptyText, "No data"));

        Assert.Equal("No data", cut.Find(".mussel-empty text").TextContent);
        Assert.Empty(cut.FindAll(".mussel-ring"));
    }

    [Theory]
    [InlineData(ChartCenterValueMode.Auto, null)]
    [InlineData(ChartCenterValueMode.Total, null)]
    [InlineData(ChartCenterValueMode.Auto, "OK")]
    public void ShowsEmptyTextAndNoCenterWithoutAFiniteValue(ChartCenterValueMode mode, string? text)
    {
        var cut = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", Double.NaN), new ChartEntry("B", Double.PositiveInfinity)])
            .Add(static p => p.CenterValueMode, mode)
            .Add(static p => p.CenterText, text)
            .Add(static p => p.CenterLabel, "Total")
            .Add(static p => p.EmptyText, "No data"));

        Assert.Equal("No data", cut.Find(".mussel-empty text").TextContent);
        Assert.Empty(cut.FindAll(".mussel-radial-center"));
        Assert.Empty(cut.FindAll(".mussel-radial-caption"));
        Assert.Equal(2, cut.FindAll(".mussel-radial-track").Count);
        Assert.Equal(["A", "B"], cut.FindAll(".mussel-view-legend-name").Select(static x => x.TextContent));
    }

    [Fact]
    public void AnyFiniteValueHidesTheEmptyText()
    {
        var cut = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", Double.NaN), new ChartEntry("B", 10)])
            .Add(static p => p.CenterValueMode, ChartCenterValueMode.Total)
            .Add(static p => p.EmptyText, "No data")
            .Add(static p => p.Culture, CultureInfo.InvariantCulture));

        Assert.Equal(string.Empty, cut.Find(".mussel-empty text").TextContent);
        Assert.Equal("10", cut.Find(".mussel-radial-center").TextContent);
    }

    [Theory]
    [InlineData(Double.NegativeInfinity, 100d, ChartCenterValueMode.Total, null)]
    [InlineData(100d, 0d, ChartCenterValueMode.Total, null)]
    [InlineData(50d, 50d, ChartCenterValueMode.Total, null)]
    [InlineData(Double.NegativeInfinity, 100d, ChartCenterValueMode.Auto, "OK")]
    public void ARangeThatIsNotValidShowsEmptyTextAndNoCenter(double minimum, double maximum, ChartCenterValueMode mode, string? text)
    {
        var cut = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 50)])
            .Add(static p => p.Minimum, minimum)
            .Add(static p => p.Maximum, maximum)
            .Add(static p => p.CenterValueMode, mode)
            .Add(static p => p.CenterText, text)
            .Add(static p => p.CenterLabel, "Total")
            .Add(static p => p.EmptyText, "No data")
            .Add(static p => p.Culture, CultureInfo.InvariantCulture));

        Assert.Equal("No data", cut.Find(".mussel-empty text").TextContent);
        Assert.Empty(cut.FindAll(".mussel-radial-center"));
        Assert.Empty(cut.FindAll(".mussel-radial-caption"));
    }

    [Fact]
    public void TheTotalLeavesOutAnItemWithoutAValidRange()
    {
        var cut = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 10), new ChartEntry("B", 20, null, 0)])
            .Add(static p => p.CenterValueMode, ChartCenterValueMode.Total)
            .Add(static p => p.Culture, CultureInfo.InvariantCulture));

        Assert.Equal("10", cut.Find(".mussel-radial-center").TextContent);
    }

    [Fact]
    public void SegmentedTakesTheThresholdColorOfEachCell()
    {
        var cut = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 100)])
            .Add(static p => p.DisplayMode, ChartRadialBarMode.Segmented)
            .Add(static p => p.SegmentCount, 10)
            .Add(static p => p.Thresholds, Levels));

        var colors = cut.Find(".mussel-ring").QuerySelectorAll("path").Select(static x => x.GetAttribute("stroke")).ToList();
        Assert.Equal(ChartColor.Level(ChartThresholdLevel.Good), colors[0]);
        Assert.Equal(ChartColor.Level(ChartThresholdLevel.Warning), colors[7]);
        Assert.Equal(ChartColor.Level(ChartThresholdLevel.Critical), colors[9]);
    }

    [Fact]
    public void GradientUsesAConicGradientCutToTheRing()
    {
        var cut = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 50, "#5794F2")])
            .Add(static p => p.FillMode, ChartFillMode.Gradient));

        var style = cut.Find(".mussel-radial-sweep div").GetAttribute("style");
        Assert.Contains("conic-gradient(from 0deg", style, StringComparison.Ordinal);
        Assert.Contains("mask:radial-gradient", style, StringComparison.Ordinal);
        Assert.Single(cut.FindAll(".mussel-radial-hit"));
    }

    [Fact]
    public void GradientOverflowWrapsOverTheGradient()
    {
        var cut = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 150, "#5794F2")])
            .Add(static p => p.FillMode, ChartFillMode.Gradient)
            .Add(static p => p.OverflowMode, ChartOverflowMode.Wrap));

        var last = cut.Find(".mussel-ring").Children[^1];
        Assert.Equal("path", last.LocalName);
        Assert.False(last.HasAttribute("class"));
        Assert.NotNull(last.GetAttribute("stroke"));
    }

    [Fact]
    public void TheWrapTakesTheLastColorOfTheGradientAndGlows()
    {
        var cut = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 150, "#5794F2")])
            .Add(static p => p.FillMode, ChartFillMode.Gradient)
            .Add(static p => p.OverflowMode, ChartOverflowMode.Wrap)
            .Add(static p => p.BarGlow, true));

        var wrap = cut.Find(".mussel-ring").Children[^1].GetAttribute("stroke");
        var style = cut.Find(".mussel-radial-sweep div").GetAttribute("style")!;
        var last = Regex.Matches(style[..style.IndexOf(";-webkit-mask", StringComparison.Ordinal)], "#[0-9A-Fa-f]{6}")[^1].Value;
        Assert.Equal(ChartColor.Shift(last, 0, -15), wrap);
        Assert.Contains(wrap, cut.FindAll(".mussel-radial-glow path").Select(static x => x.GetAttribute("stroke")));
    }

    [Fact]
    public void GradientFollowsTheThresholds()
    {
        var cut = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 100)])
            .Add(static p => p.FillMode, ChartFillMode.Gradient)
            .Add(static p => p.Thresholds, Levels));

        var style = cut.Find(".mussel-radial-sweep div").GetAttribute("style");
        Assert.Contains(ChartColor.Level(ChartThresholdLevel.Good), style, StringComparison.Ordinal);
        Assert.Contains(ChartColor.Level(ChartThresholdLevel.Warning) + " 252deg", style, StringComparison.Ordinal);
    }

    [Fact]
    public void ThresholdsOfTheSameValueTakeTheLaterOneEverywhere()
    {
        ChartThreshold[] thresholds =
        [
            new() { Value = 50, Level = ChartThresholdLevel.Warning },
            new() { Value = 50, Level = ChartThresholdLevel.Critical },
            new() { Value = 10, Level = ChartThresholdLevel.Good }
        ];
        var gradient = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 60)])
            .Add(static p => p.FillMode, ChartFillMode.Gradient)
            .Add(p => p.Thresholds, thresholds));
        var marked = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 60)])
            .Add(p => p.Thresholds, thresholds)
            .Add(static p => p.ThresholdMarkersVisible, true));

        var critical = ChartColor.Level(ChartThresholdLevel.Critical);
        var stops = Regex.Matches(gradient.Find(".mussel-radial-sweep div").GetAttribute("style")!, "(#[0-9A-Fa-f]{6}) 180deg");
        Assert.Equal(critical, stops[^1].Groups[1].Value);
        Assert.Equal(critical, marked.FindAll(".mussel-radial-markers path")[^1].GetAttribute("stroke"));
        Assert.Equal(critical, marked.Find(".mussel-ring").QuerySelector("path:not(.mussel-radial-track)")!.GetAttribute("stroke"));
    }

    [Fact]
    public void ThresholdMarkersMakeARingOutside()
    {
        var plain = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 50)])
            .Add(static p => p.Thresholds, Levels));
        var marked = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 50)])
            .Add(static p => p.Thresholds, Levels)
            .Add(static p => p.ThresholdMarkersVisible, true));

        Assert.Empty(plain.FindAll(".mussel-radial-markers path"));
        Assert.Equal(3, marked.FindAll(".mussel-radial-markers path").Count);
    }

    [Fact]
    public void ThresholdsThatAreNotNumbersDrawAsNoThresholds()
    {
        ChartThreshold[] thresholds = [new() { Value = Double.NaN, Level = ChartThresholdLevel.Critical }];
        var plain = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 50)])
            .Add(static p => p.FillMode, ChartFillMode.Gradient)
            .Add(static p => p.ThresholdMarkersVisible, true));
        var marked = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 50)])
            .Add(static p => p.FillMode, ChartFillMode.Gradient)
            .Add(p => p.Thresholds, thresholds)
            .Add(static p => p.ThresholdMarkersVisible, true));

        Assert.Empty(marked.FindAll(".mussel-radial-markers > *"));
        Assert.Equal(plain.Find(".mussel-radial-sweep div").GetAttribute("style"), marked.Find(".mussel-radial-sweep div").GetAttribute("style"));
        Assert.Equal(plain.Find(".mussel-radial-track").GetAttribute("d"), marked.Find(".mussel-radial-track").GetAttribute("d"));
    }

    [Fact]
    public void GlowCopiesTheValueUnderTheRings()
    {
        var cut = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 50)])
            .Add(static p => p.BarGlow, true));

        Assert.NotNull(cut.Find("filter feGaussianBlur"));
        Assert.Single(cut.FindAll(".mussel-radial-glow path"));
    }

    [Fact]
    public void GlowCopiesTheRoundEndsOfAGradient()
    {
        var cut = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 50, "#5794F2")])
            .Add(static p => p.FillMode, ChartFillMode.Gradient)
            .Add(static p => p.CapStyle, ChartCapStyle.Round)
            .Add(static p => p.BarGlow, true));
        cut.Render();

        var ends = cut.Find(".mussel-ring").QuerySelectorAll("circle").Select(static x => (x.GetAttribute("cx"), x.GetAttribute("cy"), x.GetAttribute("fill"))).ToList();
        Assert.Equal(2, ends.Count);
        Assert.Equal(ends, cut.FindAll(".mussel-radial-glow circle").Select(static x => (x.GetAttribute("cx"), x.GetAttribute("cy"), x.GetAttribute("fill"))));
    }

    [Fact]
    public void GlowCopiesTheThresholdRingInEitherFill()
    {
        var solid = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 50)])
            .Add(static p => p.Thresholds, Levels)
            .Add(static p => p.ThresholdMarkersVisible, true)
            .Add(static p => p.BarGlow, true));
        var gradient = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 50)])
            .Add(static p => p.FillMode, ChartFillMode.Gradient)
            .Add(static p => p.Thresholds, Levels)
            .Add(static p => p.ThresholdMarkersVisible, true)
            .Add(static p => p.BarGlow, true));

        var arcs = solid.FindAll(".mussel-radial-markers path").Select(static x => x.GetAttribute("d")).ToList();
        var glows = solid.FindAll(".mussel-radial-glow path").Select(static x => x.GetAttribute("d")).ToList();
        Assert.Equal(3, arcs.Count);
        Assert.All(arcs, x => Assert.Contains(x, glows));
        var sweep = gradient.Find(".mussel-radial-markers .mussel-radial-sweep div").GetAttribute("style");
        Assert.Contains(sweep, gradient.FindAll(".mussel-radial-glow .mussel-radial-sweep div").Select(static x => x.GetAttribute("style")));
    }

    [Fact]
    public void RoundEndsApplyToTheArcs()
    {
        var cut = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 50)])
            .Add(static p => p.CapStyle, ChartCapStyle.Round));

        Assert.NotEmpty(cut.FindAll(".mussel-ring path"));
        Assert.All(cut.FindAll(".mussel-ring path"), static x => Assert.Equal("round", x.GetAttribute("stroke-linecap")));
    }

    [Fact]
    public void OnlyRoundEndsThatAreDrawnShrinkAPartialRing()
    {
        var flat = new RadialBarLayout();
        var round = new RadialBarLayout();
        var segmented = new RadialBarLayout();

        flat.Update(640, 240, new RadialBarLayoutOptions { StartAngle = 250, SweepAngle = 220, Culture = CultureInfo.InvariantCulture }, [new ChartEntry("CPU", 50)]);
        round.Update(640, 240, new RadialBarLayoutOptions { StartAngle = 250, SweepAngle = 220, CapStyle = ChartCapStyle.Round, Culture = CultureInfo.InvariantCulture }, [new ChartEntry("CPU", 50)]);
        segmented.Update(640, 240, new RadialBarLayoutOptions { StartAngle = 250, SweepAngle = 220, CapStyle = ChartCapStyle.Round, DisplayMode = ChartRadialBarMode.Segmented, Culture = CultureInfo.InvariantCulture }, [new ChartEntry("CPU", 50)]);

        Assert.True(round.OuterRadius < flat.OuterRadius);
        Assert.Equal(flat.OuterRadius, segmented.OuterRadius, 6);
        Assert.Equal(flat.Thickness, segmented.Thickness, 6);
    }

    [Fact]
    public void LegendAddsTheValues()
    {
        var cut = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 50), new ChartEntry("B", 25)])
            .Add(static p => p.LegendValueMode, ChartLegendValueMode.Value)
            .Add(static p => p.ValueFormat, "N0")
            .Add(static p => p.Culture, CultureInfo.InvariantCulture));

        Assert.Equal(["50", "25"], cut.FindAll(".mussel-view-legend-value").Select(static x => x.TextContent));
        Assert.Contains("mussel-view-legend-right", cut.Find(".mussel-view").ClassName, StringComparison.Ordinal);
    }

    [Fact]
    public void ItemWithoutAValueDrawsOnlyTheTrack()
    {
        var cut = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", Double.NaN)]));

        var arcs = cut.Find(".mussel-ring").QuerySelectorAll("path");
        Assert.Single(arcs);
        Assert.Equal("mussel-radial-track", arcs[0].ClassList[0]);
    }

    [Fact]
    public void ThicknessIsLimitedAndRingsMoveInward()
    {
        var layout = new RadialBarLayout();

        layout.Update(216, 216, new RadialBarLayoutOptions { Culture = CultureInfo.InvariantCulture }, [new ChartEntry("A", 1), new ChartEntry("B", 2)]);

        Assert.Equal(100, layout.OuterRadius, 3);
        Assert.Equal(100d / 6, layout.Thickness, 3);
        Assert.True(layout.GetRingRadius(1) < layout.GetRingRadius(0));
    }

    [Fact]
    public void TheCenterOfAnUpperHalfGaugeStaysInsideTheView()
    {
        var layout = new RadialBarLayout();

        layout.Update(640, 220, new RadialBarLayoutOptions { StartAngle = 270, SweepAngle = 180, Culture = CultureInfo.InvariantCulture }, [new ChartEntry("CPU", 50)]);

        var center = layout.Center!;
        Assert.Equal("CPU", center.Caption);
        Assert.True(center.CaptionY + (center.CaptionFontSize / 2) <= 212);
    }

    [Fact]
    public void TheCenterOfAQuarterGaugeStaysInsideThePlotSideways()
    {
        var layout = new RadialBarLayout();

        layout.Update(220, 220, new RadialBarLayoutOptions { StartAngle = 0, SweepAngle = 90, Culture = CultureInfo.InvariantCulture }, [new ChartEntry("CPU", 50)]);

        var center = layout.Center!;
        var half = Math.Max(ChartText.Measure(center.Value, center.ValueFontSize), ChartText.Measure(center.Caption, center.CaptionFontSize)) / 2;
        Assert.Equal("CPU", center.Caption);
        Assert.InRange(center.X - half, 8, 212);
        Assert.InRange(center.X + half, 8, 212);
    }

    [Fact]
    public void AnEndThatIsNotFiniteLeavesNoNaN()
    {
        var cut = Render<MusselRadialBarView>(parameters => parameters
            .Add(static p => p.Items, [new ChartEntry("A", 50)])
            .Add(static p => p.Minimum, Double.NegativeInfinity)
            .Add(static p => p.LegendValueMode, ChartLegendValueMode.Percent)
            .Add(static p => p.Culture, CultureInfo.InvariantCulture));

        Assert.DoesNotContain("NaN", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void CellsAreFewerOnShortRings()
    {
        Assert.Equal(20, RadialBarLayout.GetSegmentCount(100, 10, 360, 20, 2));
        Assert.True(RadialBarLayout.GetSegmentCount(10, 4, 90, 20, 2) < 20);
    }

    [Fact]
    public void ThresholdStopsSitAtTheirPositions()
    {
        var (colors, positions) = RadialBarGradient.GetThresholdStops(
            [new ChartThreshold { Value = Double.NegativeInfinity, Color = "#111111" }, new ChartThreshold { Value = 50, Color = "#222222" }, new ChartThreshold { Value = 120, Color = "#333333" }],
            "#000000");

        Assert.Equal(["#111111", "#222222", "#222222"], colors);
        Assert.Equal([0, 0.5, 1], positions);
    }

    [Fact]
    public void ColorStopsEndAtTheValue()
    {
        var (colors, positions) = RadialBarGradient.GetColorStops("#3274D9", 0.4);

        Assert.Equal([0, 0.4, 1], positions);
        Assert.Equal(colors[1], colors[2]);
        Assert.Equal(colors[0], RadialBarGradient.GetColorAt(colors, positions, 0));
    }

    [Fact]
    public void ARingBufferIsCopiedBeforeItIsRead()
    {
        var buffer = new ChartRingBuffer<ChartEntry>(4);
        buffer.Push(new ChartEntry("A", 30, null, 100));
        buffer.Push(new ChartEntry("B", 50, null, 100));
        var seen = new HashSet<double>();

        Render<MusselRadialBarView>(parameters => parameters
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

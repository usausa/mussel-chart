namespace MusselChart;

using System.Text.RegularExpressions;

public sealed partial class StyleSheetTests
{
    [Fact]
    public void EveryThemeVariableIsDeclaredWithoutSpecificity()
    {
        var css = File.ReadAllText(Path.Combine(TestPaths.Root, "MusselChart", "wwwroot", "MusselChart.css"));

        var selectors = Rule().Matches(css)
            .Where(static x => Declaration().IsMatch(x.Groups["body"].Value))
            .SelectMany(static x => TopLevelComma().Split(x.Groups["selector"].Value))
            .Select(static x => x.Trim())
            .ToList();

        Assert.NotEmpty(selectors);
        Assert.All(selectors, static x => Assert.Matches(WhereOnly(), x));
    }

    [Fact]
    public void ScopedStyleSheetsDeclareNoThemeVariables()
    {
        var files = Directory.GetFiles(Path.Combine(TestPaths.Root, "MusselChart"), "*.razor.css", SearchOption.AllDirectories);

        Assert.NotEmpty(files);
        Assert.All(files, static x => Assert.DoesNotContain(Rule().Matches(File.ReadAllText(x)), static y => Declaration().IsMatch(y.Groups["body"].Value)));
    }

    [Theory]
    [InlineData("MusselChartView.razor.css", ".mussel-chart")]
    [InlineData("Internal/ChartFrame.razor.css", ".mussel-view")]
    [InlineData("MusselStatView.razor.css", ".mussel-stat")]
    [InlineData("MusselBarGaugeView.razor.css", ".mussel-gauge")]
    public void EveryViewWithTextTakesTheFontFamilyOfTheTheme(string file, string root)
    {
        var css = File.ReadAllText(Path.Combine(TestPaths.Root, "MusselChart", file));

        var body = Rule().Matches(css).Single(x => x.Groups["selector"].Value.Trim() == root).Groups["body"].Value;

        Assert.Contains("font-family: var(--mussel-font-family, inherit);", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("MusselPieChartView.razor.css", ".mussel-slices-hover:hover .mussel-slice:not(:hover)")]
    [InlineData("MusselRadialBarView.razor.css", ".mussel-rings-hover:hover .mussel-ring:not(:hover)")]
    public void DimmingTheOthersNeedsAPointerThatHovers(string file, string selector)
    {
        var css = File.ReadAllText(Path.Combine(TestPaths.Root, "MusselChart", file));

        var start = css.IndexOf("@media (hover: hover)", StringComparison.Ordinal);

        Assert.True(start >= 0);
        Assert.True(css.IndexOf(selector, StringComparison.Ordinal) > start);
    }

    [Fact]
    public void CornersAreNotRounded()
    {
        var files = Directory.GetFiles(Path.Combine(TestPaths.Root, "MusselChart"), "*.razor.css", SearchOption.AllDirectories)
            .Append(Path.Combine(TestPaths.Root, "MusselChart", "wwwroot", "MusselChart.css"))
            .ToList();

        Assert.True(files.Count > 1);
        Assert.All(files, static x => Assert.DoesNotMatch(RoundedCorner(), File.ReadAllText(x)));
    }

    [GeneratedRegex(@"(?<selector>[^{}]+)\{(?<body>[^{}]*)\}")]
    private static partial Regex Rule();

    [GeneratedRegex(@"(?:^|;)\s*--mussel-[a-z0-9-]+\s*:")]
    private static partial Regex Declaration();

    [GeneratedRegex(@",(?![^(]*\))")]
    private static partial Regex TopLevelComma();

    [GeneratedRegex(@"border-radius\s*:(?!\s*50%)")]
    private static partial Regex RoundedCorner();

    [GeneratedRegex(@"^(?::where\([^()]*\)\s*)+$")]
    private static partial Regex WhereOnly();
}

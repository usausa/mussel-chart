namespace Example.Components.Pages;

public sealed partial class RadialPage
{
    private static readonly ChartThreshold[] LoadThresholds =
    [
        new() { Level = ChartThresholdLevel.Good },
        new() { Value = 60, Level = ChartThresholdLevel.Warning },
        new() { Value = 85, Level = ChartThresholdLevel.Critical }
    ];

    private static readonly ChartEntry[] Activities =
    [
        new("Move", 120),
        new("Exercise", 58),
        new("Stand", 75)
    ];

    private static readonly ChartEntry[] Cpu =
    [
        new("CPU", 72)
    ];

    private static readonly ChartEntry[] Memory =
    [
        new("Memory", 64)
    ];

    private static readonly ChartEntry[] Servers =
    [
        new("web-01", 35),
        new("web-02", 62),
        new("db-01", 88),
        new("batch", 15)
    ];
}

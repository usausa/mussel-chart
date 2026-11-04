namespace Example.Components.Pages;

public sealed partial class WidgetsPage
{
    private static readonly double[] Trend = [12, 14, 13, 17, 21, 19, 24, 22, 27, 25, 29, 31, 28, 33, 30, 34];

    private static readonly double[] Changes = [3, -2, 5, 4, -1, -3, 6, 2, -4, 5, 3, -2];

    private static readonly ChartThreshold[] ErrorThresholds =
    [
        new() { Level = ChartThresholdLevel.Good },
        new() { Value = 2, Level = ChartThresholdLevel.Warning },
        new() { Value = 5, Level = ChartThresholdLevel.Critical }
    ];

    private static readonly ChartThreshold[] LatencyThresholds =
    [
        new() { Level = ChartThresholdLevel.Good },
        new() { Value = 150, Level = ChartThresholdLevel.Serious },
        new() { Value = 300, Level = ChartThresholdLevel.Critical }
    ];

    private static readonly ChartThreshold[] DiskThresholds =
    [
        new() { Level = ChartThresholdLevel.Good },
        new() { Value = 70, Level = ChartThresholdLevel.Warning },
        new() { Value = 85, Level = ChartThresholdLevel.Critical }
    ];

    private static readonly ChartEntry[] Stores =
    [
        new("Shibuya", 8420, null, 10000, 9000),
        new("Shinjuku", 6150, null, 8000, 7000),
        new("Ikebukuro", 4980, null, 6000, 5500)
    ];

    private static readonly ChartEntry[] Disks =
    [
        new("C: System", 57, null, 100),
        new("D: Data", 23, null, 100),
        new("E: Work", 76, null, 100),
        new("F: Backup", 91, null, 100)
    ];
}

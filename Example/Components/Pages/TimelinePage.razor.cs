namespace Example.Components.Pages;

public sealed partial class TimelinePage
{
    private static readonly DateTime Origin = new(2026, 7, 1, 8, 0, 0, DateTimeKind.Unspecified);

    private static readonly DateTime Now = Origin.AddHours(8);

    private static readonly ChartValueMapping[] Mappings =
    [
        new() { Value = "Running", Text = "Running", Level = ChartThresholdLevel.Good },
        new() { Value = "Degraded", Text = "Degraded", Level = ChartThresholdLevel.Warning },
        new() { Value = "Stopped", Text = "Stopped", Level = ChartThresholdLevel.Critical }
    ];

    private static readonly ChartThreshold[] LoadThresholds =
    [
        new() { Level = ChartThresholdLevel.Good, Label = "Low" },
        new() { Value = 60, Level = ChartThresholdLevel.Warning, Label = "Busy" },
        new() { Value = 85, Level = ChartThresholdLevel.Critical, Label = "Full" }
    ];

    private static readonly ServerState[] States =
    [
        new("web-01", Origin, "Running"),
        new("web-01", Origin.AddMinutes(190), "Degraded"),
        new("web-01", Origin.AddMinutes(240), "Running"),
        new("web-02", Origin, "Running"),
        new("web-02", Origin.AddMinutes(310), "Stopped"),
        new("web-02", Origin.AddMinutes(360), "Running"),
        new("db-01", Origin, "Degraded"),
        new("db-01", Origin.AddMinutes(60), "Running"),
        new("batch", Origin.AddMinutes(30), "Stopped"),
        new("batch", Origin.AddMinutes(120), "Running"),
        new("batch", Origin.AddMinutes(400), "Degraded")
    ];

    private static readonly LoadLevel[] Levels = CreateLevels();

    private static readonly JobState[] Jobs =
    [
        new("Worker 1", Origin, Origin.AddMinutes(90), "Import"),
        new("Worker 1", Origin.AddMinutes(120), Origin.AddMinutes(300), "Export"),
        new("Worker 2", Origin.AddMinutes(45), Origin.AddMinutes(200), "Report"),
        new("Worker 2", Origin.AddMinutes(200), Origin.AddMinutes(420), "Import")
    ];

    private static LoadLevel[] CreateLevels()
    {
        var servers = new[] { "web-01", "web-02", "db-01" };
        return (from server in Enumerable.Range(0, servers.Length)
                from step in Enumerable.Range(0, 16)
                select new LoadLevel(servers[server], Origin.AddMinutes(step * 30), 45 + (50 * Math.Sin((step * 0.6) + (server * 1.2))))).ToArray();
    }

    private sealed record ServerState(string Server, DateTime Start, string State);

    private sealed record LoadLevel(string Server, DateTime Start, double Load);

    private sealed record JobState(string Worker, DateTime Start, DateTime End, string State);
}

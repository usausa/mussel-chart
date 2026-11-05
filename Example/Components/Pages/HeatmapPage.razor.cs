namespace Example.Components.Pages;

public sealed partial class HeatmapPage
{
    private static readonly string[] Days = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

    private static readonly string[] BlueScale = ["#EAF2FB", "#B6D0F2", "#6D9CE8", "#3274D9", "#1F60C4"];

    private static readonly AccessCount[] Accesses = CreateAccesses();

    private static readonly ActivityDay[] Activities = CreateActivities();

    private static readonly ServerLoad[] Loads = CreateLoads();

    private static AccessCount[] CreateAccesses() =>
        (from day in Enumerable.Range(0, Days.Length)
         from hour in Enumerable.Range(0, 24)
         select new AccessCount(Days[day], hour.ToString(CultureInfo.InvariantCulture), Math.Round((Math.Exp(-Math.Pow(hour - 14, 2) / 40) * (day < 5 ? 130 : 70)) + ((hour * 3 * (day + 1)) % 11)))).ToArray();

    private static ActivityDay[] CreateActivities()
    {
        var start = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Unspecified);
        return Enumerable.Range(0, 182)
            .Where(static i => (i % 11) != 5)
            .Select(i => new ActivityDay(start.AddDays(i), (i * 7) % 13))
            .ToArray();
    }

    private static ServerLoad[] CreateLoads()
    {
        var servers = new[] { "web-01", "web-02", "db-01", "batch" };
        return (from server in Enumerable.Range(0, servers.Length)
                from time in Enumerable.Range(0, 12)
                where !((server == 3) && (time is > 3 and < 7))
                select new ServerLoad(servers[server], FormattableString.Invariant($"{time * 2:00}:00"), (((server + 2) * (time + 3) * 13) % 90) + 8)).ToArray();
    }

    private sealed record AccessCount(string Day, string Hour, double Count);

    private sealed record ActivityDay(DateTime Date, double Value);

    private sealed record ServerLoad(string Server, string Time, double Load);
}

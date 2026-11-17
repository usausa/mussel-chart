namespace MusselChart;

using MusselChart.Internal;

public sealed class ChartTimeTests
{
    [Fact]
    public void RepeatedLocalTimesAndTheEndsOfDateTimeAreRead()
    {
        var berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        var tokyo = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");
        var newYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

        Assert.Equal(new DateTime(2026, 10, 25, 1, 30, 0, DateTimeKind.Utc).Ticks, ChartTime.ToUtcTicks(new DateTime(2026, 10, 25, 2, 30, 0, DateTimeKind.Unspecified), berlin));
        Assert.Equal(0, ChartTime.ToUtcTicks(DateTime.MinValue, tokyo));
        Assert.Equal(DateTime.MaxValue.Ticks, ChartTime.ToUtcTicks(DateTime.MaxValue, newYork));
        Assert.Equal(-5 * TimeSpan.TicksPerHour, ChartTime.OffsetOf(-TimeSpan.TicksPerDay, newYork));
    }

    [Fact]
    public void ZoneTimeFollowsEveryChangeOfTheOffset()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var start = new DateTime(2025, 11, 17, 5, 0, 0, DateTimeKind.Utc).Ticks;
        var end = new DateTime(2026, 11, 16, 5, 0, 0, DateTimeKind.Utc).Ticks;
        var ticks = new List<(long Utc, double Local)>();

        TickGenerator.ZoneTime(start, end, zone, new TimeInterval(ChartDateTimeIntervalType.Months, 1), DayOfWeek.Sunday, [], ticks, []);

        Assert.Equal(new DateTime(2025, 12, 1, 5, 0, 0, DateTimeKind.Utc).Ticks, ticks.Single(static x => new DateTime((long)x.Local).Month == 12).Utc);
        Assert.Equal(new DateTime(2026, 7, 1, 4, 0, 0, DateTimeKind.Utc).Ticks, ticks.Single(static x => new DateTime((long)x.Local).Month == 7).Utc);
    }
}

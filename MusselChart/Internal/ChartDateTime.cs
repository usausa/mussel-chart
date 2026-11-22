namespace MusselChart.Internal;

internal static class ChartDateTime
{
    public static long ClampTicks(double ticks) => Double.IsNaN(ticks) ? 0 : Math.Min((long)Math.Clamp(ticks, 0, DateTime.MaxValue.Ticks), DateTime.MaxValue.Ticks);

    public static DateTime FromTicks(double ticks) => new(ClampTicks(ticks));

    public static int YearOf(long ticks) => new DateTime(Math.Clamp(ticks, 0, DateTime.MaxValue.Ticks)).Year;
}

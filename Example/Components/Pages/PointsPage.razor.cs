namespace Example.Components.Pages;

public sealed partial class PointsPage
{
    private static readonly PointItem[] GroupA = CreateGroup(158, 52, 18, 3);

    private static readonly PointItem[] GroupB = CreateGroup(170, 65, 18, 5);

    private static readonly PointItem[] GroupC = CreateGroup(180, 76, 14, 7);

    private static readonly BubbleItem[] Regions =
    [
        new(12, 62, 120),
        new(24, 70, 340),
        new(35, 58, 80),
        new(48, 76, 510),
        new(66, 81, 260),
        new(78, 74, 45)
    ];

    private static readonly BubbleItem[] Cities =
    [
        new(18, 48, 60),
        new(30, 82, 150),
        new(55, 66, 25),
        new(72, 60, 90)
    ];

    private static readonly PointItem[] Curve = Enumerable.Range(0, 41).Select(static i => new PointItem(i * 0.25, Math.Sin(i * 0.25) + (i * 0.05))).ToArray();

    private static readonly PointItem[] Measured = Enumerable.Range(0, 11).Select(static i => new PointItem(i, Math.Sin(i) + (i * 0.2) + (((i * 7) % 5) * 0.08) - 0.16)).ToArray();

    private static readonly EventItem[] Events = Enumerable.Range(0, 40)
        .Select(static i => new EventItem(new DateTime(2026, 7, 1, 9, 0, 0, DateTimeKind.Unspecified).AddMinutes(i * 7), 120 + (90 * Math.Abs(Math.Sin(i * 1.3))) + ((i * 37) % 60)))
        .ToArray();

    private static PointItem[] CreateGroup(double x, double y, int count, int seed) =>
        Enumerable.Range(0, count)
            .Select(i => new PointItem(
                x + (Math.Sin((i * 1.7) + seed) * 8),
                y + (Math.Sin((i * 1.7) + seed) * 6) + (Math.Cos((i * 2.3) + seed) * 5)))
            .ToArray();

    private sealed record PointItem(double X, double Y);

    private sealed record BubbleItem(double X, double Y, double Size);

    private sealed record EventItem(DateTime Time, double Value);
}

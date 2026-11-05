namespace Example.Components.Pages;

public sealed partial class BarsPage
{
    private static readonly MonthlySales[] Sales =
    [
        new("Jan", 120, 80, 40, 190),
        new("Feb", 135, 92, 38, 200),
        new("Mar", 150, 88, 52, 220),
        new("Apr", 142, 110, 47, 240),
        new("May", 168, 124, 60, 260),
        new("Jun", 175, 131, 58, 280)
    ];

    private static readonly RankItem[] Ranking =
    [
        new("Shibuya", 8420),
        new("Shinjuku", 6150),
        new("Ikebukuro", 4980),
        new("Ueno", 3760),
        new("Akihabara", 2910),
        new("Yokohama Minato Mirai", 2240)
    ];

    private static readonly MonthlyTemperature[] Temperatures =
    [
        new("Jan", 1.2, 9.8, 5.2),
        new("Feb", 2.1, 11.0, 6.1),
        new("Mar", 5.0, 14.2, 9.4),
        new("Apr", 10.3, 19.4, 14.3),
        new("May", 15.1, 23.6, 18.8),
        new("Jun", 19.0, 26.1, 21.9),
        new("Jul", 22.9, 29.9, 25.7),
        new("Aug", 24.2, 31.3, 26.9)
    ];

    private static readonly FlowPoint[] Flows = CreateFlows();

    private static FlowPoint[] CreateFlows() =>
        Enumerable.Range(0, 12)
            .Select(static i => new FlowPoint(
                new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Unspecified).AddDays(i),
                Math.Round(60 + (30 * Math.Sin(i * 0.7))),
                Math.Round(25 + (10 * Math.Cos(i * 0.5))),
                -Math.Round(40 + (20 * Math.Sin((i * 0.6) + 1)))))
            .ToArray();

    private sealed record MonthlySales(string Month, double Store, double Online, double Wholesale, double Target);

    private sealed record RankItem(string Name, double Value);

    private sealed record MonthlyTemperature(string Month, double Low, double High, double Mean);

    private sealed record FlowPoint(DateTime Time, double In, double Other, double Out);
}

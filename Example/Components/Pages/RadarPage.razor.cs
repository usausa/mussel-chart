namespace Example.Components.Pages;

public sealed partial class RadarPage
{
    private static readonly SkillScore[] ProductA =
    [
        new("Price", 72),
        new("Quality", 88),
        new("Design", 64),
        new("Support", 91),
        new("Delivery", 55),
        new("Warranty", 78)
    ];

    private static readonly SkillScore[] ProductB =
    [
        new("Price", 90),
        new("Quality", 62),
        new("Design", 81),
        new("Support", 48),
        new("Delivery", 84),
        new("Warranty", 58)
    ];

    private static readonly ChartEntry[] Team =
    [
        new("Speed", 7),
        new("Accuracy", 9),
        new("Teamwork", 6),
        new("Planning", 8),
        new("Ideas", 5)
    ];

    private static readonly ChartEntry[] Average =
    [
        new("Speed", 6),
        new("Accuracy", 6),
        new("Teamwork", 7),
        new("Planning", 5),
        new("Ideas", 7)
    ];

    private sealed record SkillScore(string Name, double Score);
}

namespace MusselChart;

public sealed record ChartThreshold
{
    public double Value { get; init; } = Double.NegativeInfinity;

    public ChartThresholdLevel Level { get; init; }

    public string? Color { get; init; }

    public string? Label { get; init; }
}

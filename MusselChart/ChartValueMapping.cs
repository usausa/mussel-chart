namespace MusselChart;

public sealed record ChartValueMapping
{
    public object? Value { get; init; }

    public string? Text { get; init; }

    public string? Color { get; init; }

    public ChartThresholdLevel Level { get; init; }
}

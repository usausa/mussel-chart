namespace MusselChart;

public sealed record ChartEntry(string? Label, double Value, string? Color = null, double? Maximum = null, double? Target = null);

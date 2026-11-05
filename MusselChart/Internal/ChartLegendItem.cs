namespace MusselChart.Internal;

public sealed record ChartLegendItem(string Key, string Text, string Color, string? Value = null, bool Hidden = false);

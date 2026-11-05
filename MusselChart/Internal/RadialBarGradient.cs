namespace MusselChart.Internal;

internal static class RadialBarGradient
{
    private const double Spin = 20;

    private const double LightDarken = 5;

    private const double Saturate = 20;

    private const double Brighten = 10;

    private const double Lighten = 10;

    public static (string[] Colors, double[] Positions) GetThresholdStops(IReadOnlyList<(double Value, string Color)> sorted, string baseColor)
    {
        var colors = new List<string>(sorted.Count + 2);
        var positions = new List<double>(sorted.Count + 2);
        if ((sorted.Count == 0) || !Double.IsNegativeInfinity(sorted[0].Value))
        {
            colors.Add(baseColor);
            positions.Add(0);
        }

        foreach (var (value, color) in sorted)
        {
            if (value > 100)
            {
                break;
            }

            colors.Add(color);
            positions.Add(Double.IsNegativeInfinity(value) ? 0 : Math.Clamp(value / 100, 0, 1));
        }

        if (positions[^1] < 1)
        {
            colors.Add(colors[^1]);
            positions.Add(1);
        }

        return (colors.ToArray(), positions.ToArray());
    }

    public static (string[] Colors, double[] Positions) GetColorStops(string color, double ratio)
    {
        var darker = ChartColor.Shift(color, -Spin, 0, -LightDarken);
        var lighter = ChartColor.Shift(ChartColor.Brighten(ChartColor.Shift(color, Spin, Saturate, 0), Brighten), 0, 0, Lighten);
        var value = Math.Clamp(ratio, 0, 1);
        var end = ChartColor.Lerp(lighter, darker, value);
        return ([lighter, end, end], [0, value, 1]);
    }

    public static string GetColorAt(string[] colors, double[] positions, double position)
    {
        if (position <= positions[0])
        {
            return colors[0];
        }

        for (var i = 1; i < positions.Length; i++)
        {
            if (position <= positions[i])
            {
                var range = positions[i] - positions[i - 1];
                return range <= 0 ? colors[i] : ChartColor.Lerp(colors[i - 1], colors[i], (position - positions[i - 1]) / range);
            }
        }

        return colors[^1];
    }
}

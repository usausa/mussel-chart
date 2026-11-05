namespace MusselChart.Internal;

// The colors are spread with the B-spline of d3 (interpolateRgbBasis), which gives the colors of the Grafana heatmap
internal static class HeatmapPalette
{
    public const int Steps = 64;

    public static string[] Build(IReadOnlyList<string> stops)
    {
        var parsed = Parse(stops);
        var colors = new string[Steps];
        var last = colors.Length - 1;
        for (var i = 0; i <= last; i++)
        {
            colors[i] = GetColor(parsed, (double)i / last);
        }

        return colors;
    }

    public static string GetColor(IReadOnlyList<string> stops, double t) => GetColor(Parse(stops), t);

    public static int GetIndex(double value, double minimum, double maximum, int count)
    {
        var range = maximum - minimum;
        if (!(range > 0))
        {
            range = 1;
        }

        if (value < minimum)
        {
            return 0;
        }

        return value > maximum ? count - 1 : Math.Min(count - 1, (int)Math.Floor(count * (value - minimum) / range));
    }

    private static (int R, int G, int B)[] Parse(IReadOnlyList<string> stops)
    {
        var parsed = new (int R, int G, int B)[Math.Max(stops.Count, 2)];
        for (var i = 0; i < parsed.Length; i++)
        {
            parsed[i] = (i < stops.Count) && ChartColor.TryParse(stops[i], out var color) ? color : (128, 128, 128);
        }

        return parsed;
    }

    private static string GetColor((int R, int G, int B)[] stops, double t) =>
        ChartColor.ToHex((Interpolate(stops, t, 0), Interpolate(stops, t, 1), Interpolate(stops, t, 2)));

    private static int Interpolate((int R, int G, int B)[] stops, double t, int channel)
    {
        var n = stops.Length - 1;
        int i;
        if (t <= 0)
        {
            t = 0;
            i = 0;
        }
        else if (t >= 1)
        {
            t = 1;
            i = n - 1;
        }
        else
        {
            i = (int)Math.Floor(t * n);
        }

        var v1 = GetChannel(stops[i], channel);
        var v2 = GetChannel(stops[i + 1], channel);
        var v0 = i > 0 ? GetChannel(stops[i - 1], channel) : (2 * v1) - v2;
        var v3 = i < n - 1 ? GetChannel(stops[i + 2], channel) : (2 * v2) - v1;
        return (int)Math.Clamp(Math.Floor(Basis((t - ((double)i / n)) * n, v0, v1, v2, v3) + 0.5), 0, 255);
    }

    private static double Basis(double t1, double v0, double v1, double v2, double v3)
    {
        var t2 = t1 * t1;
        var t3 = t2 * t1;
        return (((1 - (3 * t1) + (3 * t2) - t3) * v0) + ((4 - (6 * t2) + (3 * t3)) * v1) + ((1 + (3 * t1) + (3 * t2) - (3 * t3)) * v2) + (t3 * v3)) / 6;
    }

    private static double GetChannel((int R, int G, int B) color, int channel) => channel switch
    {
        0 => color.R,
        1 => color.G,
        _ => color.B
    };
}

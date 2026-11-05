namespace MusselChart.Internal;

using System.Globalization;

internal static class ChartColor
{
    public const string Other = "#B5B3AA";

    private const string DarkText = "#0B0B0B";

    private const string LightText = "#FFFFFF";

    private static readonly double DarkTextLuminance = GetLuminance((0x0B, 0x0B, 0x0B));

    public static string Level(ChartThresholdLevel level) => level switch
    {
        ChartThresholdLevel.Good => "#73BF69",
        ChartThresholdLevel.Warning => "#FADE2A",
        ChartThresholdLevel.Serious => "#FF9830",
        ChartThresholdLevel.Critical => "#F2495C",
        _ => "#8E8C86"
    };

    public static string Threshold(ChartThreshold? threshold, string fallback)
    {
        if (threshold is null)
        {
            return fallback;
        }

        return threshold.Color ?? (threshold.Level == ChartThresholdLevel.None ? fallback : Level(threshold.Level));
    }

    public static bool TryParse(string? text, out (int R, int G, int B) color)
    {
        color = default;
        if ((text is null) || (text.Length == 0) || (text[0] != '#'))
        {
            return false;
        }

        var hex = text.AsSpan(1);
        if (hex.Length == 3)
        {
            Span<char> expanded = [hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]];
            return TryParseHex(expanded, out color);
        }

        return hex.Length switch
        {
            6 => TryParseHex(hex, out color),
            8 => TryParseHex(hex[2..], out color),
            _ => false
        };
    }

    public static double Brightness(string? text) =>
        TryParse(text, out var color) ? ((color.R * 299) + (color.G * 587) + (color.B * 114)) / 1000d : 0;

    public static string Shift(string text, double hue, double lightness) => Shift(text, hue, 0, lightness);

    public static string Shift(string text, double hue, double saturation, double lightness)
    {
        if (!TryParse(text, out var color))
        {
            return text;
        }

        var (h, s, l) = ToHsl(color);
        h = (((h + hue) % 360) + 360) % 360;
        s = Math.Clamp(s + (saturation / 100), 0, 1);
        l = Math.Clamp(l + (lightness / 100), 0, 1);
        return ToHex(FromHsl(h, s, l));
    }

    public static string Brighten(string text, double percent)
    {
        if (!TryParse(text, out var color))
        {
            return text;
        }

        var delta = (int)Math.Round(255 * percent / 100);
        return ToHex((Math.Min(255, color.R + delta), Math.Min(255, color.G + delta), Math.Min(255, color.B + delta)));
    }

    public static string Lerp(string from, string to, double progress)
    {
        if (!TryParse(from, out var start) || !TryParse(to, out var end))
        {
            return to;
        }

        var t = Math.Clamp(progress, 0, 1);
        return ToHex((Lerp(start.R, end.R, t), Lerp(start.G, end.G, t), Lerp(start.B, end.B, t)));
    }

    // White or near black, whichever has the higher WCAG contrast with the background
    public static string Contrast(string background)
    {
        if (!TryParse(background, out var color))
        {
            return DarkText;
        }

        var luminance = GetLuminance(color);
        return (luminance + 0.05) / (DarkTextLuminance + 0.05) >= 1.05 / (luminance + 0.05) ? DarkText : LightText;
    }

    public static string ToHex((int R, int G, int B) color) =>
        String.Create(CultureInfo.InvariantCulture, $"#{color.R:X2}{color.G:X2}{color.B:X2}");

    private static int Lerp(int from, int to, double progress) => (int)Math.Round(from + ((to - from) * progress));

    private static double GetLuminance((int R, int G, int B) color) =>
        (0.2126 * ToLinear(color.R)) + (0.7152 * ToLinear(color.G)) + (0.0722 * ToLinear(color.B));

    private static double ToLinear(int channel)
    {
        var value = channel / 255d;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    private static bool TryParseHex(ReadOnlySpan<char> hex, out (int R, int G, int B) color)
    {
        color = default;
        if (!Int32.TryParse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) ||
            !Int32.TryParse(hex.Slice(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) ||
            !Int32.TryParse(hex.Slice(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            return false;
        }

        color = (r, g, b);
        return true;
    }

    private static (double H, double S, double L) ToHsl((int R, int G, int B) color)
    {
        var r = color.R / 255d;
        var g = color.G / 255d;
        var b = color.B / 255d;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2;
        if (max - min < 1e-9)
        {
            return (0, 0, l);
        }

        var d = max - min;
        var s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        double h;
        if ((r >= g) && (r >= b))
        {
            h = ((g - b) / d) + (g < b ? 6 : 0);
        }
        else if (g >= b)
        {
            h = ((b - r) / d) + 2;
        }
        else
        {
            h = ((r - g) / d) + 4;
        }

        return (h * 60, s, l);
    }

    private static (int R, int G, int B) FromHsl(double h, double s, double l)
    {
        if (s < 1e-9)
        {
            var gray = (int)Math.Round(l * 255);
            return (gray, gray, gray);
        }

        var q = l < 0.5 ? l * (1 + s) : l + s - (l * s);
        var p = (2 * l) - q;
        var hk = h / 360;
        return (ToChannel(p, q, hk + (1d / 3)), ToChannel(p, q, hk), ToChannel(p, q, hk - (1d / 3)));
    }

    private static int ToChannel(double p, double q, double t)
    {
        t = t < 0 ? t + 1 : t > 1 ? t - 1 : t;
        var value = t < 1d / 6 ? p + ((q - p) * 6 * t)
            : t < 1d / 2 ? q
            : t < 2d / 3 ? p + ((q - p) * ((2d / 3) - t) * 6)
            : p;
        return (int)Math.Round(Math.Clamp(value, 0, 1) * 255);
    }
}

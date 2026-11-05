namespace MusselChart.Internal;

using System.Text;

internal static class ChartText
{
    public const string Ellipsis = "…";

    private const double LineHeightRatio = 1.2;

    private const double NarrowRatio = 0.3;

    private const double WideLetterRatio = 0.85;

    private const double UpperRatio = 0.66;

    private const double LowerRatio = 0.56;

    private const double OtherRatio = 0.6;

    private const double FullRatio = 1;

    public static double LineHeight(double fontSize) => fontSize * LineHeightRatio;

    public static double Measure(string? text, double fontSize)
    {
        if (String.IsNullOrEmpty(text))
        {
            return 0;
        }

        var total = 0d;
        foreach (var rune in text.EnumerateRunes())
        {
            total += GetRatio(rune);
        }

        return total * fontSize;
    }

    public static string Truncate(string text, double fontSize, double maxWidth)
    {
        if (Measure(text, fontSize) <= maxWidth)
        {
            return text;
        }

        var limit = maxWidth - Measure(Ellipsis, fontSize);
        var used = 0d;
        var builder = new StringBuilder();
        foreach (var rune in text.EnumerateRunes())
        {
            used += GetRatio(rune) * fontSize;
            if (used > limit)
            {
                break;
            }

            builder.Append(rune.ToString());
        }

        return builder.Length > 0 ? builder.ToString().TrimEnd() + Ellipsis : string.Empty;
    }

    private static double GetRatio(Rune rune)
    {
        var value = rune.Value;
        if (value >= 0x80)
        {
            return IsFullWidth(value) ? FullRatio : OtherRatio;
        }

        return (char)value switch
        {
            ' ' or 'f' or 'i' or 'j' or 'l' or 't' or 'I' or '.' or ',' or ':' or ';' or '\'' or '!' or '|' => NarrowRatio,
            'm' or 'w' or 'M' or 'W' or '@' or '%' => WideLetterRatio,
            >= 'A' and <= 'Z' => UpperRatio,
            _ => LowerRatio
        };
    }

    private static bool IsFullWidth(int value) => value
        is (>= 0x1100 and <= 0x115F)
        or (>= 0x2E80 and <= 0xA4CF)
        or (>= 0xAC00 and <= 0xD7A3)
        or (>= 0xF900 and <= 0xFAFF)
        or (>= 0xFE30 and <= 0xFE6F)
        or (>= 0xFF00 and <= 0xFF60)
        or (>= 0xFFE0 and <= 0xFFE6)
        or (>= 0x1F300 and <= 0x1FAFF)
        or (>= 0x20000 and <= 0x3FFFF);
}

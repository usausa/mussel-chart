namespace MusselChart.Internal;

using System.Globalization;

internal static class CompactNumberFormat
{
    private const double CompactThreshold = 10000;

    private const double ZeroThreshold = 0.005;

    private const string ZeroFormat = "0";

    private const string PlainFormat = "#,0.##";

    private const int PlainDigits = 2;

    private const int MaxStepDigits = 15;

    private const int StepSignificantDigits = 3;

    private const double StepNoise = 1e-9;

    private static readonly string[] Suffixes = ["K", "M", "B", "T"];

    private static readonly string[] StepFormats = CreateStepFormats();

    public static string Format(double value, IFormatProvider provider)
    {
        if (!Double.IsFinite(value))
        {
            return value.ToString(provider);
        }

        var (number, format, suffix) = Resolve(value);
        var text = number.ToString(format, provider);
        return suffix is null ? text : String.Concat(text, suffix);
    }

    public static string Format(double value, double step, IFormatProvider provider)
    {
        if (!Double.IsFinite(value) || !Double.IsFinite(step) || !(step > 0))
        {
            return Format(value, provider);
        }

        var (number, format, suffix) = Resolve(value, step);
        var text = number.ToString(format, provider);
        return suffix is null ? text : String.Concat(text, suffix);
    }

    private static (double Number, string Format, string? Suffix) Resolve(double value, double step)
    {
        var magnitude = Math.Abs(value);
        if (magnitude < CompactThreshold)
        {
            var digits = Math.Max(PlainDigits, GetDigits(step));
            var plain = Round(magnitude, digits);
            return ((value < 0) && (plain > 0) ? -plain : plain, StepFormats[digits], null);
        }

        var unit = 0;
        var size = 1000d;
        var scaled = magnitude / 1000;
        while ((scaled >= 1000) && (unit < Suffixes.Length - 1))
        {
            scaled /= 1000;
            size *= 1000;
            unit++;
        }

        var places = Math.Max(GetUnitDigits(scaled), GetDigits(step / size));
        var rounded = Round(scaled, places);
        if ((rounded >= 1000) && (unit < Suffixes.Length - 1))
        {
            unit++;
            size *= 1000;
            var carried = rounded / 1000;
            places = Math.Max(GetUnitDigits(carried), GetDigits(step / size));
            rounded = Round(carried, places);
        }

        return (value < 0 ? -rounded : rounded, StepFormats[places], Suffixes[unit]);
    }

    private static int GetDigits(double step)
    {
        var lead = (int)Math.Clamp(Math.Ceiling(-Math.Log10(step)), -MaxStepDigits, MaxStepDigits);
        var limit = Math.Clamp(lead + StepSignificantDigits, 0, MaxStepDigits);
        var scaled = step;
        for (var digits = 0; digits <= limit; digits++)
        {
            if (Math.Abs(scaled - Math.Round(scaled)) <= scaled * StepNoise)
            {
                return digits;
            }

            scaled *= 10;
        }

        return Math.Clamp(lead, 0, MaxStepDigits);
    }

    private static string[] CreateStepFormats()
    {
        var formats = new string[MaxStepDigits + 1];
        for (var i = 0; i < formats.Length; i++)
        {
            formats[i] = i == 0 ? "#,0" : "#,0." + new string('#', i);
        }

        return formats;
    }

    private static (double Number, string Format, string? Suffix) Resolve(double value)
    {
        var magnitude = Math.Abs(value);
        if (magnitude < ZeroThreshold)
        {
            return (0, ZeroFormat, null);
        }

        if (magnitude < CompactThreshold)
        {
            var plain = Round(magnitude, PlainDigits);
            return (value < 0 ? -plain : plain, PlainFormat, null);
        }

        var unit = 0;
        var scaled = magnitude / 1000;
        while ((scaled >= 1000) && (unit < Suffixes.Length - 1))
        {
            scaled /= 1000;
            unit++;
        }

        var rounded = Round(scaled);
        if ((rounded >= 1000) && (unit < Suffixes.Length - 1))
        {
            unit++;
            rounded = Round(rounded / 1000);
        }

        return (value < 0 ? -rounded : rounded, rounded >= 100 ? "#,0" : "0.##", Suffixes[unit]);
    }

    private static double Round(double value) => Round(value, GetUnitDigits(value));

    private static int GetUnitDigits(double value) => value >= 100 ? 0 : value >= 10 ? 1 : 2;

    private static double Round(double value, int digits)
    {
        Span<char> text = stackalloc char[32];
        return value.TryFormat(text, out var written, "R", CultureInfo.InvariantCulture) && Decimal.TryParse(text[..written], NumberStyles.Float, CultureInfo.InvariantCulture, out var exact)
            ? (double)Math.Round(exact, digits, MidpointRounding.AwayFromZero)
            : Math.Round(value, digits, MidpointRounding.AwayFromZero);
    }
}

namespace MusselChart.Internal;

internal sealed record ChartCenterText(string Value, string? Caption, double ValueFontSize, double CaptionFontSize, double ValueY, double CaptionY)
{
    private const double LegendFontSize = 12;

    private const double CaptionGap = 2;

    private const double CenterPadding = 4;

    private const double MaxFontSize = 32;

    private const double MinFontSize = 6;

    private const double FontRatio = 0.5;

    private const double CaptionFontRatio = 0.45;

    private const int FitAttempts = 3;

    public static ChartCenterText? Fit(string value, string? caption, double edge, double centerY, double top, double bottom)
    {
        var available = edge - CenterPadding;
        var valueSize = RoundSize(Math.Min(MaxFontSize, edge * FontRatio));
        for (var attempt = 0; attempt < FitAttempts; attempt++)
        {
            if ((available <= 0) || (valueSize < MinFontSize))
            {
                return null;
            }

            var captionSize = RoundSize(Math.Min(LegendFontSize, valueSize * CaptionFontRatio));
            var text = !String.IsNullOrEmpty(caption) && (captionSize >= MinFontSize) ? caption : null;
            var valueWidth = ChartText.Measure(value, valueSize);
            var valueHeight = ChartText.LineHeight(valueSize);
            var captionWidth = ChartText.Measure(text, captionSize);
            var captionHeight = text is null ? 0 : ChartText.LineHeight(captionSize);
            var height = valueHeight + (text is null ? 0 : CaptionGap + captionHeight);
            var width = Math.Max(valueWidth, captionWidth);
            var start = Math.Clamp(centerY - (height / 2), top, Math.Max(top, bottom - height));
            var reach = Math.Max(Math.Abs(start - centerY), Math.Abs(start + height - centerY));
            var extent = Math.Sqrt((width * width / 4) + (reach * reach));
            if (extent <= available)
            {
                return new ChartCenterText(value, text, valueSize, captionSize, start + (valueHeight / 2), start + valueHeight + CaptionGap + (captionHeight / 2));
            }

            valueSize = RoundSize(valueSize * available / extent);
        }

        return null;
    }

    private static double RoundSize(double size) => Math.Floor(size * 2) / 2;
}

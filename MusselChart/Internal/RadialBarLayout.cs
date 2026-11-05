namespace MusselChart.Internal;

using System.Globalization;

internal sealed record RadialBarLayoutOptions
{
    public double Minimum { get; init; }

    public double Maximum { get; init; } = 100;

    public double StartAngle { get; init; }

    public double SweepAngle { get; init; } = 360;

    public double InnerRadius { get; init; } = 0.4;

    public double? RingThickness { get; init; }

    public double RingSpacing { get; init; } = 4;

    public ChartCapStyle CapStyle { get; init; }

    public ChartRadialBarMode DisplayMode { get; init; }

    public ChartOverflowMode OverflowMode { get; init; }

    public int SegmentCount { get; init; } = 20;

    public double SegmentGap { get; init; } = 2;

    public bool BarGlow { get; init; }

    public bool ThresholdMarkers { get; init; }

    public ChartCenterValueMode CenterValueMode { get; init; }

    public string? CenterText { get; init; }

    public string? CenterLabel { get; init; }

    public string? ValueFormat { get; init; }

    public Func<double, string>? ValueFormatter { get; init; }

    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;
}

internal sealed record RadialBarRow(int Slot, string? Label, double Ratio, double Percent, string? Color, string? PercentText, string? ValueText);

internal sealed record RadialBarCenter(string Value, string? Caption, double ValueFontSize, double CaptionFontSize, double ValueY, double CaptionY);

internal sealed class RadialBarLayout
{
    private const double Padding = 8;

    private const double LegendFontSize = 12;

    private const double CenterPadding = 4;

    private const double CaptionGap = 2;

    private const double MaxCenterFontSize = 32;

    private const double MinCenterFontSize = 6;

    private const double MaxThicknessRatio = 1d / 6;

    private const double CenterFontRatio = 0.5;

    private const double CaptionFontRatio = 0.45;

    private const double MinExtent = 1e-6;

    private const double MinThickness = 1;

    private const double MinSegmentLength = 3;

    private const double GlowMarginRatio = 0.02;

    private const double GlowSigmaBase = 0.75;

    private const double GlowSigmaRatio = 0.08;

    private const double MarkerWidthRatio = 0.2;

    private const double MarkerWidthPower = 0.92;

    private const double MinMarkerWidth = 4;

    private const double MaxMarkerWidth = 12;

    private const double MinMarkerSpacing = 2;

    private const double MaxMarkerSpacing = 12;

    private const int FitAttempts = 3;

    private const string PercentPattern = "P0";

    private readonly List<RadialBarRow> rows = [];

    public IReadOnlyList<RadialBarRow> Rows => rows;

    public double CenterX { get; private set; }

    public double CenterY { get; private set; }

    public double OuterRadius { get; private set; }

    public double Thickness { get; private set; }

    public double Spacing { get; private set; }

    public double MarkerRadius { get; private set; }

    public double MarkerWidth { get; private set; }

    public RadialBarCenter? Center { get; private set; }

    public double GlowSigma => GlowSigmaBase + (Thickness * GlowSigmaRatio);

    public double GetRingRadius(int slot) => OuterRadius - (Thickness / 2) - (slot * (Thickness + Spacing));

    public void Update(double width, double height, RadialBarLayoutOptions options, IReadOnlyList<ChartEntry> items)
    {
        Spacing = Math.Max(options.RingSpacing, 0);
        var sweep = options.SweepAngle > 0 ? Math.Min(options.SweepAngle, 360) : 360;
        var inner = Math.Clamp(options.InnerRadius, 0, 0.95);
        BuildRows(options, items);
        PlaceRings(new Area(Padding, Padding, Math.Max(0, width - (Padding * 2)), Math.Max(0, height - (Padding * 2))), options with { SweepAngle = sweep, InnerRadius = inner });
        PrepareCenter(options, items);
    }

    public static int GetSegmentCount(double radius, double thickness, double sweepAngle, int segmentCount, double segmentGap)
    {
        var inner = Math.Max(0, radius - (thickness / 2));
        var length = Math.PI * 2 * inner * Math.Min(sweepAngle, 360) / 360;
        var limit = (int)Math.Floor(length / (segmentGap + MinSegmentLength));
        return Math.Max(1, Math.Min(segmentCount, limit));
    }

    private static double RoundSize(double size) => Math.Floor(size * 2) / 2;

    private static double ResolveThickness(double radius, double innerRadius, double? ringThickness, double spacing, int count)
    {
        if (radius <= 0)
        {
            return 0;
        }

        if (ringThickness is { } thickness)
        {
            return thickness;
        }

        var limit = radius * MaxThicknessRatio;
        if (count == 0)
        {
            return limit;
        }

        var share = ((radius * (1 - innerRadius)) - (spacing * (count - 1))) / count;
        return Math.Max(MinThickness, Math.Min(limit, share));
    }

    private void BuildRows(RadialBarLayoutOptions options, IReadOnlyList<ChartEntry> items)
    {
        rows.Clear();
        var wrap = (options.OverflowMode == ChartOverflowMode.Wrap) && (options.DisplayMode != ChartRadialBarMode.Segmented);
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var maximum = item.Maximum ?? options.Maximum;
            var range = maximum - options.Minimum;
            var valid = Double.IsFinite(item.Value) && (range > 0);
            var raw = valid ? (item.Value - options.Minimum) / range : Double.NaN;
            var ratio = !valid ? 0 : wrap ? Math.Max(0, raw) : Math.Clamp(raw, 0, 1);
            var percent = valid ? raw.ToString(PercentPattern, options.Culture) : null;
            var value = valid ? ChartFormat.Value(item.Value, options.ValueFormat, options.ValueFormatter, options.Culture) : null;
            rows.Add(new RadialBarRow(i, item.Label, ratio, valid ? raw * 100 : Double.NaN, item.Color, percent, value));
        }
    }

    private void PlaceRings(Area area, RadialBarLayoutOptions options)
    {
        if (options.BarGlow)
        {
            var margin = Math.Min(area.Width, area.Height) * GlowMarginRatio;
            area = new Area(area.X + margin, area.Y + margin, Math.Max(0, area.Width - (margin * 2)), Math.Max(0, area.Height - (margin * 2)));
        }

        var count = rows.Count;
        var (left, top, right, bottom) = ChartGeometry.GetUnitBounds(options.StartAngle, options.SweepAngle, options.InnerRadius);
        var radius = Math.Max(0, Math.Min(area.Width / Math.Max(right - left, MinExtent), area.Height / Math.Max(bottom - top, MinExtent)));
        var thickness = ResolveThickness(radius, options.InnerRadius, options.RingThickness, Spacing, count);
        if ((options.CapStyle == ChartCapStyle.Round) && (options.SweepAngle < 360))
        {
            radius = Math.Max(0, radius - (thickness / 2));
            thickness = ResolveThickness(radius, options.InnerRadius, options.RingThickness, Spacing, count);
        }

        CenterX = area.X + (area.Width / 2) - (radius * (left + right) / 2);
        CenterY = area.Y + (area.Height / 2) - (radius * (top + bottom) / 2);
        OuterRadius = radius;
        Thickness = thickness;
        MarkerRadius = 0;
        MarkerWidth = 0;
        if (!options.ThresholdMarkers || (count == 0) || !(thickness > 0))
        {
            return;
        }

        var width = Math.Clamp(MarkerWidthRatio * Math.Pow(thickness, MarkerWidthPower), MinMarkerWidth, MaxMarkerWidth);
        var gap = Math.Clamp(width / 2, MinMarkerSpacing, MaxMarkerSpacing);
        var ring = radius - width - gap;
        if (ring > 0)
        {
            OuterRadius = ring;
            Thickness = ResolveThickness(ring, options.InnerRadius, options.RingThickness, Spacing, count);
            MarkerRadius = radius - (width / 2);
            MarkerWidth = width;
        }
    }

    private void PrepareCenter(RadialBarLayoutOptions options, IReadOnlyList<ChartEntry> items)
    {
        Center = null;
        var edge = rows.Count > 0 ? GetRingRadius(rows.Count - 1) - (Thickness / 2) : OuterRadius;
        var available = edge - CenterPadding;
        if ((rows.Count == 0) || (available <= 0))
        {
            return;
        }

        if (options.CenterText is { } text)
        {
            Center = FitCenter(text, options.CenterLabel, available);
        }
        else if (options.CenterValueMode == ChartCenterValueMode.Auto)
        {
            var first = rows[0];
            if (first.PercentText is { } percent)
            {
                Center = FitCenter(percent, options.CenterLabel ?? first.Label, available);
            }
        }
        else if (options.CenterValueMode == ChartCenterValueMode.Total)
        {
            var total = items.Where(static x => Double.IsFinite(x.Value)).Sum(static x => x.Value);
            Center = FitCenter(ChartFormat.Value(total, options.ValueFormat, options.ValueFormatter, options.Culture), options.CenterLabel, available);
        }
    }

    private RadialBarCenter? FitCenter(string value, string? caption, double available)
    {
        var valueSize = RoundSize(Math.Min(MaxCenterFontSize, (available + CenterPadding) * CenterFontRatio));
        for (var attempt = 0; attempt < FitAttempts; attempt++)
        {
            if (valueSize < MinCenterFontSize)
            {
                return null;
            }

            var captionSize = RoundSize(Math.Min(LegendFontSize, valueSize * CaptionFontRatio));
            var text = !String.IsNullOrEmpty(caption) && (captionSize >= MinCenterFontSize) ? caption : null;
            var valueWidth = ChartText.Measure(value, valueSize);
            var valueHeight = ChartText.LineHeight(valueSize);
            var captionWidth = ChartText.Measure(text, captionSize);
            var captionHeight = text is null ? 0 : ChartText.LineHeight(captionSize);
            var height = valueHeight + (text is null ? 0 : CaptionGap + captionHeight);
            var width = Math.Max(valueWidth, captionWidth);
            var top = CenterY - (height / 2);
            var extent = Math.Sqrt((width * width / 4) + (height * height / 4));
            if (extent <= available)
            {
                return new RadialBarCenter(value, text, valueSize, captionSize, top + (valueHeight / 2), top + valueHeight + CaptionGap + (captionHeight / 2));
            }

            valueSize = RoundSize(valueSize * available / extent);
        }

        return null;
    }

    private readonly record struct Area(double X, double Y, double Width, double Height);
}

namespace MusselChart;

using System.Globalization;
using System.Text;

using Microsoft.AspNetCore.Components;

using MusselChart.Internal;

public sealed partial class MusselRadialBarView
{
    private const string TrackClass = "mussel-radial-track";

    private const string UnlitClass = "mussel-radial-unlit";

    private const string HitClass = "mussel-radial-hit";

    private const double SweepEdge = 0.25;

    private const double WrapLightness = -15;

    private const double Tolerance = 1e-6;

    private readonly string glowId = "mussel-" + Guid.NewGuid().ToString("N")[..12];

    private readonly RadialBarLayout layout = new();

    private readonly List<ChartLegendItem> legendItems = [];

    private readonly List<RingView> ringViews = [];

    private readonly List<ArcView> glowArcs = [];

    private readonly List<ArcView> markerArcs = [];

    private readonly List<SweepView> glowSweeps = [];

    private SweepView? markerSweep;

    private string ringClass = string.Empty;

    private string centerX = string.Empty;

    private string emptyText = string.Empty;

    private string thicknessText = string.Empty;

    private string markerWidthText = string.Empty;

    private string capRadius = string.Empty;

    private string glowSigma = string.Empty;

    [Parameter]
    public IReadOnlyList<ChartEntry>? Items { get; set; }

    [Parameter]
    public IReadOnlyList<string>? Palette { get; set; }

    [Parameter]
    public double Minimum { get; set; }

    [Parameter]
    public double Maximum { get; set; } = 100;

    [Parameter]
    public double StartAngle { get; set; }

    [Parameter]
    public double SweepAngle { get; set; } = 360;

    [Parameter]
    public double InnerRadius { get; set; } = 0.4;

    [Parameter]
    public double? RingThickness { get; set; }

    [Parameter]
    public double RingSpacing { get; set; } = 4;

    [Parameter]
    public ChartCapStyle CapStyle { get; set; }

    [Parameter]
    public ChartRadialBarMode DisplayMode { get; set; }

    [Parameter]
    public ChartFillMode FillMode { get; set; }

    [Parameter]
    public int SegmentCount { get; set; } = 20;

    [Parameter]
    public double SegmentGap { get; set; } = 2;

    [Parameter]
    public bool TrackVisible { get; set; } = true;

    [Parameter]
    public bool BarGlow { get; set; }

    [Parameter]
    public bool ThresholdMarkersVisible { get; set; }

    [Parameter]
    public ChartOverflowMode OverflowMode { get; set; }

    [Parameter]
    public IReadOnlyList<ChartThreshold>? Thresholds { get; set; }

    [Parameter]
    public ChartCenterValueMode CenterValueMode { get; set; }

    [Parameter]
    public string? CenterText { get; set; }

    [Parameter]
    public string? CenterLabel { get; set; }

    [Parameter]
    public string? ValueFormat { get; set; }

    [Parameter]
    public Func<double, string>? ValueFormatter { get; set; }

    [Parameter]
    public bool? LegendVisible { get; set; }

    [Parameter]
    public ChartLegendPosition LegendPosition { get; set; } = ChartLegendPosition.Right;

    [Parameter]
    public ChartLegendValueMode LegendValueMode { get; set; }

    private bool HasThresholds => Thresholds is { Count: > 0 };

    private double Sweep => SweepAngle > 0 ? Math.Min(SweepAngle, 360) : 360;

    private string? CapText => CapStyle == ChartCapStyle.Round ? "round" : null;

    private void Prepare()
    {
        var culture = Culture ?? CultureInfo.CurrentCulture;
        var items = Items ?? [];
        var markers = ThresholdMarkersVisible && HasThresholds;
        layout.Update(Width, Height, new RadialBarLayoutOptions
        {
            Minimum = Minimum,
            Maximum = Maximum,
            StartAngle = StartAngle,
            SweepAngle = SweepAngle,
            InnerRadius = InnerRadius,
            RingThickness = RingThickness,
            RingSpacing = RingSpacing,
            CapStyle = CapStyle,
            DisplayMode = DisplayMode,
            OverflowMode = OverflowMode,
            SegmentCount = SegmentCount,
            SegmentGap = SegmentGap,
            BarGlow = BarGlow,
            ThresholdMarkers = markers,
            CenterValueMode = CenterValueMode,
            CenterText = CenterText,
            CenterLabel = CenterLabel,
            ValueFormat = ValueFormat,
            ValueFormatter = ValueFormatter,
            Culture = culture
        }, items);

        ringClass = TrackballMode == ChartTrackballMode.Hover ? "mussel-rings mussel-rings-hover" : "mussel-rings";
        centerX = ChartGeometry.Text(layout.CenterX);
        thicknessText = ChartGeometry.Text(layout.Thickness);
        markerWidthText = ChartGeometry.Text(layout.MarkerWidth);
        capRadius = ChartGeometry.Text(layout.Thickness / 2);
        glowSigma = ChartGeometry.Text(layout.GlowSigma);
        emptyText = layout.Rows.Count > 0 ? string.Empty : EmptyText ?? string.Empty;

        ringViews.Clear();
        glowArcs.Clear();
        glowSweeps.Clear();
        markerArcs.Clear();
        markerSweep = null;
        legendItems.Clear();
        var palette = Palette ?? ChartPalettes.DefaultLight;
        foreach (var row in layout.Rows)
        {
            var baseColor = row.Color ?? (row.Slot < palette.Count ? palette[row.Slot] : ChartColor.Other);
            var color = GetThresholdColor(row.Percent, baseColor);
            ringViews.Add(CreateRing(row, baseColor, color));
            if ((LegendVisible ?? true) && !String.IsNullOrEmpty(row.Label))
            {
                var value = LegendValueMode switch
                {
                    ChartLegendValueMode.Value => row.ValueText,
                    ChartLegendValueMode.Percent => row.PercentText,
                    _ => null
                };
                legendItems.Add(new ChartLegendItem(row.Slot.ToString(CultureInfo.InvariantCulture), row.Label, color, value));
            }
        }

        if (markers && (layout.Rows.Count > 0) && (layout.MarkerWidth > 0))
        {
            var first = layout.Rows[0];
            AddMarkerArcs(first.Color ?? (palette.Count > 0 ? palette[0] : ChartColor.Other));
        }
    }

    private string GetThresholdColor(double percent, string baseColor) =>
        Double.IsNaN(percent) || !HasThresholds ? baseColor : ChartColor.Threshold(ChartFormat.FindThreshold(Thresholds, percent), baseColor);

    private double GetAngle(double fraction) => StartAngle + (Sweep * fraction);

    private string CreateArc(double radius, double from, double to) =>
        ChartGeometry.Arc(layout.CenterX, layout.CenterY, radius, GetAngle(from), Sweep * (to - from));

    private RingView CreateRing(RadialBarRow row, string baseColor, string color)
    {
        var radius = layout.GetRingRadius(row.Slot);
        var arcs = new List<ArcView>();
        var caps = new List<CapView>();
        SweepView? sweep = null;
        if ((radius > 0) && (layout.Thickness > 0))
        {
            var ratio = Double.IsNaN(row.Percent) ? 0 : row.Ratio;
            if (DisplayMode == ChartRadialBarMode.Segmented)
            {
                AddSegments(arcs, radius, ratio, baseColor);
            }
            else
            {
                if (TrackVisible && (ratio < 1))
                {
                    arcs.Add(new ArcView(CreateArc(radius, ratio, 1), null, TrackClass, CapText));
                }

                if (ratio > Tolerance)
                {
                    if (FillMode == ChartFillMode.Gradient)
                    {
                        sweep = AddGradient(arcs, caps, radius, ratio, baseColor);
                    }
                    else
                    {
                        arcs.Add(new ArcView(CreateArc(radius, 0, Math.Min(ratio, 1)), color, null, CapText));
                        var laps = Math.Max(0, Math.Ceiling(ratio) - 1);
                        if (laps > 0)
                        {
                            arcs.Add(new ArcView(CreateArc(radius, 0, ratio - laps), ChartColor.Shift(color, 0, WrapLightness), null, CapText));
                        }
                    }
                }
            }

            if (BarGlow)
            {
                foreach (var arc in arcs)
                {
                    if (arc.Class is null)
                    {
                        glowArcs.Add(arc);
                    }
                }

                if (sweep is { } value)
                {
                    glowSweeps.Add(value);
                }
            }
        }

        var tip = ChartTip.Create(null, [new ChartTipRow(color, row.Label, row.ValueText is null ? null : $"{row.ValueText} ({row.PercentText})")]);
        return new RingView(row.Slot, tip, arcs, caps, sweep);
    }

    // Each cell takes the threshold color at its start and is lit when its start is before the value
    private void AddSegments(List<ArcView> arcs, double radius, double ratio, string baseColor)
    {
        var gapAngle = SegmentGap / radius * 180 / Math.PI;
        var count = RadialBarLayout.GetSegmentCount(radius, layout.Thickness, Sweep, SegmentCount, SegmentGap);
        var step = Sweep / count;
        for (var c = 0; c < count; c++)
        {
            var fraction = (double)c / count;
            var lit = fraction < ratio - Tolerance;
            if (!lit && !TrackVisible)
            {
                continue;
            }

            var path = ChartGeometry.Arc(layout.CenterX, layout.CenterY, radius, StartAngle + (c * step) + (gapAngle / 2), Math.Max(0, step - gapAngle));
            arcs.Add(lit ? new ArcView(path, GetThresholdColor(fraction * 100, baseColor), null, null) : new ArcView(path, null, UnlitClass, null));
        }
    }

    private SweepView AddGradient(List<ArcView> arcs, List<CapView> caps, double radius, double ratio, string baseColor)
    {
        var (colors, positions) = HasThresholds
            ? RadialBarGradient.GetThresholdStops(GetSteps(baseColor), baseColor)
            : RadialBarGradient.GetColorStops(baseColor, ratio);
        var drawn = Math.Min(ratio, 1);

        // The sweep takes no pointer events, so this keeps the value part of the ring under the pointer
        arcs.Add(new ArcView(CreateArc(radius, 0, drawn), null, HitClass, null));
        if (CapStyle == ChartCapStyle.Round)
        {
            AddCap(caps, radius, 0, colors[0]);
            AddCap(caps, radius, drawn, RadialBarGradient.GetColorAt(colors, positions, drawn));
        }

        var laps = Math.Max(0, Math.Ceiling(ratio) - 1);
        if (laps > 0)
        {
            arcs.Add(new ArcView(CreateArc(radius, 0, ratio - laps), ChartColor.Shift(colors[^1], 0, WrapLightness), null, CapText));
        }

        return CreateSweep(radius, layout.Thickness, drawn, colors, positions);
    }

    // SVG has no sweep gradient, so the ring is a conic gradient of CSS cut out to the band of the ring
    private SweepView CreateSweep(double radius, double thickness, double drawn, string[] colors, double[] positions)
    {
        var outer = radius + (thickness / 2);
        var inner = Math.Max(0, radius - (thickness / 2));
        var end = Sweep * drawn;
        var open = end < 360 - SweepEdge;
        var builder = ChartStringBuilder.Rent();
        builder.Append("background:conic-gradient(from ").AppendNumber(ChartGeometry.Normalize(StartAngle)).Append("deg");
        if (open)
        {
            builder.Append(",transparent 0deg");
        }

        for (var i = 0; i < positions.Length; i++)
        {
            var angle = Sweep * positions[i];
            if (angle < end)
            {
                builder.Append(',').Append(colors[i]).Append(' ').AppendNumber(Math.Max(angle, open ? SweepEdge : 0)).Append("deg");
            }
        }

        builder.Append(',').Append(RadialBarGradient.GetColorAt(colors, positions, drawn)).Append(' ').AppendNumber(end).Append("deg");
        if (open)
        {
            builder.Append(",transparent ").AppendNumber(end + SweepEdge).Append("deg");
        }

        builder.Append(");-webkit-mask:");
        AppendSweepMask(builder, inner, outer);
        builder.Append(";mask:");
        AppendSweepMask(builder, inner, outer);
        return new SweepView(ChartGeometry.Text(layout.CenterX - outer), ChartGeometry.Text(layout.CenterY - outer), ChartGeometry.Text(outer * 2), ChartStringBuilder.ToStringAndReturn(builder));
    }

    private static void AppendSweepMask(StringBuilder builder, double inner, double outer)
    {
        builder.Append("radial-gradient(circle closest-side,transparent ").AppendNumber(Math.Max(inner - 0.5, 0));
        builder.Append("px,#000 ").AppendNumber(inner).Append("px,#000 ").AppendNumber(outer - 0.5);
        builder.Append("px,transparent ").AppendNumber(outer).Append("px)");
    }

    private void AddCap(List<CapView> caps, double radius, double fraction, string color)
    {
        var (x, y) = ChartGeometry.GetPoint(layout.CenterX, layout.CenterY, radius, GetAngle(fraction));
        caps.Add(new CapView(ChartGeometry.Text(x), ChartGeometry.Text(y), color));
    }

    private List<(double Value, string Color)> GetSteps(string baseColor)
    {
        var steps = new List<(double Value, string Color)>();
        foreach (var threshold in Thresholds ?? [])
        {
            steps.Add((threshold.Value, ChartColor.Threshold(threshold, baseColor)));
        }

        steps.Sort(static (x, y) => x.Value.CompareTo(y.Value));
        return steps;
    }

    // One thin ring outside the outermost ring: each threshold range takes its color
    private void AddMarkerArcs(string baseColor)
    {
        var radius = layout.MarkerRadius;
        if (FillMode == ChartFillMode.Gradient)
        {
            var (colors, positions) = RadialBarGradient.GetThresholdStops(GetSteps(baseColor), baseColor);
            markerSweep = CreateSweep(radius, layout.MarkerWidth, 1, colors, positions);
            return;
        }

        var color = baseColor;
        var from = 0d;
        foreach (var (value, stepColor) in GetSteps(baseColor))
        {
            if (Double.IsNaN(value))
            {
                continue;
            }

            if (Double.IsNegativeInfinity(value))
            {
                color = stepColor;
                continue;
            }

            var to = Math.Clamp(value / 100, from, 1);
            if (to > from)
            {
                markerArcs.Add(new ArcView(CreateArc(radius, from, to), color, null, null));
            }

            color = stepColor;
            from = to;
        }

        if (from < 1)
        {
            markerArcs.Add(new ArcView(CreateArc(radius, from, 1), color, null, null));
        }
    }

    private readonly record struct ArcView(string Path, string? Color, string? Class, string? Cap);

    private readonly record struct CapView(string X, string Y, string Color);

    private readonly record struct SweepView(string X, string Y, string Size, string Style);

    private sealed record RingView(int Key, string Tip, List<ArcView> Arcs, List<CapView> Caps, SweepView? Sweep);
}

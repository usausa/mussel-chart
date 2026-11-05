namespace MusselChart.Internal;

using System.Globalization;
using System.Text;

internal readonly record struct RadarPoint(string? Category, double Value);

internal readonly record struct RadarAxis(string? Text, double DirectionX, double DirectionY, double LabelX, double LabelY, string Anchor, string Header);

internal readonly record struct RadarLevel(double Value, string Text, double Width);

internal sealed record RadarLayoutOptions
{
    public double? Minimum { get; init; }

    public double? Maximum { get; init; }

    public ChartRadarGridShape GridShape { get; init; }

    public int LevelCount { get; init; } = 5;

    public string? ValueFormat { get; init; }

    public Func<double, string>? ValueFormatter { get; init; }

    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;
}

internal sealed class RadarLayout
{
    public const double LabelFontSize = 11;

    private const double Padding = 8;

    private const double LabelGap = 8;

    private const double LevelLabelPadding = 2;

    private const double LabelWidthRatio = 0.25;

    private const double MinLabeledRadiusRatio = 0.4;

    private const double AlignThreshold = 0.05;

    private const double Tolerance = 1e-9;

    private readonly Dictionary<string, int> indexes = new(StringComparer.Ordinal);

    private readonly List<string?> names = [];

    private readonly List<RadarAxis> axes = [];

    private readonly List<double> labelWidths = [];

    private readonly List<RadarLevel> levels = [];

    private readonly List<double> ticks = [];

    private readonly List<RadarLevel> levelCandidates = [];

    private readonly List<LabelExtent> levelExtents = [];

    private readonly List<int> selectedLevels = [];

    private double[] values = [];

    private bool[] filled = [];

    private double margin;

    private static double LabelHeight => ChartText.LineHeight(LabelFontSize);

    public IReadOnlyList<RadarAxis> Axes => axes;

    public IReadOnlyList<RadarLevel> Levels => levels;

    public IReadOnlyList<double> Ticks => ticks;

    public AxisScale Scale { get; private set; } = new(0, 1, 1);

    public int SeriesCount { get; private set; }

    public int AxisCount { get; private set; }

    public double CenterX { get; private set; }

    public double CenterY { get; private set; }

    public double Radius { get; private set; }

    public bool ShowsLabels { get; private set; }

    public bool IsCircle { get; private set; }

    public double GetValue(int series, int axis) => values[(series * AxisCount) + axis];

    public double GetRatio(double value) => Scale.Span > 0 ? Math.Clamp((value - Scale.Minimum) / Scale.Span, 0, 1) : 0;

    public (double X, double Y) GetPoint(int axis, double ratio) =>
        (CenterX + (axes[axis].DirectionX * Radius * ratio), CenterY + (axes[axis].DirectionY * Radius * ratio));

    public void Update(double width, double height, RadarLayoutOptions options, List<List<RadarPoint>> series, List<bool> visible, double ringMargin)
    {
        margin = ringMargin;
        SeriesCount = series.Count;
        MergeAxes(series);
        IsCircle = (options.GridShape == ChartRadarGridShape.Circle) || (AxisCount < 3);
        ResolveScale(options, visible);
        PlaceRadar(new Area(Padding, Padding, Math.Max(0, width - (Padding * 2)), Math.Max(0, height - (Padding * 2))), options);
        PlaceLevels(options);
    }

    // The rings of every level and a line from the center to each axis
    public string CreateGridPath()
    {
        if (Radius <= 0)
        {
            return string.Empty;
        }

        var builder = ChartStringBuilder.Rent();
        foreach (var tick in ticks)
        {
            var ratio = Scale.Span > 0 ? (tick - Scale.Minimum) / Scale.Span : 0;
            if ((ratio > Tolerance) && (ratio < 1 - Tolerance))
            {
                AppendRing(builder, ratio);
            }
        }

        AppendRing(builder, 1);
        for (var k = 0; k < AxisCount; k++)
        {
            var (x, y) = GetPoint(k, 1);
            builder.Append('M').AppendNumber(CenterX).Append(' ').AppendNumber(CenterY);
            builder.Append('L').AppendNumber(x).Append(' ').AppendNumber(y);
        }

        return ChartStringBuilder.ToStringAndReturn(builder);
    }

    public string CreatePolygon(int series)
    {
        var builder = ChartStringBuilder.Rent();
        for (var k = 0; k < AxisCount; k++)
        {
            var value = GetValue(series, k);
            if (!Double.IsFinite(value))
            {
                continue;
            }

            var (x, y) = GetPoint(k, GetRatio(value));
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.AppendNumber(x).Append(',').AppendNumber(y);
        }

        return ChartStringBuilder.ToStringAndReturn(builder);
    }

    // The wedge around an axis that reacts to the pointer
    public string CreateAxisArea(int axis)
    {
        var step = 360d / AxisCount;
        return ChartGeometry.Sector(CenterX, CenterY, Radius + LabelGap, (axis * step) - (step / 2), AxisCount > 1 ? step : 359.99);
    }

    private static double GetDirection(in RadarAxis axis, bool vertical) => vertical ? axis.DirectionY : axis.DirectionX;

    private static (double Left, double Right, string Anchor) GetLabelOffsets(double direction, double width)
    {
        if (Math.Abs(direction) < AlignThreshold)
        {
            return (-width / 2, width / 2, "middle");
        }

        return direction > 0 ? (0, width, "start") : (-width, 0, "end");
    }

    private void AppendRing(StringBuilder builder, double ratio)
    {
        if (IsCircle)
        {
            var radius = Radius * ratio;
            builder.Append('M').AppendNumber(CenterX - radius).Append(' ').AppendNumber(CenterY);
            builder.Append('a').AppendNumber(radius).Append(' ').AppendNumber(radius).Append(" 0 1 0 ").AppendNumber(radius * 2).Append(" 0");
            builder.Append('a').AppendNumber(radius).Append(' ').AppendNumber(radius).Append(" 0 1 0 ").AppendNumber(-radius * 2).Append(" 0");
            return;
        }

        for (var k = 0; k < AxisCount; k++)
        {
            var (x, y) = GetPoint(k, ratio);
            builder.Append(k == 0 ? 'M' : 'L').AppendNumber(x).Append(' ').AppendNumber(y);
        }

        builder.Append('Z');
    }

    // Each item name becomes one axis in the order it first appears; items without a name take axes by position
    private void MergeAxes(List<List<RadarPoint>> series)
    {
        indexes.Clear();
        names.Clear();
        foreach (var points in series)
        {
            foreach (var point in points)
            {
                if (point.Category is not null)
                {
                    GetIndex(point.Category);
                }
            }
        }

        foreach (var points in series)
        {
            var unnamed = 0;
            foreach (var point in points)
            {
                unnamed += point.Category is null ? 1 : 0;
            }

            while (names.Count < unnamed)
            {
                names.Add(null);
            }
        }

        AxisCount = names.Count;
        var size = series.Count * AxisCount;
        if (values.Length < size)
        {
            values = new double[size];
            filled = new bool[size];
        }

        Array.Fill(values, Double.NaN, 0, size);
        Array.Clear(filled, 0, size);
        for (var s = 0; s < series.Count; s++)
        {
            var points = series[s];
            var position = 0;
            foreach (var point in points)
            {
                var axis = point.Category is { } name ? indexes[name] : position++;
                var slot = (s * AxisCount) + axis;
                if (!filled[slot])
                {
                    filled[slot] = true;
                    values[slot] = point.Value;
                }
            }
        }
    }

    private void GetIndex(string category)
    {
        if (!indexes.ContainsKey(category))
        {
            indexes.Add(category, names.Count);
            names.Add(category);
        }
    }

    private void ResolveScale(RadarLayoutOptions options, List<bool> visible)
    {
        var minimum = Double.PositiveInfinity;
        var maximum = Double.NegativeInfinity;
        for (var s = 0; s < SeriesCount; s++)
        {
            if (!visible[s])
            {
                continue;
            }

            for (var k = 0; k < AxisCount; k++)
            {
                var value = GetValue(s, k);
                if (Double.IsFinite(value))
                {
                    minimum = Math.Min(minimum, value);
                    maximum = Math.Max(maximum, value);
                }
            }
        }

        var range = new ChartRangeOptions(options.Minimum, options.Maximum, null, null, true, true);
        Scale = ChartRange.Resolve(minimum, maximum, range, Math.Max(options.LevelCount, 1));
        TickGenerator.Numeric(Scale, ticks);
    }

    private void PlaceRadar(in Area area, RadarLayoutOptions options)
    {
        axes.Clear();
        labelWidths.Clear();
        var maxWidth = area.Width * LabelWidthRatio;
        var named = false;
        for (var k = 0; k < AxisCount; k++)
        {
            var text = names[k] is { } name ? ChartText.Truncate(name, LabelFontSize, maxWidth) : null;
            var (x, y) = ChartGeometry.GetDirection(360d * k / AxisCount);
            axes.Add(new RadarAxis(text, x, y, 0, 0, "middle", names[k] ?? (k + 1).ToString(options.Culture)));
            labelWidths.Add(ChartText.Measure(text, LabelFontSize));
            named |= text is not null;
        }

        var (radius, centerX, centerY) = Fit(area, false);
        ShowsLabels = false;
        if (named)
        {
            var labeled = Fit(area, true);
            if ((labeled.Radius > 0) && (labeled.Radius >= radius * MinLabeledRadiusRatio))
            {
                ShowsLabels = true;
                (radius, centerX, centerY) = labeled;
            }
        }

        Radius = radius;
        CenterX = centerX;
        CenterY = centerY;
        if (!ShowsLabels)
        {
            return;
        }

        for (var k = 0; k < AxisCount; k++)
        {
            var axis = axes[k];
            if (axis.Text is null)
            {
                continue;
            }

            var (_, _, anchor) = GetLabelOffsets(axis.DirectionX, labelWidths[k]);
            var x = CenterX + ((Radius + LabelGap) * axis.DirectionX);
            var y = CenterY + ((Radius + LabelGap) * axis.DirectionY) + (axis.DirectionY * LabelHeight / 2);
            axes[k] = axis with { LabelX = x, LabelY = y, Anchor = anchor };
        }
    }

    private (double Radius, double CenterX, double CenterY) Fit(in Area area, bool labeled)
    {
        var count = (IsCircle ? 2 : AxisCount) + (labeled ? AxisCount : 0);
        var radius = Math.Min(Solve(false, count, area.X, area.X + area.Width), Solve(true, count, area.Y, area.Y + area.Height));
        return (radius, GetCenter(false, count, area.X, area.X + area.Width, radius), GetCenter(true, count, area.Y, area.Y + area.Height, radius));
    }

    // The largest radius at which every ring vertex and every name stays inside the area
    private double Solve(bool vertical, int count, double low, double high)
    {
        var upper = Double.PositiveInfinity;
        var lower = 0d;
        for (var i = 0; i < count; i++)
        {
            var (from, start, _) = GetExtent(i, vertical);
            for (var j = 0; j < count; j++)
            {
                var (to, _, end) = GetExtent(j, vertical);
                var room = high - low + start - end;
                var slope = to - from;
                if (slope > Tolerance)
                {
                    upper = Math.Min(upper, room / slope);
                }
                else if (slope < -Tolerance)
                {
                    lower = Math.Max(lower, room / slope);
                }
                else if (room < 0)
                {
                    return 0;
                }
            }
        }

        return Double.IsFinite(upper) && (lower <= upper) ? Math.Max(0, upper) : 0;
    }

    private double GetCenter(bool vertical, int count, double low, double high, double radius)
    {
        var from = Double.NegativeInfinity;
        var to = Double.PositiveInfinity;
        for (var i = 0; i < count; i++)
        {
            var (direction, start, end) = GetExtent(i, vertical);
            from = Math.Max(from, low - (radius * direction) - start);
            to = Math.Min(to, high - (radius * direction) - end);
        }

        return from <= to ? (from + to) / 2 : (low + high) / 2;
    }

    private (double Direction, double Start, double End) GetExtent(int index, bool vertical)
    {
        var rings = IsCircle ? 2 : AxisCount;
        if (index < rings)
        {
            return (IsCircle ? (index == 0 ? -1 : 1) : GetDirection(axes[index], vertical), -margin, margin);
        }

        var k = index - rings;
        var axis = axes[k];
        var direction = GetDirection(axis, vertical);
        if (axis.Text is null)
        {
            return (direction, -margin, margin);
        }

        if (vertical)
        {
            var top = (LabelGap * direction) + ((direction - 1) * LabelHeight / 2);
            return (direction, top, top + LabelHeight);
        }

        var (left, right, _) = GetLabelOffsets(direction, labelWidths[k]);
        return (direction, (LabelGap * direction) + left, (LabelGap * direction) + right);
    }

    private void PlaceLevels(RadarLayoutOptions options)
    {
        levels.Clear();
        if (!ShowsLabels || (Radius <= 0))
        {
            return;
        }

        var half = (LabelHeight / 2) + LevelLabelPadding;
        levelCandidates.Clear();
        levelExtents.Clear();
        foreach (var value in ticks)
        {
            if ((value <= Scale.Minimum) || (value > Scale.Maximum))
            {
                continue;
            }

            var text = ChartFormat.Value(value, options.ValueFormat, options.ValueFormatter, options.Culture);
            var radius = Radius * GetRatio(value);
            levelCandidates.Add(new RadarLevel(value, text, ChartText.Measure(text, LabelFontSize)));
            levelExtents.Add(new LabelExtent(radius - half, radius + half));
        }

        LabelThinning.Select(levelExtents, 0, Int32.MaxValue, selectedLevels);
        foreach (var index in selectedLevels)
        {
            levels.Add(levelCandidates[index]);
        }
    }

    private readonly record struct Area(double X, double Y, double Width, double Height);
}

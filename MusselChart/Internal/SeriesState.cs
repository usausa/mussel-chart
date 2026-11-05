namespace MusselChart.Internal;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

internal readonly record struct SeriesPoint(long Sequence, long Ticks, double X, double Value, double Top, double Bottom);

internal readonly record struct SeriesBar(long Key, string XText, string ValueText, string? BottomText, string? HeightText, double Bottom, double Top, double Value, long StackSlot);

internal readonly record struct SeriesMarker(long Key, string XText, string YText, string? SizeText, double X, double Y, double Size);

internal readonly record struct StackTop(double Positive, double Negative);

internal sealed class SeriesChunk
{
    private readonly StringBuilder line = new();

    private readonly List<(string X, double Bottom)> bottoms = [];

    private StringBuilder? values;

    public SeriesChunk(long key)
    {
        Key = key;
    }

    public long Key { get; }

    public int Count { get; private set; }

    public long LastSequence { get; private set; }

    public string LastX { get; private set; } = string.Empty;

    public double LastTop { get; private set; }

    public double LastBottom { get; private set; }

    public double LastValue { get; private set; }

    [AllowNull]
    public string LinePoints
    {
        get => field ??= line.ToString();
        private set;
    }

    [AllowNull]
    public string AreaPoints
    {
        get => field ??= CreateAreaPoints();
        private set;
    }

    public string? Values
    {
        get => field ??= values?.ToString();
        private set;
    }

    public void Add(long sequence, string x, double top, double bottom, double value, bool area, bool stacked)
    {
        if (Count > 0)
        {
            line.Append(' ');
        }

        line.Append(x).Append(',').Append(CultureInfo.InvariantCulture, $"{top:G6}");
        LinePoints = null;
        Count++;
        LastSequence = sequence;
        LastX = x;
        LastTop = top;
        LastBottom = bottom;
        LastValue = value;

        if (stacked)
        {
            values ??= new StringBuilder();
            if (values.Length > 0)
            {
                values.Append(' ');
            }

            values.Append(CultureInfo.InvariantCulture, $"{value:G6}");
            Values = null;
        }

        if (area)
        {
            bottoms.Add((x, bottom));
            AreaPoints = null;
        }
    }

    private string CreateAreaPoints()
    {
        var builder = ChartStringBuilder.Rent();
        builder.Append(line);
        for (var i = bottoms.Count - 1; i >= 0; i--)
        {
            builder.Append(' ').Append(bottoms[i].X).Append(',').Append(CultureInfo.InvariantCulture, $"{bottoms[i].Bottom:G6}");
        }

        return ChartStringBuilder.ToStringAndReturn(builder);
    }
}

internal sealed class SeriesState
{
    private SeriesOptions? options;

    public SeriesState(ChartSeries series)
    {
        Series = series;
    }

    public ChartSeries Series { get; }

    public object? Source { get; set; }

    public long Sequence { get; set; } = -1;

    public int Generation { get; set; }

    public bool Visible { get; set; }

    public bool Gap { get; set; } = true;

    public string Color { get; set; } = string.Empty;

    public string ThicknessText { get; private set; } = string.Empty;

    public string OpacityText { get; private set; } = string.Empty;

    public int BarSlot { get; set; }

    public double BarOffset { get; private set; } = Double.NaN;

    public double BarWidth { get; private set; } = Double.NaN;

    public string BarOffsetText { get; private set; } = string.Empty;

    public string BarWidthText { get; private set; } = string.Empty;

    public string BarTransform { get; private set; } = string.Empty;

    public List<SeriesPoint> Points { get; } = [];

    public List<SeriesChunk> Chunks { get; } = [];

    public List<SeriesBar> Bars { get; } = [];

    public List<SeriesMarker> Markers { get; } = [];

    public bool MarkersSorted { get; set; }

    public List<MarkerView> MarkerViews { get; } = [];

    public void ApplyOptions(SeriesOptions value)
    {
        if (!ReferenceEquals(options, value))
        {
            options = value;
            ThicknessText = ChartFormat.Pixel(value.Thickness);
            OpacityText = value.Opacity.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }

    // The bars go into the SVG once and this moves all of them, so that a new width does not change the position of each bar
    public void PlaceBars(double offset, double width, bool position)
    {
        if (BarOffset.Equals(offset) && BarWidth.Equals(width))
        {
            return;
        }

        BarOffset = offset;
        BarWidth = width;
        BarOffsetText = position ? ChartFormat.Position(offset) : ChartFormat.Coordinate(offset);
        BarWidthText = position ? ChartFormat.Position(width) : ChartFormat.Coordinate(width);
        BarTransform = String.Create(CultureInfo.InvariantCulture, $"translate({(position ? ChartFormat.Position(offset - (width / 2)) : ChartFormat.Coordinate(offset - (width / 2)))} 0)");
    }

    public void Clear()
    {
        Points.Clear();
        Chunks.Clear();
        Bars.Clear();
        Markers.Clear();
        Gap = true;
        Sequence = -1;
    }

    public void Trim(long minimumSequence)
    {
        var points = 0;
        while ((points < Points.Count) && (Points[points].Sequence < minimumSequence))
        {
            points++;
        }

        Points.RemoveRange(0, points);

        var chunks = 0;
        while ((chunks < Chunks.Count) && (Chunks[chunks].LastSequence < minimumSequence))
        {
            chunks++;
        }

        Chunks.RemoveRange(0, chunks);

        var bars = 0;
        while ((bars < Bars.Count) && (Bars[bars].Key < minimumSequence))
        {
            bars++;
        }

        Bars.RemoveRange(0, bars);

        // Bubbles are kept in the order of their sizes, so that the oldest markers can be anywhere
        var kept = 0;
        for (var i = 0; i < Markers.Count; i++)
        {
            if (Markers[i].Key >= minimumSequence)
            {
                Markers[kept++] = Markers[i];
            }
        }

        Markers.RemoveRange(kept, Markers.Count - kept);
    }
}

internal sealed record SeriesView(SeriesState State, ChartSeriesKind Kind, string Format, double StackGap, Dictionary<long, StackTop>? StackTops, List<MarkerView>? Markers)
{
    public (string? Y, string? Height) GetBarExtent(in SeriesBar bar)
    {
        if (bar.BottomText is not null)
        {
            return (bar.BottomText, bar.HeightText);
        }

        var bottom = bar.Bottom;
        var top = bar.Top;
        if (StackGap > 0)
        {
            var half = StackGap / 2;
            if (bar.Value >= 0)
            {
                var outer = (StackTops is null) || (StackTops.TryGetValue(bar.StackSlot, out var stack) && top.Equals(stack.Positive));
                bottom += bottom.Equals(0d) ? 0 : half;
                top -= outer ? 0 : half;
            }
            else
            {
                var outer = (StackTops is null) || (StackTops.TryGetValue(bar.StackSlot, out var stack) && bottom.Equals(stack.Negative));
                top -= top.Equals(0d) ? 0 : half;
                bottom += outer ? 0 : half;
            }

            if (top <= bottom)
            {
                return (null, null);
            }
        }

        return (ChartFormat.Data(bottom), ChartFormat.Data(top - bottom));
    }
}

internal readonly record struct MarkerView(long Key, string XText, string YText, string? SizeText, string Cx, string Cy, string R, string? Path);

internal readonly record struct AxisLabel(string Position, string Text);

internal readonly record struct ThresholdLine(string X1, string Y1, string X2, string Y2, string LabelX, string LabelY, string Class, string? Color, string? Label);

internal readonly record struct ThresholdBand(string X, string Y, string Width, string Height, string Class, string? Color);

internal readonly record struct LivePoint(string X, string Y, string Color);

internal sealed record LegendRow(ChartSeries Series, string Title, string Color, bool Hidden, bool Dot, List<string> Values);

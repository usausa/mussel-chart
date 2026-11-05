namespace MusselChart.Internal;

using System.Text;

internal readonly record struct SeriesPoint(long Sequence, long Ticks, double X, double Value, double Top, double Bottom);

internal readonly record struct SeriesBar(long Key, string XText, double X, double Bottom, double Top, double Value, long StackSlot);

internal readonly record struct SeriesMarker(long Key, string XText, double X, double Y, double Size);

internal readonly record struct StackTop(double Positive, double Negative);

internal sealed class SeriesChunk
{
    private readonly StringBuilder line = new();

    private readonly StringBuilder values = new();

    private readonly List<(string X, double Bottom)> bottoms = [];

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

    public string LinePoints { get; private set; } = string.Empty;

    public string AreaPoints { get; private set; } = string.Empty;

    public string? Values { get; private set; }

    public void Add(long sequence, string x, double top, double bottom, double value, bool area, bool stacked)
    {
        if (Count > 0)
        {
            line.Append(' ');
        }

        line.Append(x).Append(',').Append(ChartFormat.Data(top));
        Count++;
        LastSequence = sequence;
        LastX = x;
        LastTop = top;
        LastBottom = bottom;
        LastValue = value;
        LinePoints = line.ToString();

        if (stacked)
        {
            if (values.Length > 0)
            {
                values.Append(' ');
            }

            values.Append(ChartFormat.Data(value));
            Values = values.ToString();
        }

        if (!area)
        {
            return;
        }

        bottoms.Add((x, bottom));
        var builder = new StringBuilder((LinePoints.Length * 2) + 2);
        builder.Append(LinePoints);
        for (var i = bottoms.Count - 1; i >= 0; i--)
        {
            builder.Append(' ').Append(bottoms[i].X).Append(',').Append(ChartFormat.Data(bottoms[i].Bottom));
        }

        AreaPoints = builder.ToString();
    }
}

internal sealed class SeriesState
{
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

    public List<SeriesPoint> Points { get; } = [];

    public List<SeriesChunk> Chunks { get; } = [];

    public List<SeriesBar> Bars { get; } = [];

    public List<SeriesMarker> Markers { get; } = [];

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
        Chunks.RemoveAll(x => x.LastSequence < minimumSequence);
        Bars.RemoveAll(x => x.Key < minimumSequence);
        Markers.RemoveAll(x => x.Key < minimumSequence);
    }
}

internal sealed record SeriesView(SeriesState State, ChartSeriesKind Kind, string Color, string Thickness, string Opacity, double Offset, double HalfWidth, string Width, string Format, double StackGap, IReadOnlyDictionary<long, StackTop>? StackTops, IReadOnlyList<MarkerView>? Markers)
{
    public (string? Y, string? Height) GetBarExtent(in SeriesBar bar)
    {
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

internal sealed record MarkerView(long Key, string XText, string YText, string? SizeText, string Cx, string Cy, string R, string Size, string? Path);

internal sealed record AxisLabel(string Position, string Text);

internal sealed record ThresholdLine(string X1, string Y1, string X2, string Y2, string LabelX, string LabelY, string Class, string? Color, string? Label);

internal sealed record ThresholdBand(string X, string Y, string Width, string Height, string Class, string? Color);

internal sealed record LivePoint(string X, string Y, string Color);

internal sealed record LegendRow(ChartSeries Series, string Title, string Color, bool Hidden, bool Dot, IReadOnlyList<string> Values);

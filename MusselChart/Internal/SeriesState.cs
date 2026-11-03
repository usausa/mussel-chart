namespace MusselChart.Internal;

using System.Text;

internal readonly record struct SeriesPoint(long Sequence, long Ticks, double X, double Value, double Top, double Bottom);

internal readonly record struct SeriesBar(long Key, double X, double Bottom, double Top);

internal sealed class SeriesChunk
{
    private readonly StringBuilder line = new();

    private readonly List<(double X, double Bottom)> bottoms = [];

    public SeriesChunk(long key)
    {
        Key = key;
    }

    public long Key { get; }

    public int Count { get; private set; }

    public long LastSequence { get; private set; }

    public double LastX { get; private set; }

    public double LastTop { get; private set; }

    public double LastBottom { get; private set; }

    public string LinePoints { get; private set; } = string.Empty;

    public string AreaPoints { get; private set; } = string.Empty;

    public void Add(long sequence, double x, double top, double bottom, bool area)
    {
        if (Count > 0)
        {
            line.Append(' ');
        }

        line.Append(ChartFormat.Coordinate(x)).Append(',').Append(ChartFormat.Data(top));
        Count++;
        LastSequence = sequence;
        LastX = x;
        LastTop = top;
        LastBottom = bottom;
        LinePoints = line.ToString();

        if (!area)
        {
            return;
        }

        bottoms.Add((x, bottom));
        var builder = new StringBuilder((LinePoints.Length * 2) + 2);
        builder.Append(LinePoints);
        for (var i = bottoms.Count - 1; i >= 0; i--)
        {
            builder.Append(' ').Append(ChartFormat.Coordinate(bottoms[i].X)).Append(',').Append(ChartFormat.Data(bottoms[i].Bottom));
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

    public void Clear()
    {
        Points.Clear();
        Chunks.Clear();
        Bars.Clear();
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
    }
}

internal sealed record SeriesView(SeriesState State, ChartSeriesKind Kind, string Color, string Thickness, string Opacity, double HalfWidth, string Width, string Format, string Stack);

internal sealed record AxisLabel(string Position, string Text);

internal sealed record ThresholdLine(string Y, string Class, string? Color, string? Label);

internal sealed record ThresholdBand(string Y, string Height, string Class, string? Color);

internal sealed record LivePoint(string X, string Y, string Color);

internal sealed record LegendRow(ChartSeries Series, string Title, string Color, bool Hidden, IReadOnlyList<string> Values);

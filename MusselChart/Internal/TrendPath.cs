namespace MusselChart.Internal;

using System.Globalization;
using System.Text;

internal readonly record struct TrendBar(string X, string Y, string Height, bool Negative);

internal sealed class TrendPath
{
    public const double ViewHeight = 100;

    private TrendPath(int count, string line, string area, IReadOnlyList<TrendBar> bars)
    {
        Count = count;
        Line = line;
        Area = area;
        Bars = bars;
    }

    public static TrendPath Empty { get; } = new(0, string.Empty, string.Empty, []);

    public int Count { get; }

    public string Line { get; }

    public string Area { get; }

    public IReadOnlyList<TrendBar> Bars { get; }

    public string ViewBox(bool bars) =>
        String.Create(CultureInfo.InvariantCulture, $"0 0 {(bars ? Count : Math.Max(Count - 1, 1))} {ViewHeight}");

    // Wins fill the upper half and losses the lower half with a pixel between them, and the rest is not drawn
    public static TrendPath CreateWinLoss(IEnumerable<double>? source, double height)
    {
        if (source is null)
        {
            return Empty;
        }

        var values = source.ToArray();
        if (values.Length == 0)
        {
            return Empty;
        }

        var middle = ViewHeight / 2;
        var gap = height > 0 ? ViewHeight / height / 2 : 0;
        var extent = ChartFormat.Coordinate(middle - gap);
        var lossTop = ChartFormat.Coordinate(middle + gap);
        var list = new List<TrendBar>(values.Length);
        for (var i = 0; i < values.Length; i++)
        {
            if (Double.IsFinite(values[i]) && (values[i] != 0))
            {
                var negative = values[i] < 0;
                list.Add(new TrendBar(ChartFormat.Coordinate(i + 0.15), negative ? lossTop : "0", extent, negative));
            }
        }

        return new TrendPath(values.Length, string.Empty, string.Empty, list);
    }

    public static TrendPath Create(IEnumerable<double>? source, double? minimum, double? maximum, bool bars, double inset)
    {
        if (source is null)
        {
            return Empty;
        }

        var values = source.ToArray();
        if (values.Length == 0)
        {
            return Empty;
        }

        var finite = values.Where(Double.IsFinite).ToArray();
        var low = minimum ?? (finite.Length > 0 ? finite.Min() : 0);
        var high = maximum ?? (finite.Length > 0 ? finite.Max() : 1);
        if (bars)
        {
            low = Math.Min(low, 0);
            high = Math.Max(high, 0);
        }

        if (!(high > low))
        {
            low -= 1;
            high += 1;
        }

        var span = high - low;
        var usable = ViewHeight - (inset * 2);
        double ToY(double value) => ViewHeight - inset - ((Math.Clamp(value, low, high) - low) / span * usable);

        if (bars)
        {
            var list = new List<TrendBar>(values.Length);
            var zero = ToY(0);
            for (var i = 0; i < values.Length; i++)
            {
                if (!Double.IsFinite(values[i]))
                {
                    continue;
                }

                var y = ToY(values[i]);
                list.Add(new TrendBar(ChartFormat.Coordinate(i + 0.15), ChartFormat.Coordinate(Math.Min(y, zero)), ChartFormat.Coordinate(Math.Abs(zero - y)), values[i] < 0));
            }

            return new TrendPath(values.Length, string.Empty, string.Empty, list);
        }

        var line = new StringBuilder();
        for (var i = 0; i < values.Length; i++)
        {
            if (!Double.IsFinite(values[i]))
            {
                continue;
            }

            if (line.Length > 0)
            {
                line.Append(' ');
            }

            line.Append(ChartFormat.Coordinate(i)).Append(',').Append(ChartFormat.Coordinate(ToY(values[i])));
        }

        var text = line.ToString();
        var area = String.Create(CultureInfo.InvariantCulture, $"0,{ViewHeight} {text} {values.Length - 1},{ViewHeight}");
        return new TrendPath(values.Length, text, area, []);
    }
}

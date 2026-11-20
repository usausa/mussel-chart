namespace MusselChart.Internal;

using System.Globalization;

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

    public static TrendPath CreateWinLoss(IEnumerable<double>? source, List<double> copied, double height)
    {
        var values = Read(source, copied);
        if (values.Count == 0)
        {
            return Empty;
        }

        var middle = ViewHeight / 2;
        var gap = height > 0 ? ViewHeight / height / 2 : 0;
        var extent = ChartFormat.Coordinate(middle - gap);
        var lossTop = ChartFormat.Coordinate(middle + gap);
        var list = new List<TrendBar>(values.Count);
        for (var i = 0; i < values.Count; i++)
        {
            if (Double.IsFinite(values[i]) && (values[i] != 0))
            {
                var negative = values[i] < 0;
                list.Add(new TrendBar(ChartFormat.Coordinate(i + 0.15), negative ? lossTop : "0", extent, negative));
            }
        }

        return new TrendPath(values.Count, string.Empty, string.Empty, list);
    }

    public static TrendPath Create(IEnumerable<double>? source, List<double> copied, double? minimum, double? maximum, bool bars, double inset)
    {
        var values = Read(source, copied);
        if (values.Count == 0)
        {
            return Empty;
        }

        var smallest = Double.PositiveInfinity;
        var largest = Double.NegativeInfinity;
        // ReSharper disable once ForCanBeConvertedToForeach
        for (var i = 0; i < values.Count; i++)
        {
            if (Double.IsFinite(values[i]))
            {
                smallest = Math.Min(smallest, values[i]);
                largest = Math.Max(largest, values[i]);
            }
        }

        (minimum, maximum) = ChartRange.FixedEnds(minimum, maximum);
        var found = smallest <= largest;
        var low = minimum ?? (found ? smallest : 0);
        var high = maximum ?? (found ? largest : 1);

        if (bars)
        {
            low = Math.Min(low, 0);
            high = Math.Max(high, 0);
        }

        if (!(high > low))
        {
            var onlyMaximum = (maximum is not null) && (minimum is null);
            var kept = Math.Abs(onlyMaximum ? high : low);
            var tenth = kept * 0.1;
            var delta = Double.IsNormal(tenth) ? Math.Min(tenth, Double.MaxValue - kept) : 1;
            if ((minimum is not null) && (maximum is null))
            {
                high = low + delta;
            }
            else if (onlyMaximum)
            {
                low = high - delta;
            }
            else
            {
                low -= delta;
                high += delta;
            }
        }

        var span = (high / 2) - (low / 2);
        var usable = ViewHeight - (inset * 2);
        double ToY(double value) => span > 0 ? ViewHeight - inset - (((Math.Clamp(value, low, high) / 2) - (low / 2)) / span * usable) : ViewHeight / 2;

        if (bars)
        {
            var list = new List<TrendBar>(values.Count);
            var zero = ToY(0);
            for (var i = 0; i < values.Count; i++)
            {
                if (!Double.IsFinite(values[i]))
                {
                    continue;
                }

                var y = ToY(values[i]);
                list.Add(new TrendBar(ChartFormat.Coordinate(i + 0.15), ChartFormat.Coordinate(Math.Min(y, zero)), ChartFormat.Coordinate(Math.Abs(zero - y)), values[i] < 0));
            }

            return new TrendPath(values.Count, string.Empty, string.Empty, list);
        }

        var line = ChartStringBuilder.Rent();
        var first = -1;
        var last = -1;
        for (var i = 0; i < values.Count; i++)
        {
            if (!Double.IsFinite(values[i]))
            {
                continue;
            }

            if (line.Length > 0)
            {
                line.Append(' ');
            }

            first = first < 0 ? i : first;
            last = i;
            line.Append(CultureInfo.InvariantCulture, $"{i},{ToY(values[i]):0.###}");
        }

        var text = ChartStringBuilder.ToStringAndReturn(line);
        var area = first < 0 ? string.Empty : String.Create(CultureInfo.InvariantCulture, $"{first},{ViewHeight} {text} {last},{ViewHeight}");
        return new TrendPath(values.Count, text, area, []);
    }

    private static IReadOnlyList<double> Read(IEnumerable<double>? source, List<double> copied)
    {
        switch (source)
        {
            case null:
                return [];
            case ChartRingBuffer<double> buffer:
                return buffer.CopyTo(copied);
            case IReadOnlyList<double> list:
                return list;
            default:
                copied.Clear();
                copied.AddRange(source);
                return copied;
        }
    }
}

namespace MusselChart;

using System.Globalization;

using MusselChart.Internal;

public sealed partial class MusselChartView
{
    private const int ChunkSize = 32;

    private const long RebaseTicks = 100_000 * TimeSpan.TicksPerSecond;

    private readonly Dictionary<ChartSeries, SeriesState> states = new(ReferenceEqualityComparer.Instance);

    private readonly List<SeriesState> orderedStates = [];

    private readonly List<ChartPointData> reading = [];

    private readonly List<(SeriesState State, ChartBufferRead Read, int Start, int Count)> reads = [];

    private readonly Dictionary<IChartBuffer, ChartBufferMark> marks = [];

    private readonly Dictionary<(ChartSeriesKind Kind, string Group), Dictionary<long, StackTop>> stacks = [];

    private readonly Dictionary<(ChartSeriesKind Kind, string Group), Dictionary<long, StackTop>> totals = [];

    private readonly Dictionary<string, int> categoryIndexes = new(StringComparer.Ordinal);

    private readonly List<string> categories = [];

    private bool rebuildRequired = true;

    private long baseTicks;

    private double numberBase;

    private double valueBase;

    private bool hasValueBase;

    private ChartXKind xKind;

    private ChartStackMode stackMode;

    private void SyncData()
    {
        var ordered = orderedStates;
        ordered.Clear();
        foreach (var series in seriesList)
        {
            WatchSource(series.Source);
            if (!states.TryGetValue(series, out var state))
            {
                state = new SeriesState(series);
                states[series] = state;
                rebuildRequired = true;
            }

            var visible = series.IsVisible && !hiddenSeries.Contains(series);
            if (visible != state.Visible)
            {
                state.Visible = visible;
                rebuildRequired = true;
            }

            if (!ReferenceEquals(state.Source, series.Source))
            {
                rebuildRequired = true;
            }

            ordered.Add(state);
        }

        ApplySources();
        var kind = ResolveXKind();
        if ((kind != xKind) || (stackMode != StackMode))
        {
            xKind = kind;
            stackMode = StackMode;
            rebuildRequired = true;
        }

        var appendable = (xKind == ChartXKind.Time) && (stackMode == ChartStackMode.Normal);
        var rebuild = rebuildRequired || !appendable || !Read(ordered, false);
        if (!rebuild && (reading.Count > 0) && (reading[^1].Ticks - baseTicks > RebaseTicks))
        {
            rebuild = true;
        }

        if (rebuild)
        {
            Read(ordered, true);
            stacks.Clear();
            totals.Clear();
            categories.Clear();
            categoryIndexes.Clear();
            foreach (var state in ordered)
            {
                state.Clear();
            }

            var minimum = Int64.MaxValue;
            var leftmost = Double.PositiveInfinity;
            foreach (var (state, _, start, count) in reads)
            {
                for (var i = start; i < start + count; i++)
                {
                    var point = reading[i];
                    minimum = (point.Ticks != 0) ? Math.Min(minimum, point.Ticks) : minimum;
                    leftmost = (state.Series.XKind == ChartXKind.Number) && Double.IsFinite(point.Number) ? Math.Min(leftmost, point.Number) : leftmost;
                    if ((xKind == ChartXKind.Category) && (point.Category is { } category))
                    {
                        AddCategory(category);
                    }
                }
            }

            baseTicks = (xKind != ChartXKind.Time) || (minimum == Int64.MaxValue) ? 0 : minimum - (minimum % TimeSpan.TicksPerSecond);
            numberBase = (xKind == ChartXKind.Number) && Double.IsFinite(leftmost) ? leftmost : 0;
            baseText = xKind == ChartXKind.Time
                ? ((baseTicks - DateTime.UnixEpoch.Ticks) / TimeSpan.TicksPerSecond).ToString(CultureInfo.InvariantCulture)
                : numberBase.ToString(CultureInfo.InvariantCulture);
            valueBase = 0;
            valueBaseText = "0";
            hasValueBase = stackMode == ChartStackMode.Percent;
            if (stackMode == ChartStackMode.Percent)
            {
                CollectTotals();
            }
        }

        if (!hasValueBase)
        {
            ChooseValueBase();
        }

        foreach (var (state, read, start, count) in reads)
        {
            var first = read.Sequence - count + 1;
            for (var i = 0; i < count; i++)
            {
                Append(state, reading[start + i], first + i, i);
            }

            state.Source = state.Series.Source;
            state.Sequence = read.Sequence;
            state.Generation = read.Generation;
            state.Trim(read.Sequence - read.Count + 1);
        }

        if (xKind == ChartXKind.Time)
        {
            PruneStacks(ordered);
        }

        rebuildRequired = false;
    }

    private ChartXKind ResolveXKind() => xAxis switch
    {
        DateTimeAxis => ChartXKind.Time,
        CategoryAxis => ChartXKind.Category,
        NumericXAxis => ChartXKind.Number,
        _ => seriesList.Count > 0 ? seriesList[0].XKind : ChartXKind.Time
    };

    private bool Read(List<SeriesState> ordered, bool full)
    {
        reading.Clear();
        reads.Clear();
        marks.Clear();
        foreach (var state in ordered)
        {
            if (state.Series.Source is IChartBuffer buffer)
            {
                marks.TryAdd(buffer, buffer.Mark());
            }
        }

        var appended = true;
        foreach (var state in ordered)
        {
            var start = reading.Count;
            ChartBufferMark? until = state.Series.Source is IChartBuffer buffer ? marks[buffer] : null;
            var read = state.Series.ReadPoints(full ? -1 : state.Sequence, state.Generation, until, zone, reading);
            appended &= read.Appended;
            reads.Add((state, read, start, reading.Count - start));
        }

        foreach (var (buffer, mark) in marks)
        {
            AlignOldest(buffer, mark);
        }

        return appended;
    }

    private void AlignOldest(IChartBuffer buffer, ChartBufferMark mark)
    {
        var oldest = Int64.MinValue;
        foreach (var (state, read, _, _) in reads)
        {
            if (ReferenceEquals(state.Series.Source, buffer) && (read.Generation == mark.Generation))
            {
                oldest = Math.Max(oldest, read.Sequence - read.Count + 1);
            }
        }

        for (var i = 0; i < reads.Count; i++)
        {
            var (state, read, start, count) = reads[i];
            if (ReferenceEquals(state.Series.Source, buffer) && (read.Generation == mark.Generation) && (read.Sequence - read.Count + 1 < oldest))
            {
                var kept = (int)Math.Max(read.Sequence - oldest + 1, 0);
                var dropped = Math.Max(count - kept, 0);
                reads[i] = (state, read with { Count = kept }, start + dropped, count - dropped);
            }
        }
    }

    private int AddCategory(string category)
    {
        if (!categoryIndexes.TryGetValue(category, out var index))
        {
            index = categories.Count;
            categoryIndexes.Add(category, index);
            categories.Add(category);
        }

        return index;
    }

    private int GetCategoryIndex(string? category, int position)
    {
        if (category is not null)
        {
            return AddCategory(category);
        }

        while (categories.Count <= position)
        {
            categories.Add(string.Empty);
        }

        return position;
    }

    private double GetX(SeriesState state, in ChartPointData point, int position) => xKind switch
    {
        ChartXKind.Time => state.Series.XKind == ChartXKind.Time ? (point.Ticks - baseTicks) / (double)TimeSpan.TicksPerSecond : Double.NaN,
        ChartXKind.Number => state.Series.XKind == ChartXKind.Number ? point.Number : Double.NaN,
        _ => GetCategoryIndex(point.Category, position)
    };

    private long GetStackSlot(double x, in ChartPointData point) =>
        xKind == ChartXKind.Time ? point.Ticks : BitConverter.DoubleToInt64Bits(x);

    private string FormatX(double x) => xKind switch
    {
        ChartXKind.Time => ChartFormat.Coordinate(x),
        ChartXKind.Number => ChartFormat.Position(x - numberBase),
        _ => ChartFormat.Position(x)
    };

    private void ChooseValueBase()
    {
        var lowest = Double.PositiveInfinity;
        foreach (var (state, _, start, count) in reads)
        {
            if (!state.Visible)
            {
                continue;
            }

            var range = state.Series.Kind == ChartSeriesKind.RangeBar;
            for (var i = start; i < start + count; i++)
            {
                var point = reading[i];
                lowest = Double.IsFinite(point.Value) ? Math.Min(lowest, point.Value) : lowest;
                lowest = range && Double.IsFinite(point.Low) ? Math.Min(lowest, point.Low) : lowest;
            }
        }

        if (Double.IsFinite(lowest))
        {
            valueBase = lowest;
            valueBaseText = lowest.ToString(CultureInfo.InvariantCulture);
            hasValueBase = true;
        }
    }

    private void CollectTotals()
    {
        foreach (var (state, _, start, count) in reads)
        {
            if (!state.Visible || (state.Series.StackKey is not { } stackKey))
            {
                continue;
            }

            var key = (state.Series.Kind, stackKey);
            if (!totals.TryGetValue(key, out var group))
            {
                group = [];
                totals[key] = group;
            }

            for (var i = 0; i < count; i++)
            {
                var point = reading[start + i];
                var x = GetX(state, point, i);
                if (!Double.IsFinite(x) || !Double.IsFinite(point.Value))
                {
                    continue;
                }

                var slot = GetStackSlot(x, point);
                var total = group.GetValueOrDefault(slot);
                group[slot] = point.Value >= 0 ? total with { Positive = total.Positive + point.Value } : total with { Negative = total.Negative - point.Value };
            }
        }
    }

    private void Append(SeriesState state, in ChartPointData point, long sequence, int position)
    {
        var kind = state.Series.Kind;
        var x = GetX(state, point, position);
        var value = kind switch
        {
            ChartSeriesKind.RangeBar when !Double.IsFinite(point.Low) => Double.NaN,
            ChartSeriesKind.RangeBar => Math.Max(point.Low, point.Value),
            ChartSeriesKind.Bubble when !Double.IsFinite(point.Size) => Double.NaN,
            _ => point.Value
        };

        if (!Double.IsFinite(x))
        {
            value = Double.NaN;
        }

        var bottom = kind == ChartSeriesKind.RangeBar ? Math.Min(point.Low, point.Value) : 0d;
        var top = value;
        var stackKey = state.Visible ? state.Series.StackKey : null;
        var slot = 0L;
        if ((stackKey is not null) && Double.IsFinite(value))
        {
            slot = GetStackSlot(x, point);
            (top, bottom) = Stack((kind, stackKey), slot, value);
        }

        state.Points.Add(new SeriesPoint(sequence, point.Ticks, x, value, top, bottom));
        if (!Double.IsFinite(value))
        {
            state.Gap = true;
            return;
        }

        var text = FormatX(x);
        switch (kind)
        {
            case ChartSeriesKind.Bar:
            case ChartSeriesKind.RangeBar:
                AppendBar(state, text, sequence, bottom, top, value, slot, stackKey is not null);
                return;
            case ChartSeriesKind.Scatter:
            case ChartSeriesKind.Bubble:
                state.Markers.Add(new SeriesMarker(sequence, text, ChartFormat.Position(value - valueBase), kind == ChartSeriesKind.Bubble ? ChartFormat.Position(point.Size) : null, x, value, point.Size));
                state.MarkersSorted = false;
                return;
        }

        var area = kind == ChartSeriesKind.Area;
        var stacked = stackKey is not null;
        var chunk = state.Chunks.Count > 0 ? state.Chunks[^1] : null;
        if ((chunk is null) || state.Gap || (chunk.Count >= ChunkSize))
        {
            var next = new SeriesChunk(sequence);
            if ((chunk is not null) && !state.Gap)
            {
                next.Add(chunk.LastSequence, chunk.LastX, chunk.LastTop, chunk.LastBottom, chunk.LastValue, area, stacked);
            }

            state.Chunks.Add(next);
            chunk = next;
        }

        chunk.Add(sequence, text, top - valueBase, bottom - valueBase, value, area, stacked);
        state.Gap = false;
    }

    private void AppendBar(SeriesState state, string text, long sequence, double bottom, double top, double value, long slot, bool stacked)
    {
        var low = Math.Min(bottom, top);
        var high = Math.Max(bottom, top);
        var lowText = stacked ? null : ChartFormat.Position(low - valueBase);
        var heightText = stacked ? null : ChartFormat.Position(high - low);
        state.Bars.Add(new SeriesBar(sequence, text, ChartFormat.Position(value), lowText, heightText, low, high, value, slot));
    }

    private (double Top, double Bottom) Stack((ChartSeriesKind Kind, string Group) key, long slot, double value)
    {
        if (!stacks.TryGetValue(key, out var tops))
        {
            tops = [];
            stacks[key] = tops;
        }

        var scaled = value;
        if ((stackMode == ChartStackMode.Percent) && totals.TryGetValue(key, out var group) && group.TryGetValue(slot, out var total))
        {
            var sum = value >= 0 ? total.Positive : total.Negative;
            scaled = sum > 0 ? value / sum * 100 : 0;
        }

        var current = tops.GetValueOrDefault(slot);
        if (value >= 0)
        {
            var top = current.Positive + scaled;
            tops[slot] = current with { Positive = top };
            return (top, current.Positive);
        }

        var low = current.Negative + scaled;
        tops[slot] = current with { Negative = low };
        return (low, current.Negative);
    }

    private void PruneStacks(List<SeriesState> ordered)
    {
        foreach (var (key, tops) in stacks)
        {
            var minimum = Int64.MaxValue;
            foreach (var state in ordered)
            {
                if ((state.Points.Count > 0) && (state.Series.Kind == key.Kind) && String.Equals(state.Series.StackKey, key.Group, StringComparison.Ordinal))
                {
                    minimum = Math.Min(minimum, state.Points[0].Ticks);
                }
            }

            foreach (var slot in tops.Keys)
            {
                if (slot < minimum)
                {
                    tops.Remove(slot);
                }
            }
        }
    }
}

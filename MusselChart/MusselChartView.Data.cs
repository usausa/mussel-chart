namespace MusselChart;

using MusselChart.Internal;

public sealed partial class MusselChartView
{
    private const int ChunkSize = 32;

    private const long RebaseTicks = 100_000 * TimeSpan.TicksPerSecond;

    private readonly Dictionary<ChartSeries, SeriesState> states = new(ReferenceEqualityComparer.Instance);

    private readonly List<ChartPointData> reading = [];

    private readonly List<(SeriesState State, ChartBufferRead Read, int Start, int Count)> reads = [];

    private readonly Dictionary<string, Dictionary<long, double>> stacks = new(StringComparer.Ordinal);

    private bool rebuildRequired = true;

    private long baseTicks;

    private void SyncData()
    {
        var ordered = new List<SeriesState>(seriesList.Count);
        foreach (var series in seriesList)
        {
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

        var rebuild = rebuildRequired || !Read(ordered, false);
        if (!rebuild && (reading.Count > 0) && (reading[^1].Ticks - baseTicks > RebaseTicks))
        {
            rebuild = true;
        }

        if (rebuild)
        {
            Read(ordered, true);
            stacks.Clear();
            foreach (var state in ordered)
            {
                state.Clear();
            }

            var minimum = Int64.MaxValue;
            foreach (var point in reading)
            {
                minimum = Math.Min(minimum, point.Ticks);
            }

            baseTicks = minimum == Int64.MaxValue ? 0 : minimum - (minimum % TimeSpan.TicksPerSecond);
        }

        foreach (var (state, read, start, count) in reads)
        {
            var first = read.Sequence - count + 1;
            for (var i = 0; i < count; i++)
            {
                Append(state, reading[start + i], first + i);
            }

            state.Source = state.Series.Source;
            state.Sequence = read.Sequence;
            state.Generation = read.Generation;
            state.Trim(read.Sequence - read.Count + 1);
        }

        PruneStacks(ordered);
        rebuildRequired = false;
    }

    private bool Read(List<SeriesState> ordered, bool full)
    {
        reading.Clear();
        reads.Clear();
        var appended = true;
        foreach (var state in ordered)
        {
            var start = reading.Count;
            var read = state.Series.ReadPoints(full ? -1 : state.Sequence, state.Generation, zone, reading);
            appended &= read.Appended;
            reads.Add((state, read, start, reading.Count - start));
        }

        return appended;
    }

    private void Append(SeriesState state, ChartPointData point, long sequence)
    {
        var x = (point.Ticks - baseTicks) / (double)TimeSpan.TicksPerSecond;
        var value = point.Value;
        var bottom = 0d;
        var top = value;
        var stackKey = state.Visible ? state.Series.StackKey : null;
        if ((stackKey is not null) && Double.IsFinite(value))
        {
            if (!stacks.TryGetValue(stackKey, out var tops))
            {
                tops = [];
                stacks[stackKey] = tops;
            }

            bottom = tops.GetValueOrDefault(point.Ticks);
            top = bottom + value;
            tops[point.Ticks] = top;
        }

        state.Points.Add(new SeriesPoint(sequence, point.Ticks, x, value, top, bottom));
        if (!Double.IsFinite(value))
        {
            state.Gap = true;
            return;
        }

        if (state.Series.Kind == ChartSeriesKind.Bar)
        {
            state.Bars.Add(new SeriesBar(sequence, x, Math.Min(bottom, top), Math.Max(bottom, top)));
            return;
        }

        var area = state.Series.Kind == ChartSeriesKind.Area;
        var chunk = state.Chunks.Count > 0 ? state.Chunks[^1] : null;
        if ((chunk is null) || state.Gap || (chunk.Count >= ChunkSize))
        {
            var next = new SeriesChunk(sequence);
            if ((chunk is not null) && !state.Gap)
            {
                next.Add(chunk.LastSequence, chunk.LastX, chunk.LastTop, chunk.LastBottom, area);
            }

            state.Chunks.Add(next);
            chunk = next;
        }

        chunk.Add(sequence, x, top, bottom, area);
        state.Gap = false;
    }

    private void PruneStacks(List<SeriesState> ordered)
    {
        foreach (var (key, tops) in stacks)
        {
            var minimum = Int64.MaxValue;
            foreach (var state in ordered)
            {
                if ((state.Points.Count > 0) && String.Equals(state.Series.StackKey, key, StringComparison.Ordinal))
                {
                    minimum = Math.Min(minimum, state.Points[0].Ticks);
                }
            }

            if (tops.Count > 0)
            {
                foreach (var stale in tops.Keys.Where(x => x < minimum).ToList())
                {
                    tops.Remove(stale);
                }
            }
        }
    }
}

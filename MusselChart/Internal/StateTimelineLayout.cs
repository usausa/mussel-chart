namespace MusselChart.Internal;

using System.Globalization;
using System.Text;

internal readonly record struct StateTimelineEntry(int DataIndex, string? Row, double Start, double End, string? Text, double Number)
{
    public bool HasState => (Text is not null) || Double.IsFinite(Number);
}

internal readonly record struct StateTimelineSegment(int Row, double Start, double End, int State);

internal readonly record struct StateTimelineState(string Text, string Color, bool Discrete);

internal sealed record StateTimelineLayoutOptions
{
    public IReadOnlyList<ChartValueMapping>? Mappings { get; init; }

    public IReadOnlyList<ChartThreshold>? Thresholds { get; init; }

    public bool MergeValues { get; init; } = true;

    public string? ValueFormat { get; init; }

    public Func<double, string>? ValueFormatter { get; init; }

    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;
}

internal sealed class StateTimelineLayout
{
    private static readonly string[] Continuous = [ChartColor.Level(ChartThresholdLevel.Good), ChartColor.Level(ChartThresholdLevel.Warning), ChartColor.Level(ChartThresholdLevel.Critical)];

    private static readonly (int R, int G, int B)[] ContinuousStops = HeatmapPalette.Parse(Continuous);

    private readonly List<(int Row, StateTimelineEntry Entry)> sorted = [];

    private readonly List<string?> rowNames = [];

    private readonly Dictionary<string, int> rowIndexes = new(StringComparer.Ordinal);

    private readonly List<double> rowMinimums = [];

    private readonly List<double> rowMaximums = [];

    private readonly List<StateTimelineSegment> segments = [];

    private readonly List<StateTimelineState> states = [];

    private readonly Dictionary<(string Text, string Color), int> stateIndexes = [];

    private readonly Dictionary<string, ChartValueMapping> mappings = new(StringComparer.Ordinal);

    private readonly List<ChartThreshold> steps = [];

    private readonly List<string> stepLabels = [];

    private readonly List<string> stepColors = [];

    private readonly List<(string Text, string Color)> legend = [];

    private bool hasNumbers;

    private StateTimelineLayoutOptions options = new();

    public IReadOnlyList<string?> RowNames => rowNames;

    public IReadOnlyList<StateTimelineSegment> Segments => segments;

    public IReadOnlyList<StateTimelineState> States => states;

    public IReadOnlyList<(string Text, string Color)> Legend => legend;

    public bool HasRowNames { get; private set; }

    public static string FormatDuration(double ticks)
    {
        var milliseconds = ticks / TimeSpan.TicksPerMillisecond;
        if (!(milliseconds >= 0))
        {
            return string.Empty;
        }

        var seconds = Math.Floor(milliseconds / 1000);
        var minutes = Math.Floor(seconds / 60);
        seconds %= 60;
        var hours = Math.Floor(minutes / 60);
        minutes %= 60;
        var days = Math.Floor(hours / 24);
        hours %= 24;
        var years = Math.Floor(days / 365);
        if (years > 0)
        {
            days %= 365;
        }

        var months = Math.Floor(days / 30);
        if (months > 0)
        {
            days %= 30;
        }

        var weeks = Math.Floor(days / 7);
        if (weeks > 0)
        {
            days %= 7;
        }

        var rest = Math.Round((milliseconds % 1000) * 1000) / 1000;
        var builder = ChartStringBuilder.Rent();
        if (years > 0)
        {
            AppendPart(builder, years, "y");
            AppendPart(builder, months, "mo");
            AppendPart(builder, weeks, "w");
            AppendPart(builder, days, "d");
        }
        else if (months > 0)
        {
            AppendPart(builder, months, "mo");
            AppendPart(builder, weeks, "w");
            AppendPart(builder, days, "d");
        }
        else if (weeks > 0)
        {
            AppendPart(builder, weeks, "w");
            AppendPart(builder, days, "d");
        }
        else if (days > 0)
        {
            AppendPart(builder, days, "d");
            AppendPart(builder, hours, "h");
        }
        else if (hours > 0)
        {
            AppendPart(builder, hours, "h");
            AppendPart(builder, minutes, "m");
        }
        else if (minutes > 0)
        {
            AppendPart(builder, minutes, "m");
            AppendPart(builder, seconds, "s");
        }
        else if (seconds > 0)
        {
            AppendPart(builder, seconds, "s");
            AppendPart(builder, rest, "ms");
        }
        else
        {
            AppendPart(builder, rest, "ms");
        }

        if (builder.Length > 0)
        {
            builder.Length--;
        }
        else
        {
            builder.Append('0');
        }

        return ChartStringBuilder.ToStringAndReturn(builder);
    }

    public void Update(StateTimelineLayoutOptions layoutOptions, List<StateTimelineEntry> entries)
    {
        options = layoutOptions;
        PrepareColors();
        BuildRows(entries);
        BuildSegments();
        BuildLegend();
    }

    private static void AppendPart(StringBuilder builder, double value, string unit)
    {
        if (value > 0)
        {
            builder.Append(CultureInfo.InvariantCulture, $"{value}{unit} ");
        }
    }

    private static string? ToKey(object? value) => value switch
    {
        null => null,
        string text => text,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };

    private string Format(double value) => ChartFormat.Value(value, options.ValueFormat, options.ValueFormatter, options.Culture);

    private static string? GetLevelColor(ChartThresholdLevel level) =>
        level == ChartThresholdLevel.None ? null : ChartColor.Level(level);

    private void PrepareColors()
    {
        mappings.Clear();
        if (options.Mappings is { } mappingList)
        {
            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < mappingList.Count; i++)
            {
                if (ToKey(mappingList[i].Value) is { } key)
                {
                    mappings.TryAdd(key, mappingList[i]);
                }
            }
        }

        steps.Clear();
        if (options.Thresholds is { } thresholds)
        {
            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < thresholds.Count; i++)
            {
                var threshold = thresholds[i];
                var j = steps.Count;
                while ((j > 0) && (steps[j - 1].Value > threshold.Value))
                {
                    j--;
                }

                steps.Insert(j, threshold);
            }
        }

        stepLabels.Clear();
        stepColors.Clear();
        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            stepColors.Add(ChartColor.Threshold(step, ChartColor.Other));
            stepLabels.Add(step.Label ?? (Double.IsNegativeInfinity(step.Value) && (i < steps.Count - 1)
                ? "< " + Format(steps[i + 1].Value)
                : Format(step.Value) + "+"));
        }
    }

    private int GetRowIndex(string? name)
    {
        var key = name ?? string.Empty;
        if (!rowIndexes.TryGetValue(key, out var index))
        {
            index = rowNames.Count;
            rowIndexes.Add(key, index);
            rowNames.Add(name);
            rowMinimums.Add(Double.PositiveInfinity);
            rowMaximums.Add(Double.NegativeInfinity);
        }

        return index;
    }

    private void BuildRows(List<StateTimelineEntry> entries)
    {
        sorted.Clear();
        rowNames.Clear();
        rowIndexes.Clear();
        rowMinimums.Clear();
        rowMaximums.Clear();
        HasRowNames = false;
        hasNumbers = false;
        foreach (var entry in entries)
        {
            if (!Double.IsFinite(entry.Start))
            {
                continue;
            }

            var row = GetRowIndex(entry.Row);
            HasRowNames |= entry.Row is not null;
            sorted.Add((row, entry));
            if ((entry.Text is null) && Double.IsFinite(entry.Number))
            {
                hasNumbers = true;
                rowMinimums[row] = Math.Min(rowMinimums[row], entry.Number);
                rowMaximums[row] = Math.Max(rowMaximums[row], entry.Number);
            }
        }

        sorted.Sort(static (x, y) =>
        {
            var row = x.Row.CompareTo(y.Row);
            if (row != 0)
            {
                return row;
            }

            var start = x.Entry.Start.CompareTo(y.Entry.Start);
            return start != 0 ? start : x.Entry.DataIndex.CompareTo(y.Entry.DataIndex);
        });
    }

    // Each state lasts until the next start of its row, or its own end when that comes first
    private void BuildSegments()
    {
        segments.Clear();
        states.Clear();
        stateIndexes.Clear();
        var count = sorted.Count;
        var i = 0;
        for (var row = 0; row < rowNames.Count; row++)
        {
            var first = segments.Count;
            var lastKey = default(StateKey);
            while ((i < count) && (sorted[i].Row == row))
            {
                var entry = sorted[i].Entry;
                var next = (i + 1 < count) && (sorted[i + 1].Row == row) ? sorted[i + 1].Entry.Start : Double.PositiveInfinity;
                i++;
                var end = Double.IsFinite(entry.End) && (entry.End > entry.Start) ? Math.Min(entry.End, next) : next;
                if (!(end > entry.Start) || !entry.HasState)
                {
                    continue;
                }

                var (state, key) = ResolveState(entry, row);
                if (options.MergeValues && (segments.Count > first) && (key == lastKey) && segments[^1].End.Equals(entry.Start))
                {
                    segments[^1] = segments[^1] with { End = end };
                    continue;
                }

                segments.Add(new StateTimelineSegment(row, entry.Start, end, state));
                lastKey = key;
            }
        }
    }

    private (int State, StateKey Key) ResolveState(in StateTimelineEntry entry, int row)
    {
        var number = entry.Text is null;
        if (number && (steps.Count > 0) && options.MergeValues)
        {
            var step = FindStep(entry.Number);
            return (AddState(stepLabels[step], stepColors[step], true), new StateKey(true, null, step));
        }

        var key = new StateKey(false, entry.Text, number ? entry.Number : Double.NaN);
        var raw = entry.Text ?? Format(entry.Number);
        if ((mappings.Count > 0) && mappings.TryGetValue(entry.Text ?? entry.Number.ToString(CultureInfo.InvariantCulture), out var mapping))
        {
            var color = mapping.Color ?? GetLevelColor(mapping.Level) ?? GetDefaultColor(entry, row);
            return (AddState(mapping.Text ?? raw, color, true), key);
        }

        return (AddState(raw, GetDefaultColor(entry, row), !number || (steps.Count > 0)), key);
    }

    private string GetDefaultColor(in StateTimelineEntry entry, int row)
    {
        if (entry.Text is not null)
        {
            return steps.Count > 0 ? stepColors[0] : Continuous[0];
        }

        if (steps.Count > 0)
        {
            return stepColors[FindStep(entry.Number)];
        }

        var range = rowMaximums[row] - rowMinimums[row];
        var percent = range > 0 ? (entry.Number - rowMinimums[row]) / range : 0;
        return HeatmapPalette.GetColor(ContinuousStops, percent);
    }

    private int FindStep(double value)
    {
        var found = 0;
        for (var i = 0; i < steps.Count; i++)
        {
            if (steps[i].Value <= value)
            {
                found = i;
            }
        }

        return found;
    }

    private int AddState(string text, string color, bool discrete)
    {
        if (!stateIndexes.TryGetValue((text, color), out var index))
        {
            index = states.Count;
            stateIndexes.Add((text, color), index);
            states.Add(new StateTimelineState(text, color, discrete));
        }

        return index;
    }

    private void BuildLegend()
    {
        legend.Clear();
        if ((steps.Count > 0) && hasNumbers)
        {
            for (var i = 0; i < steps.Count; i++)
            {
                legend.Add((stepLabels[i], stepColors[i]));
            }

            return;
        }

        foreach (var state in states)
        {
            if (state.Discrete && (state.Text.Length > 0))
            {
                legend.Add((state.Text, state.Color));
            }
        }
    }

    private readonly record struct StateKey(bool Step, string? Text, double Number);
}

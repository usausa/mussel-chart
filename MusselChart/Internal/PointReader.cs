namespace MusselChart.Internal;

internal sealed class PointReader<TItem>
{
    public List<TItem> Items { get; } = [];

    public static ChartXKind GetKind(Func<TItem, double>? xValue, Func<TItem, string?>? category)
    {
        if (category is not null)
        {
            return ChartXKind.Category;
        }

        return xValue is not null ? ChartXKind.Number : ChartXKind.Time;
    }

    public static ChartPointData CreatePoint(TItem item, Func<TItem, DateTime>? x, Func<TItem, double>? xValue, Func<TItem, string?>? category, TimeZoneInfo zone, double value, double low, double size) =>
        new(x is null ? 0 : ChartTime.ToUtcTicks(x(item), zone), xValue?.Invoke(item) ?? Double.NaN, category?.Invoke(item), value, low, size);

    // ReSharper disable once ParameterTypeCanBeEnumerable.Global
    public ChartBufferRead Fill(IReadOnlyList<TItem> items, long afterSequence, int afterGeneration)
    {
        Items.Clear();
        if (items is ChartRingBuffer<TItem> ring)
        {
            return ring.Read(afterSequence, afterGeneration, Items);
        }

        Items.AddRange(items);
        return new ChartBufferRead(false, Items.Count, 0, Items.Count);
    }

    public void Clear() => Items.Clear();
}

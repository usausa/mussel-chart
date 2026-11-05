namespace MusselChart.Internal;

internal sealed class PointReader<TItem>
{
    private readonly List<TItem> buffer = [];

    public IReadOnlyList<TItem> Items => buffer;

    public static ChartXKind GetKind(Func<TItem, double>? xValue, Func<TItem, string?>? category)
    {
        if (category is not null)
        {
            return ChartXKind.Category;
        }

        return xValue is not null ? ChartXKind.Number : ChartXKind.Time;
    }

    public static ChartPointData CreatePoint(TItem item, Func<TItem, DateTime>? x, Func<TItem, double>? xValue, Func<TItem, string?>? category, TimeZoneInfo zone, double value, double low, double size) =>
        new(x is null ? 0 : ChartTime.ToUtcTicks(x(item), zone), xValue is null ? Double.NaN : xValue(item), category?.Invoke(item), value, low, size);

    public ChartBufferRead Fill(IReadOnlyList<TItem> items, long afterSequence, int afterGeneration)
    {
        buffer.Clear();
        if (items is ChartRingBuffer<TItem> ring)
        {
            return ring.Read(afterSequence, afterGeneration, buffer);
        }

        buffer.AddRange(items);
        return new ChartBufferRead(false, buffer.Count, 0, buffer.Count);
    }

    public void Clear() => buffer.Clear();
}

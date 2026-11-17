namespace MusselChart.Internal;

using System.Collections.Specialized;

internal static class ChartEvents
{
    public static readonly NotifyCollectionChangedEventArgs Reset = new(NotifyCollectionChangedAction.Reset);
}

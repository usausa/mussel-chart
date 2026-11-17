namespace MusselChart.Internal;

using System.Collections.Specialized;

internal sealed class ChartSourceWatcher(Action changed)
{
    private readonly NotifyCollectionChangedEventHandler handler = (_, _) => changed();

    private HashSet<INotifyCollectionChanged> watched = new(ReferenceEqualityComparer.Instance);

    private HashSet<INotifyCollectionChanged> found = new(ReferenceEqualityComparer.Instance);

    public void Add(object? source)
    {
        if (source is INotifyCollectionChanged notifier)
        {
            found.Add(notifier);
        }
    }

    public void Apply()
    {
        foreach (var notifier in watched)
        {
            if (!found.Contains(notifier))
            {
                notifier.CollectionChanged -= handler;
            }
        }

        foreach (var notifier in found)
        {
            if (!watched.Contains(notifier))
            {
                notifier.CollectionChanged += handler;
            }
        }

        (watched, found) = (found, watched);
        found.Clear();
    }

    public void Clear()
    {
        found.Clear();
        Apply();
    }
}

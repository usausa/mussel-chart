namespace MusselChart.Internal;

using System.Collections.Specialized;

internal sealed class ChartSourceWatcher(Func<Func<Task>, Task> invoke, Action render)
{
    private HashSet<INotifyCollectionChanged> watched = new(ReferenceEqualityComparer.Instance);

    private HashSet<INotifyCollectionChanged> found = new(ReferenceEqualityComparer.Instance);

    private int pending;

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
                notifier.CollectionChanged -= OnCollectionChanged;
            }
        }

        foreach (var notifier in found)
        {
            if (!watched.Contains(notifier))
            {
                notifier.CollectionChanged += OnCollectionChanged;
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

    public void Rendered() => Volatile.Write(ref pending, 0);

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (Interlocked.Exchange(ref pending, 1) == 0)
        {
            _ = invoke(RenderAsync);
        }
    }

    private async Task RenderAsync()
    {
        await Task.Yield();
        if (Interlocked.Exchange(ref pending, 0) == 1)
        {
            render();
        }
    }
}

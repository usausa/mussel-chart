namespace MusselChart;

using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

using Microsoft.AspNetCore.Components;

public sealed class ChartSourceTests : BunitContext
{
    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static string Seconds(DateTime time) => ((time.Ticks - DateTime.UnixEpoch.Ticks) / TimeSpan.TicksPerSecond).ToString(CultureInfo.InvariantCulture);

    public ChartSourceTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public Task ChartsDrawWhatIsPushedIntoTheirBuffer()
    {
        var buffer = new ChartRingBuffer<Sample>(10);
        buffer.Push(new Sample(Start, 1));
        var cut = RenderLines(buffer, buffer);

        buffer.Push(new Sample(Start.AddSeconds(1), 2));

        return cut.WaitForAssertionAsync(() => Assert.Equal("0,1 1,2", cut.Find("polyline").GetAttribute("points")));
    }

    [Fact]
    public Task ChartsDrawWhatIsAddedToAnObservableCollection()
    {
        var items = new ObservableCollection<Sample> { new(Start, 1) };
        var cut = RenderLines(items, items);

        items.Add(new Sample(Start.AddSeconds(1), 2));

        return cut.WaitForAssertionAsync(() => Assert.Equal("0,1 1,2", cut.Find("polyline").GetAttribute("points")));
    }

    [Fact]
    public async Task ChartsWatchEachSourceOnceAndStopWhenTheyAreGone()
    {
        var items = new WatchedList<Sample>([new Sample(Start, 1)]);
        RenderLines(items, items);

        Assert.Equal(1, items.Watchers);

        await DisposeComponentsAsync();

        Assert.Equal(0, items.Watchers);
    }

    [Fact]
    public void ChartsMoveTheWatchToNewItems()
    {
        var first = new WatchedList<Sample>([new Sample(Start, 1)]);
        var second = new WatchedList<Sample>([new Sample(Start, 2)]);
        var cut = RenderLines(first, first);

        cut.Render(parameters => parameters
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, second)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value)));

        Assert.Equal(0, first.Watchers);
        Assert.Equal(1, second.Watchers);
    }

    [Fact]
    public Task StatViewsDrawWhatIsPushedIntoTheirTrend()
    {
        var trend = new ChartRingBuffer<double>(10);
        trend.Push(1);
        trend.Push(3);
        var cut = Render<MusselStatView>(parameters => parameters
            .Add(static p => p.Value, 10)
            .Add(static p => p.TrendValues, trend)
            .Add(static p => p.TrendPlacement, ChartTrendPlacement.Background));

        trend.Push(2);

        return cut.WaitForAssertionAsync(() => Assert.Equal("0 0 2 100", cut.Find(".mussel-stat-trend-background").GetAttribute("viewBox")));
    }

    [Fact]
    public Task SparklinesDrawWhatIsPushedIntoTheirValues()
    {
        var values = new ChartRingBuffer<double>(10);
        values.Push(1);
        values.Push(3);
        var cut = Render<MusselSparklineView>(parameters => parameters.Add(static p => p.Values, values));
        var before = cut.Find(".mussel-sparkline-line").GetAttribute("points");

        values.Push(2);

        return cut.WaitForAssertionAsync(() => Assert.NotEqual(before, cut.Find(".mussel-sparkline-line").GetAttribute("points")));
    }

    [Fact]
    public Task BarGaugesDrawWhatIsAddedToTheirItems()
    {
        var items = new ObservableCollection<ChartEntry> { new("A", 30, null, 100) };
        var cut = Render<MusselBarGaugeView>(parameters => parameters.Add(static p => p.Items, items));

        items.Add(new ChartEntry("B", 50, null, 100));

        return cut.WaitForAssertionAsync(() => Assert.Equal(2, cut.FindAll(".mussel-gauge-label").Count));
    }

    [Fact]
    public async Task WidgetsStopWatchingWhenTheyAreGone()
    {
        var trend = new WatchedList<double>([1, 2]);
        var entries = new WatchedList<ChartEntry>([new ChartEntry("A", 30, null, 100)]);
        var line = new WatchedList<double>([1, 2]);
        Render<MusselStatView>(parameters => parameters.Add(static p => p.Value, 10).Add(static p => p.TrendValues, trend));
        Render<MusselBarGaugeView>(parameters => parameters.Add(static p => p.Items, entries));
        Render<MusselSparklineView>(parameters => parameters.Add(static p => p.Values, line));

        Assert.Equal(1, trend.Watchers);
        Assert.Equal(1, entries.Watchers);
        Assert.Equal(1, line.Watchers);

        await DisposeComponentsAsync();

        Assert.Equal(0, trend.Watchers);
        Assert.Equal(0, entries.Watchers);
        Assert.Equal(0, line.Watchers);
    }

    [Fact]
    public Task PieChartsDrawWhatIsAddedToTheirItems()
    {
        var items = new ObservableCollection<ChartEntry> { new("A", 50), new("B", 30) };
        var cut = Render<MusselPieChartView>(parameters => parameters.Add(static p => p.Items, items));

        items.Add(new ChartEntry("C", 20));

        return cut.WaitForAssertionAsync(() => Assert.Equal(3, cut.FindAll(".mussel-slice").Count));
    }

    [Fact]
    public Task RadarChartsDrawWhatIsAddedToTheirSeries()
    {
        var items = new ObservableCollection<ChartEntry> { new("A", 1), new("B", 2), new("C", 3) };
        var cut = Render<MusselRadarChartView>(parameters => parameters
            .AddChildContent<RadarSeries<ChartEntry>>(series => series.Add(static p => p.Items, items)));

        items.Add(new ChartEntry("D", 4));

        return cut.WaitForAssertionAsync(() => Assert.Equal(4, cut.FindAll(".mussel-radar-axis").Count));
    }

    [Fact]
    public Task HeatmapsDrawWhatIsAddedToTheirItems()
    {
        var items = new ObservableCollection<HeatItem> { new("A", "1", 1) };
        var cut = Render<MusselHeatmapView<HeatItem>>(parameters => parameters
            .Add(static p => p.Items, items)
            .Add(static p => p.Row, static x => x.Row)
            .Add(static p => p.Column, static x => x.Column)
            .Add(static p => p.Value, static x => x.Value));

        items.Add(new HeatItem("A", "2", 2));

        return cut.WaitForAssertionAsync(() => Assert.Equal(2, cut.FindAll(".mussel-heat-cells rect").Count));
    }

    [Fact]
    public Task StateTimelinesDrawWhatIsAddedToTheirItems()
    {
        var items = new ObservableCollection<StateItem> { new("A", Start, "x") };
        var cut = Render<MusselStateTimelineView<StateItem>>(parameters => parameters
            .Add(static p => p.Items, items)
            .Add(static p => p.Row, static x => x.Row)
            .Add(static p => p.Start, static x => x.Start)
            .Add(static p => p.State, static x => x.State)
            .Add(static p => p.Maximum, Start.AddHours(2))
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc));

        items.Add(new StateItem("A", Start.AddHours(1), "y"));

        return cut.WaitForAssertionAsync(() => Assert.Equal(2, cut.FindAll(".mussel-timeline-bands rect").Count));
    }

    [Fact]
    public Task RadialBarsDrawWhatIsAddedToTheirItems()
    {
        var items = new ObservableCollection<ChartEntry> { new("A", 50) };
        var cut = Render<MusselRadialBarView>(parameters => parameters.Add(static p => p.Items, items));

        items.Add(new ChartEntry("B", 25));

        return cut.WaitForAssertionAsync(() => Assert.Equal(2, cut.FindAll(".mussel-ring").Count));
    }

    [Fact]
    public async Task PushesInARowDrawOnce()
    {
        var buffer = new ChartRingBuffer<Sample>(100);
        buffer.Push(new Sample(Start, 0));
        var cut = RenderLines(buffer, buffer);
        var renders = cut.RenderCount;
        await cut.InvokeAsync(() => buffer.Push(new Sample(Start.AddSeconds(1), 1)));
        await cut.InvokeAsync(static () => { });
        var single = cut.RenderCount - renders;
        renders = cut.RenderCount;

        await cut.InvokeAsync(() =>
        {
            for (var i = 2; i <= 51; i++)
            {
                buffer.Push(new Sample(Start.AddSeconds(i), i));
            }
        });
        await cut.InvokeAsync(static () => { });

        Assert.Equal(single, cut.RenderCount - renders);
        Assert.Contains(cut.FindAll("polyline"), static x => x.GetAttribute("points")!.EndsWith(" 51,51", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AFullBufferWiderThanTheRebaseKeepsItsTimeBaseOnAPush()
    {
        var buffer = new ChartRingBuffer<Sample>(2000);
        for (var i = 0; i < 2000; i++)
        {
            buffer.Push(new Sample(Start.AddMinutes(i), i));
        }

        var cut = RenderLines(buffer, buffer);
        var before = cut.Find("svg.mussel-chart-svg").GetAttribute("data-base");

        await cut.InvokeAsync(() => buffer.Push(new Sample(Start.AddMinutes(2000), 2000)));
        await cut.InvokeAsync(static () => { });

        Assert.Equal(before, cut.Find("svg.mussel-chart-svg").GetAttribute("data-base"));
        Assert.Contains(cut.FindAll("polyline"), static x => x.GetAttribute("points")!.EndsWith(" 120000,2000", StringComparison.Ordinal));
    }

    [Fact]
    public async Task APushPastTheRebaseMovesTheTimeBaseToTheOldestPoint()
    {
        var buffer = new ChartRingBuffer<Sample>(2);
        buffer.Push(new Sample(Start, 0));
        buffer.Push(new Sample(Start.AddDays(2), 1));
        var cut = RenderLines(buffer, buffer);

        await cut.InvokeAsync(() => buffer.Push(new Sample(Start.AddDays(2).AddSeconds(1), 2)));
        await cut.InvokeAsync(static () => { });

        Assert.Equal(Seconds(Start.AddDays(2)), cut.Find("svg.mussel-chart-svg").GetAttribute("data-base"));
    }

    [Fact]
    public void TheTimeBaseOfPointsOutOfOrderComesFromTheOldestKeptPoint()
    {
        var buffer = new ChartRingBuffer<Sample>(100);
        buffer.Push(new Sample(Start.AddHours(30), 0));
        buffer.Push(new Sample(Start, 1));
        buffer.Push(new Sample(Start.AddMinutes(1), 2));

        var cut = RenderLines(buffer, buffer);

        Assert.Equal(Seconds(Start.AddHours(30)), cut.Find("svg.mussel-chart-svg").GetAttribute("data-base"));
    }

    [Fact]
    public async Task APushDrawnWithTheParentDrawsOnce()
    {
        var points = new ChartRingBuffer<Sample>(10);
        var trend = new ChartRingBuffer<double>(10);
        var values = new ChartRingBuffer<double>(10);
        var entries = new ChartRingBuffer<ChartEntry>(10);

        await DrawsOnceWithTheParentAsync(RenderLines(points, points), () => points.Push(new Sample(Start, 1)));
        await DrawsOnceWithTheParentAsync(Render<MusselStatView>(parameters => parameters.Add(static p => p.TrendValues, trend)), () => trend.Push(1));
        await DrawsOnceWithTheParentAsync(Render<MusselSparklineView>(parameters => parameters.Add(static p => p.Values, values)), () => values.Push(1));
        await DrawsOnceWithTheParentAsync(Render<MusselBarGaugeView>(parameters => parameters.Add(static p => p.Items, entries)), () => entries.Push(new ChartEntry("A", 1)));
    }

    private static async Task DrawsOnceWithTheParentAsync<TComponent>(IRenderedComponent<TComponent> cut, Action push)
        where TComponent : IComponent
    {
        var renders = cut.RenderCount;
        await cut.InvokeAsync(() => cut.Render()).ConfigureAwait(true);
        await cut.InvokeAsync(static () => { }).ConfigureAwait(true);
        var alone = cut.RenderCount - renders;
        renders = cut.RenderCount;

        await cut.InvokeAsync(() =>
        {
            push();
            cut.Render();
        }).ConfigureAwait(true);
        await cut.InvokeAsync(static () => { }).ConfigureAwait(true);

        Assert.Equal(alone, cut.RenderCount - renders);
    }

    private IRenderedComponent<MusselChartView> RenderLines(IReadOnlyList<Sample> first, IReadOnlyList<Sample> second) =>
        Render<MusselChartView>(parameters => parameters
            .Add(static p => p.TimeZone, TimeZoneInfo.Utc)
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, first)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Value))
            .AddChildContent<LineSeries<Sample>>(series => series
                .Add(static p => p.Items, second)
                .Add(static p => p.X, static x => x.Time)
                .Add(static p => p.Y, static x => x.Other)));

    private sealed class WatchedList<T>(IReadOnlyList<T> items) : IReadOnlyList<T>, INotifyCollectionChanged
    {
        private NotifyCollectionChangedEventHandler? handlers;

        public event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add => handlers += value;
            remove => handlers -= value;
        }

        public int Watchers => handlers?.GetInvocationList().Length ?? 0;

        public int Count => items.Count;

        public T this[int index] => items[index];

        public IEnumerator<T> GetEnumerator() => items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

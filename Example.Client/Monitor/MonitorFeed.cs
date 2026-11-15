namespace Example.Client.Monitor;

public sealed class MonitorFeed : IDisposable
{
    private const int HistoryCount = 190;

    private const int TrendCount = 30;

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    private readonly Lock sync = new();

    private readonly TimeProvider timeProvider;

    private readonly SystemMonitor monitor = new();

    private EventHandler? updated;

    private ITimer? timer;

    public event EventHandler? Updated
    {
        add
        {
            lock (sync)
            {
                if (timer is null)
                {
                    Fill();
                    timer = timeProvider.CreateTimer(_ => Tick(), null, Interval, Interval);
                }

                updated += value;
            }
        }
        remove
        {
            lock (sync)
            {
                updated -= value;
                if (updated is null)
                {
                    timer?.Dispose();
                    timer = null;
                }
            }
        }
    }

    public ChartRingBuffer<MonitorSample> Samples { get; } = new(HistoryCount);

    public ChartRingBuffer<double> CpuTrend { get; } = new(TrendCount);

    public ChartRingBuffer<double> MemoryTrend { get; } = new(TrendCount);

    public ChartRingBuffer<double> TemperatureTrend { get; } = new(TrendCount);

    public ChartRingBuffer<double> NetworkTrend { get; } = new(TrendCount);

    public IReadOnlyList<ChartEntry> Disks { get; } = SystemMonitor.LoadDisks();

    public double Cpu { get; private set; }

    public double Memory { get; private set; }

    public double Temperature { get; private set; }

    public double Network { get; private set; }

    public MonitorFeed(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;
    }

    public void Dispose()
    {
        lock (sync)
        {
            timer?.Dispose();
            timer = null;
        }
    }

    private void Fill()
    {
        Samples.Clear();
        CpuTrend.Clear();
        MemoryTrend.Clear();
        TemperatureTrend.Clear();
        NetworkTrend.Clear();

        var time = timeProvider.GetUtcNow().UtcDateTime - (Interval * (HistoryCount - 1));
        for (var i = 0; i < HistoryCount; i++)
        {
            Add(monitor.Next(time));
            time += Interval;
        }
    }

    private void Tick()
    {
        lock (sync)
        {
            if (timer is null)
            {
                return;
            }

            Add(monitor.Next(timeProvider.GetUtcNow().UtcDateTime));
        }

        updated?.Invoke(this, EventArgs.Empty);
    }

    private void Add(MonitorSample sample)
    {
        Samples.Push(sample);

        Cpu = monitor.Cpu;
        Memory = monitor.Memory;
        Temperature = monitor.Temperature;
        Network = monitor.Network;

        CpuTrend.Push(Cpu);
        MemoryTrend.Push(Memory);
        TemperatureTrend.Push(Temperature);
        NetworkTrend.Push(Network);
    }
}

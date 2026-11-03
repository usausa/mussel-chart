namespace Example.Monitor;

public sealed class MonitorFeed : BackgroundService
{
    private const int HistoryCount = 190;

    private const int TrendCount = 30;

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    private readonly TimeProvider timeProvider;

    private readonly SystemMonitor monitor = new();

    public event EventHandler? Updated;

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

        var time = timeProvider.GetUtcNow().UtcDateTime - (Interval * (HistoryCount - 1));
        for (var i = 0; i < HistoryCount; i++)
        {
            Add(monitor.Next(time));
            time += Interval;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                Add(monitor.Next(timeProvider.GetUtcNow().UtcDateTime));
                Updated?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException)
        {
        }
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

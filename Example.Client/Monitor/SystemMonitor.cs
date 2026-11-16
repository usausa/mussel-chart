namespace Example.Client.Monitor;

public sealed class SystemMonitor
{
    private const int LoadCount = 4;

    private static readonly double[] LoadTargets = [32, 24, 38, 20];

    private readonly double[] levels = [28, 21, 34, 17];

    private readonly double[] loads = new double[LoadCount];

    private double receiveLevel = 220;

    private double sendLevel = 90;

    private double receive;

    private double send;

    private uint noise = 2463534242;

    public double Cpu { get; private set; }

    public double Memory { get; private set; } = 53;

    public double Temperature { get; private set; } = 54;

    public double Network => receive + send;

    public MonitorSample Next(DateTime time)
    {
        for (var i = 0; i < LoadCount; i++)
        {
            levels[i] = Math.Clamp(levels[i] + (NextNoise() * 3) + ((LoadTargets[i] - levels[i]) * 0.05), 8, 75);
            loads[i] = Math.Clamp(levels[i] + (NextNoise() * 6) + ((NextNoise() > 0.9) ? 20 : 0), 1, 99);
        }

        Cpu = loads.Average();
        Memory = Math.Clamp(Memory + (NextNoise() * 0.4) + ((55 - Memory) * 0.02), 38, 72);
        Temperature = Math.Clamp(Temperature + ((36 + (Cpu * 0.5) - Temperature) * 0.2) + (NextNoise() * 0.8), 40, 92);
        receiveLevel = Math.Clamp(receiveLevel + (NextNoise() * 40) + ((260 - receiveLevel) * 0.08), 60, 900);
        sendLevel = Math.Clamp(sendLevel + (NextNoise() * 15) + ((100 - sendLevel) * 0.08), 20, 400);
        receive = Math.Clamp(receiveLevel + (NextNoise() * 80) + ((NextNoise() > 0.85) ? 350 : 0), 5, 1200);
        send = Math.Clamp(sendLevel + (NextNoise() * 30) + ((NextNoise() > 0.9) ? 120 : 0), 2, 600);

        return new MonitorSample
        {
            Time = time,
            Core1 = loads[0],
            Core2 = loads[1],
            Core3 = loads[2],
            Core4 = loads[3],
            Receive = receive,
            Send = send,
            Queue = Math.Round(Math.Max(0, (Cpu / 15) + (NextNoise() * 2)))
        };
    }

    public static IReadOnlyList<ChartEntry> LoadDisks() =>
    [
        new("C: System", 57, null, 100),
        new("D: Data", 23, null, 100),
        new("E: Work", 67, null, 100),
        new("F: Backup", 82, null, 100)
    ];

    private double NextNoise()
    {
        noise ^= noise << 13;
        noise ^= noise >> 17;
        noise ^= noise << 5;
        return ((noise / (double)UInt32.MaxValue) * 2) - 1;
    }
}

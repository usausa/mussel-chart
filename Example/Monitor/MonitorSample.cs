namespace Example.Monitor;

public sealed class MonitorSample
{
    public required DateTime Time { get; init; }

    public required double Core1 { get; init; }

    public required double Core2 { get; init; }

    public required double Core3 { get; init; }

    public required double Core4 { get; init; }

    public required double Receive { get; init; }

    public required double Send { get; init; }

    public required double Queue { get; init; }
}

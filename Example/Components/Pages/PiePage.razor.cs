namespace Example.Components.Pages;

public sealed partial class PiePage
{
    private static readonly ChartEntry[] Expenses =
    [
        new("Housing", 31000),
        new("Utilities", 18000),
        new("Telecom", 34000),
        new("Leisure", 20000),
        new("Education", 37000),
        new("Transport", 12000),
        new("Insurance", 8000),
        new("Other", 4000)
    ];

    private static readonly ChartEntry[] Traffic =
    [
        new("Organic", 5400),
        new("Direct", 3100, "#E0B400"),
        new("Referral", 1700)
    ];

    private static readonly ChartEntry[] Empty = [];
}

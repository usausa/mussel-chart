namespace Example.Client.Components.Pages;

using Example.Client.Models;

using Microsoft.AspNetCore.Components;

public sealed partial class MonitorPage : IDisposable
{
    private static readonly ChartThreshold[] DiskThresholds =
    [
        new() { Level = ChartThresholdLevel.Good },
        new() { Value = 70, Level = ChartThresholdLevel.Warning },
        new() { Value = 85, Level = ChartThresholdLevel.Critical }
    ];

    private bool wide = true;

    [Inject]
    private MonitorFeed Feed { get; set; } = default!;

    private TimeSpan Window => wide ? TimeSpan.FromMinutes(3) : TimeSpan.FromSeconds(45);

    protected override void OnInitialized()
    {
        Feed.Updated += OnUpdated;
    }

    public void Dispose()
    {
        Feed.Updated -= OnUpdated;
    }

    private void OnUpdated(object? sender, EventArgs e) => _ = InvokeAsync(StateHasChanged);

    private void ToggleWindow() => wide = !wide;
}

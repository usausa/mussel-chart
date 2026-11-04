namespace Example.Components.Pages;

public sealed partial class MonitorPage
{
    private static readonly ChartThreshold[] DiskThresholds =
    [
        new() { Level = ChartThresholdLevel.Good },
        new() { Value = 70, Level = ChartThresholdLevel.Warning },
        new() { Value = 85, Level = ChartThresholdLevel.Critical }
    ];

    private bool wide = true;

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

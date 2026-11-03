namespace MusselChart;

using System.Globalization;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

public sealed partial class MusselChartView : IAsyncDisposable
{
    private const string ModulePath = "./_content/MusselChart/musselchart.js";

    private const double DefaultWidth = 640;

    private readonly string clipId = "mussel-" + Guid.NewGuid().ToString("N")[..12];

    private readonly List<ChartSeries> seriesList = [];

    private readonly HashSet<ChartSeries> hiddenSeries = new(ReferenceEqualityComparer.Instance);

    private ElementReference element;

    private IJSObjectReference? module;

    private DotNetObjectReference<MusselChartView>? reference;

    private DateTimeAxis? xAxis;

    private NumericAxis? yAxis;

    private double width = DefaultWidth;

    private bool disposed;

    private CultureInfo culture = CultureInfo.CurrentCulture;

    private TimeZoneInfo zone = TimeZoneInfo.Local;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public double Height { get; set; } = 240;

    [Parameter]
    public IReadOnlyList<string>? Palette { get; set; }

    [Parameter]
    public bool? LegendVisible { get; set; }

    [Parameter]
    public ChartLegendMode LegendMode { get; set; }

    [Parameter]
    public ChartLegendValues LegendValues { get; set; }

    [Parameter]
    public bool LegendToggleEnabled { get; set; }

    [Parameter]
    public ChartTrackballMode TrackballMode { get; set; } = ChartTrackballMode.Hover;

    [Parameter]
    public bool TooltipVisible { get; set; } = true;

    [Parameter]
    public string? TrackingGroup { get; set; }

    [Parameter]
    public ChartTrackingGroupMode TrackingGroupMode { get; set; }

    [Parameter]
    public IReadOnlyList<ChartThreshold>? Thresholds { get; set; }

    [Parameter]
    public ChartThresholdsMode ThresholdsMode { get; set; }

    [Parameter]
    public CultureInfo? Culture { get; set; }

    [Parameter]
    public TimeZoneInfo? TimeZone { get; set; }

    [Parameter]
    public string? EmptyText { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private string GroupModeText => TrackingGroupMode == ChartTrackingGroupMode.Tooltip ? "tooltip" : "crosshair";

    private string TrackballText => TrackballMode == ChartTrackballMode.Hover ? "hover" : "none";

    private string TooltipText => TooltipVisible ? "true" : "false";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        try
        {
            module = await Runtime.InvokeAsync<IJSObjectReference>("import", ModulePath).ConfigureAwait(true);
            if (disposed)
            {
                return;
            }

            reference = DotNetObjectReference.Create(this);
            await module.InvokeVoidAsync("attach", element, reference).ConfigureAwait(true);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        if (module is not null)
        {
            try
            {
                await module.InvokeVoidAsync("detach", element).ConfigureAwait(true);
                await module.DisposeAsync().ConfigureAwait(true);
            }
            catch (JSDisconnectedException)
            {
            }
        }

        reference?.Dispose();
    }

    [JSInvokable]
    public void OnResize(double value)
    {
        var rounded = Math.Floor(value);
        if ((rounded > 0) && (Math.Abs(rounded - width) >= 1))
        {
            width = rounded;
            StateHasChanged();
        }
    }

    internal void AddSeries(ChartSeries series)
    {
        seriesList.Add(series);
        rebuildRequired = true;
        RequestRender();
    }

    internal void RemoveSeries(ChartSeries series)
    {
        seriesList.Remove(series);
        states.Remove(series);
        hiddenSeries.Remove(series);
        rebuildRequired = true;
        RequestRender();
    }

    internal void InvalidateSeries()
    {
        rebuildRequired = true;
        RequestRender();
    }

    internal void SetAxis(ChartAxis axis)
    {
        switch (axis)
        {
            case DateTimeAxis dateTimeAxis:
                xAxis = dateTimeAxis;
                break;
            case NumericAxis numericAxis:
                yAxis = numericAxis;
                break;
        }

        RequestRender();
    }

    internal void RemoveAxis(ChartAxis axis)
    {
        if (ReferenceEquals(xAxis, axis))
        {
            xAxis = null;
        }

        if (ReferenceEquals(yAxis, axis))
        {
            yAxis = null;
        }

        RequestRender();
    }

    private void ToggleSeries(ChartSeries series)
    {
        if (!LegendToggleEnabled)
        {
            return;
        }

        if (!hiddenSeries.Remove(series))
        {
            var visible = seriesList.Count(x => x.IsVisible && !hiddenSeries.Contains(x));
            if (visible <= 1)
            {
                return;
            }

            hiddenSeries.Add(series);
        }

        rebuildRequired = true;
    }

    private void RequestRender()
    {
        if (!disposed)
        {
            StateHasChanged();
        }
    }

    private void Prepare()
    {
        culture = Culture ?? CultureInfo.CurrentCulture;
        zone = TimeZone ?? TimeZoneInfo.Local;
        SyncData();
        Arrange();
    }
}

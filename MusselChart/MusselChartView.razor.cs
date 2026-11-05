namespace MusselChart;

using System.Globalization;

using Microsoft.AspNetCore.Components;

public sealed partial class MusselChartView
{
    private readonly string clipId = "mussel-" + Guid.NewGuid().ToString("N")[..12];

    private readonly List<ChartSeries> seriesList = [];

    private readonly HashSet<ChartSeries> hiddenSeries = new(ReferenceEqualityComparer.Instance);

    private ElementReference element;

    private ChartAxis? xAxis;

    private NumericAxis? yAxis;

    private CultureInfo culture = CultureInfo.CurrentCulture;

    private TimeZoneInfo zone = TimeZoneInfo.Local;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public IReadOnlyList<string>? Palette { get; set; }

    [Parameter]
    public ChartOrientation Orientation { get; set; }

    [Parameter]
    public ChartStackMode StackMode { get; set; }

    [Parameter]
    public bool? LegendVisible { get; set; }

    [Parameter]
    public ChartLegendMode LegendMode { get; set; }

    [Parameter]
    public ChartLegendValues LegendValues { get; set; }

    [Parameter]
    public bool LegendToggleEnabled { get; set; }

    [Parameter]
    public string? TrackingGroup { get; set; }

    [Parameter]
    public ChartTrackingGroupMode TrackingGroupMode { get; set; }

    [Parameter]
    public IReadOnlyList<ChartThreshold>? Thresholds { get; set; }

    [Parameter]
    public ChartThresholdsMode ThresholdsMode { get; set; }

    [Parameter]
    public TimeZoneInfo? TimeZone { get; set; }

    private string GroupModeText => TrackingGroupMode == ChartTrackingGroupMode.Tooltip ? "tooltip" : "crosshair";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await AttachAsync(element).ConfigureAwait(true);
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
        if (axis is NumericAxis numericAxis and not NumericXAxis)
        {
            yAxis = numericAxis;
        }
        else
        {
            xAxis = axis;
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

    private void Prepare()
    {
        culture = Culture ?? CultureInfo.CurrentCulture;
        zone = TimeZone ?? TimeZoneInfo.Local;
        SyncData();
        Arrange();
    }
}

namespace MusselChart;

using Microsoft.AspNetCore.Components;

using MusselChart.Internal;

public abstract class RadarSeriesBase : ComponentBase, IDisposable
{
    private SeriesOptions? options;

    [CascadingParameter]
    internal MusselRadarChartView? Chart { get; set; }

    [Parameter]
    public string? Title { get; set; }

    [Parameter]
    public string? Color { get; set; }

    [Parameter]
    public bool IsVisible { get; set; } = true;

    [Parameter]
    public bool LegendVisible { get; set; } = true;

    [Parameter]
    public string? ValueFormat { get; set; }

    [Parameter]
    public Func<double, string>? ValueFormatter { get; set; }

    [Parameter]
    public double Thickness { get; set; } = 2;

    [Parameter]
    public double FillOpacity { get; set; } = 0.15;

    internal abstract object? Source { get; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Chart?.RemoveSeries(this);
        }
    }

    protected override void OnInitialized()
    {
        Chart?.AddSeries(this);
    }

    protected override void OnParametersSet()
    {
        var current = new SeriesOptions(Title, Color, IsVisible, LegendVisible, ValueFormat, ValueFormatter, Source, ChartXKind.Category, Thickness, FillOpacity, null, false);
        if ((options is not null) && !current.Equals(options))
        {
            Chart?.InvalidateSeries();
        }

        options = current;
    }

    internal abstract void ReadPoints(List<RadarPoint> destination);
}

public sealed class RadarSeries<TItem> : RadarSeriesBase
{
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<TItem>? Items { get; set; }

    [Parameter]
    public Func<TItem, string?>? Category { get; set; }

    [Parameter]
    public Func<TItem, double>? Y { get; set; }

    internal override object? Source => Items;

    internal override void ReadPoints(List<RadarPoint> destination)
    {
        if (Items is null)
        {
            return;
        }

        // ReSharper disable once ForCanBeConvertedToForeach
        for (var i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            if (item is ChartEntry entry)
            {
                destination.Add(new RadarPoint(Category is null ? entry.Label : Category(item), Y?.Invoke(item) ?? entry.Value));
            }
            else if (Y is not null)
            {
                destination.Add(new RadarPoint(Category?.Invoke(item), Y(item)));
            }
        }
    }
}

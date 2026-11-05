namespace MusselChart;

using Microsoft.AspNetCore.Components;

using MusselChart.Internal;

public abstract class ChartSeries : ComponentBase, IDisposable
{
    private SeriesOptions? options;

    [CascadingParameter]
    internal MusselChartView? Chart { get; set; }

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

    internal abstract ChartSeriesKind Kind { get; }

    internal abstract ChartXKind XKind { get; }

    internal virtual string? StackKey => null;

    internal virtual object? Source => null;

    internal SeriesOptions Options => options ??= CreateOptions();

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
        var current = CreateOptions();
        if (current.Equals(options))
        {
            return;
        }

        if (options is not null)
        {
            Chart?.InvalidateSeries();
        }

        options = current;
    }

    internal virtual SeriesOptions CreateOptions() => new(Title, Color, IsVisible, LegendVisible, ValueFormat, ValueFormatter, Source, XKind, 0, 0, null, false);

    internal abstract ChartBufferRead ReadPoints(long afterSequence, int afterGeneration, TimeZoneInfo zone, List<ChartPointData> destination);
}

public abstract class XySeries<TItem> : ChartSeries
{
    private readonly PointReader<TItem> reader = new();

    [Parameter]
    [EditorRequired]
    public IReadOnlyList<TItem>? Items { get; set; }

    [Parameter]
    public Func<TItem, DateTime>? X { get; set; }

    [Parameter]
    public Func<TItem, double>? XValue { get; set; }

    [Parameter]
    public Func<TItem, string?>? Category { get; set; }

    [Parameter]
    [EditorRequired]
    public Func<TItem, double>? Y { get; set; }

    internal override ChartXKind XKind => PointReader<TItem>.GetKind(XValue, Category);

    internal override object? Source => Items;

    internal override ChartBufferRead ReadPoints(long afterSequence, int afterGeneration, TimeZoneInfo zone, List<ChartPointData> destination)
    {
        if ((Items is null) || (Y is null) || ((X is null) && (XValue is null) && (Category is null)))
        {
            return default;
        }

        var read = reader.Fill(Items, afterSequence, afterGeneration);
        foreach (var item in reader.Items)
        {
            destination.Add(PointReader<TItem>.CreatePoint(item, X, XValue, Category, zone, Y(item), Double.NaN, ReadSize(item)));
        }

        reader.Clear();
        return read;
    }

    internal virtual double ReadSize(TItem item) => Double.NaN;
}

public class LineSeries<TItem> : XySeries<TItem>
{
    [Parameter]
    public double Thickness { get; set; } = 2;

    [Parameter]
    public bool LiveIndicatorVisible { get; set; }

    internal override ChartSeriesKind Kind => ChartSeriesKind.Line;

    internal override SeriesOptions CreateOptions() => base.CreateOptions() with { Thickness = Thickness, Live = LiveIndicatorVisible };
}

public sealed class AreaSeries<TItem> : LineSeries<TItem>
{
    [Parameter]
    public double FillOpacity { get; set; } = 0.1;

    [Parameter]
    public string? StackGroup { get; set; }

    internal override ChartSeriesKind Kind => ChartSeriesKind.Area;

    internal override string? StackKey => String.IsNullOrEmpty(StackGroup) ? null : StackGroup;

    internal override SeriesOptions CreateOptions() => base.CreateOptions() with { Opacity = FillOpacity, Stack = StackGroup };
}

public sealed class BarSeries<TItem> : XySeries<TItem>
{
    [Parameter]
    public double BarWidthRatio { get; set; } = 0.6;

    [Parameter]
    public double MaxBarWidth { get; set; } = 24;

    [Parameter]
    public string? StackGroup { get; set; }

    internal override ChartSeriesKind Kind => ChartSeriesKind.Bar;

    internal override string? StackKey => String.IsNullOrEmpty(StackGroup) ? null : StackGroup;

    internal override SeriesOptions CreateOptions() => base.CreateOptions() with { Thickness = MaxBarWidth, Opacity = BarWidthRatio, Stack = StackGroup };
}

public sealed class RangeBarSeries<TItem> : ChartSeries
{
    private readonly PointReader<TItem> reader = new();

    [Parameter]
    [EditorRequired]
    public IReadOnlyList<TItem>? Items { get; set; }

    [Parameter]
    public Func<TItem, DateTime>? X { get; set; }

    [Parameter]
    public Func<TItem, double>? XValue { get; set; }

    [Parameter]
    public Func<TItem, string?>? Category { get; set; }

    [Parameter]
    [EditorRequired]
    public Func<TItem, double>? Low { get; set; }

    [Parameter]
    [EditorRequired]
    public Func<TItem, double>? High { get; set; }

    [Parameter]
    public double BarWidthRatio { get; set; } = 0.6;

    [Parameter]
    public double MaxBarWidth { get; set; } = 24;

    internal override ChartSeriesKind Kind => ChartSeriesKind.RangeBar;

    internal override ChartXKind XKind => PointReader<TItem>.GetKind(XValue, Category);

    internal override object? Source => Items;

    internal override SeriesOptions CreateOptions() => base.CreateOptions() with { Thickness = MaxBarWidth, Opacity = BarWidthRatio };

    internal override ChartBufferRead ReadPoints(long afterSequence, int afterGeneration, TimeZoneInfo zone, List<ChartPointData> destination)
    {
        if ((Items is null) || (Low is null) || (High is null) || ((X is null) && (XValue is null) && (Category is null)))
        {
            return default;
        }

        var read = reader.Fill(Items, afterSequence, afterGeneration);
        foreach (var item in reader.Items)
        {
            destination.Add(PointReader<TItem>.CreatePoint(item, X, XValue, Category, zone, High(item), Low(item), Double.NaN));
        }

        reader.Clear();
        return read;
    }
}

public class ScatterSeries<TItem> : XySeries<TItem>
{
    [Parameter]
    public double MarkerSize { get; set; } = 8;

    [Parameter]
    public ChartMarkerShape MarkerShape { get; set; }

    [Parameter]
    public double? FillOpacity { get; set; }

    internal override ChartSeriesKind Kind => ChartSeriesKind.Scatter;

    internal override SeriesOptions CreateOptions() => base.CreateOptions() with { Thickness = MarkerSize, Opacity = FillOpacity ?? 0.5, Shape = MarkerShape };
}

public sealed class BubbleSeries<TItem> : ScatterSeries<TItem>
{
    [Parameter]
    [EditorRequired]
    public Func<TItem, double>? Size { get; set; }

    [Parameter]
    public double MinBubbleSize { get; set; } = 8;

    [Parameter]
    public double MaxBubbleSize { get; set; } = 40;

    [Parameter]
    public bool SizeIncludesZero { get; set; } = true;

    internal override ChartSeriesKind Kind => ChartSeriesKind.Bubble;

    internal override SeriesOptions CreateOptions() => base.CreateOptions() with { MinSize = MinBubbleSize, MaxSize = MaxBubbleSize, SizeIncludesZero = SizeIncludesZero };

    internal override double ReadSize(TItem item) => Size?.Invoke(item) ?? Double.NaN;
}

namespace MusselChart;

using Microsoft.AspNetCore.Components;

public abstract class ChartAxis : ComponentBase, IDisposable
{
    private object? options;

    [CascadingParameter]
    internal MusselChartView? Chart { get; set; }

    [Parameter]
    public bool? IsVisible { get; set; }

    [Parameter]
    public bool LabelsVisible { get; set; } = true;

    [Parameter]
    public string? LabelFormat { get; set; }

    [Parameter]
    public int? MaxLabelCount { get; set; }

    [Parameter]
    public bool? GridLinesVisible { get; set; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Chart?.RemoveAxis(this);
        }
    }

    protected override void OnParametersSet()
    {
        var current = CreateOptions();
        if (!current.Equals(options))
        {
            options = current;
            Chart?.SetAxis(this);
        }
    }

    internal abstract object CreateOptions();
}

public sealed class DateTimeAxis : ChartAxis
{
    [Parameter]
    public DateTime? Minimum { get; set; }

    [Parameter]
    public DateTime? Maximum { get; set; }

    [Parameter]
    public TimeSpan? WindowSize { get; set; }

    [Parameter]
    public Func<DateTime, string>? LabelFormatter { get; set; }

    internal override object CreateOptions() =>
        (IsVisible, LabelsVisible, LabelFormat, MaxLabelCount, GridLinesVisible, Minimum, Maximum, WindowSize, LabelFormatter);
}

public sealed class CategoryAxis : ChartAxis
{
    [Parameter]
    public ChartLabelPlacement LabelPlacement { get; set; }

    [Parameter]
    public int? LabelMaxLength { get; set; }

    [Parameter]
    public Func<string, string>? LabelFormatter { get; set; }

    internal override object CreateOptions() =>
        (IsVisible, LabelsVisible, LabelFormat, MaxLabelCount, GridLinesVisible, LabelPlacement, LabelMaxLength, LabelFormatter);
}

public class NumericAxis : ChartAxis
{
    [Parameter]
    public double? Minimum { get; set; }

    [Parameter]
    public double? Maximum { get; set; }

    [Parameter]
    public double? SoftMinimum { get; set; }

    [Parameter]
    public double? SoftMaximum { get; set; }

    [Parameter]
    public bool? IncludeZero { get; set; }

    [Parameter]
    public bool NiceRange { get; set; } = true;

    [Parameter]
    public Func<double, string>? LabelFormatter { get; set; }

    internal override object CreateOptions() =>
        (IsVisible, LabelsVisible, LabelFormat, MaxLabelCount, GridLinesVisible, Minimum, Maximum, SoftMinimum, SoftMaximum, IncludeZero, NiceRange, LabelFormatter);
}

public sealed class NumericXAxis : NumericAxis
{
}

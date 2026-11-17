namespace MusselChart;

using Microsoft.AspNetCore.Components;

using MusselChart.Internal;

public sealed partial class MusselSparklineView : IDisposable
{
    private readonly ChartSourceWatcher sources;

    private bool disposed;

    public MusselSparklineView()
    {
        sources = new ChartSourceWatcher(InvokeAsync, Refresh);
    }

    [Parameter]
    public IEnumerable<double>? Values { get; set; }

    [Parameter]
    public ChartSparklineKind Kind { get; set; }

    [Parameter]
    public string? Color { get; set; }

    [Parameter]
    public string? NegativeColor { get; set; }

    [Parameter]
    public IReadOnlyList<string>? Palette { get; set; }

    [Parameter]
    public double Thickness { get; set; } = 2;

    [Parameter]
    public double? Minimum { get; set; }

    [Parameter]
    public double? Maximum { get; set; }

    [Parameter]
    public double Height { get; set; } = 32;

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    public void Dispose()
    {
        disposed = true;
        sources.Clear();
    }

    protected override bool ShouldRender()
    {
        sources.Rendered();
        return true;
    }

    private void Refresh()
    {
        if (!disposed)
        {
            StateHasChanged();
        }
    }
}

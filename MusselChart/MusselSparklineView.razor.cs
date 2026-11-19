namespace MusselChart;

using Microsoft.AspNetCore.Components;

using MusselChart.Internal;

public sealed partial class MusselSparklineView : IDisposable
{
    private readonly ChartSourceWatcher sources;

    private readonly List<double> copied = [];

    private TrendPath path = TrendPath.Empty;

    private bool bars;

    private string color = string.Empty;

    private string? negative;

    public MusselSparklineView()
    {
        sources = new ChartSourceWatcher(InvokeAsync, StateHasChanged);
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

    public void Dispose() => sources.Clear();

    private void Prepare()
    {
        sources.Add(Values);
        sources.Apply();
        bars = Kind is ChartSparklineKind.Bar or ChartSparklineKind.WinLoss;
        path = Kind == ChartSparklineKind.WinLoss ? TrendPath.CreateWinLoss(Values, copied, Height) : TrendPath.Create(Values, copied, Minimum, Maximum, bars, bars ? 0 : 4);
        color = Color ?? ChartColor.At(Palette ?? ChartPalettes.DefaultLight, 0);
        negative = NegativeColor ?? (Kind == ChartSparklineKind.WinLoss ? null : color);
    }

    protected override bool ShouldRender()
    {
        sources.Rendered();
        return true;
    }
}

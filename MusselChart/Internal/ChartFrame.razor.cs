namespace MusselChart.Internal;

using Microsoft.AspNetCore.Components;

public sealed partial class ChartFrame
{
    private ElementReference plot;

    [Parameter]
    [EditorRequired]
    public ChartViewBase Owner { get; set; } = default!;

    [Parameter]
    public string? ViewClass { get; set; }

    [Parameter]
    public IReadOnlyDictionary<string, object>? Attributes { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public IReadOnlyList<ChartLegendItem>? LegendItems { get; set; }

    [Parameter]
    public ChartLegendPosition LegendPosition { get; set; } = ChartLegendPosition.Bottom;

    [Parameter]
    public bool LegendToggleEnabled { get; set; }

    [Parameter]
    public EventCallback<string> OnLegendClick { get; set; }

    private string RootClass
    {
        get
        {
            var position = LegendPosition switch
            {
                ChartLegendPosition.Top => " mussel-view-legend-top",
                ChartLegendPosition.Left => " mussel-view-legend-left",
                ChartLegendPosition.Right => " mussel-view-legend-right",
                _ => string.Empty
            };
            var toggle = LegendToggleEnabled ? " mussel-view-toggle" : string.Empty;
            return $"mussel-view {ViewClass}{position}{toggle}";
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await Owner.AttachAsync(plot).ConfigureAwait(true);
        }
    }
}

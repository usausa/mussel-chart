namespace MusselChart.Internal;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

public sealed class ChartChildren : ComponentBase
{
    private int rendered;

    [Parameter]
    public int Version { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    protected override bool ShouldRender() => Version != rendered;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        rendered = Version;
        builder.AddContent(0, ChildContent);
    }
}

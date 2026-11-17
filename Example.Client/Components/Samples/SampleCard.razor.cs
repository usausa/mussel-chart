namespace Example.Client.Components.Samples;

using Microsoft.AspNetCore.Components;

public sealed partial class SampleCard
{
    [Parameter]
    public string? Title { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}

namespace MusselChart;

using System.Globalization;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

public abstract class ChartViewBase : ComponentBase, IAsyncDisposable
{
    private const string ModulePath = "./_content/MusselChart/MusselChart.js";

    private const double DefaultWidth = 640;

    private IJSObjectReference? module;

    private DotNetObjectReference<ChartViewBase>? reference;

    private ElementReference attached;

    [Inject]
    private IJSRuntime Runtime { get; set; } = default!;

    [Parameter]
    public double Height { get; set; } = 240;

    [Parameter]
    public ChartTrackballMode TrackballMode { get; set; } = ChartTrackballMode.Hover;

    [Parameter]
    public bool TooltipVisible { get; set; } = true;

    [Parameter]
    public CultureInfo? Culture { get; set; }

    [Parameter]
    public string? EmptyText { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    internal string TrackballText => TrackballMode == ChartTrackballMode.Hover ? "hover" : "none";

    internal string TooltipText => TooltipVisible ? "true" : "false";

    private protected double Width { get; private set; } = DefaultWidth;

    private protected bool Disposed { get; private set; }

    public async ValueTask DisposeAsync()
    {
        Disposed = true;
        if (module is not null)
        {
            try
            {
                await module.InvokeVoidAsync("detach", attached).ConfigureAwait(true);
                await module.DisposeAsync().ConfigureAwait(true);
            }
            catch (JSDisconnectedException)
            {
            }
        }

        reference?.Dispose();
        GC.SuppressFinalize(this);
    }

    [JSInvokable]
    public void OnResize(double value)
    {
        var rounded = Math.Floor(value);
        if ((rounded > 0) && (Math.Abs(rounded - Width) >= 1))
        {
            Width = rounded;
            StateHasChanged();
        }
    }

    internal async Task AttachAsync(ElementReference element)
    {
        try
        {
            module = await Runtime.InvokeAsync<IJSObjectReference>("import", ModulePath).ConfigureAwait(true);
            if (Disposed)
            {
                return;
            }

            attached = element;
            reference = DotNetObjectReference.Create(this);
            await module.InvokeVoidAsync("attach", element, reference).ConfigureAwait(true);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    private protected void RequestRender()
    {
        if (!Disposed)
        {
            StateHasChanged();
        }
    }
}

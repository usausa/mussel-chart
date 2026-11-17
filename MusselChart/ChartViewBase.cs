namespace MusselChart;

using System.Globalization;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using MusselChart.Internal;

public abstract class ChartViewBase : ComponentBase, IAsyncDisposable
{
    private const string ModulePath = "./_content/MusselChart/MusselChart.js";

    private const double DefaultWidth = 640;

    private readonly ChartSourceWatcher sources;

    private IJSObjectReference? module;

    private DotNetObjectReference<ChartScriptBridge>? reference;

    private ElementReference attached;

    private double viewBoxWidth;

    private double viewBoxHeight;

    private bool collecting;

    protected ChartViewBase()
    {
        sources = new ChartSourceWatcher(OnSourceChanged);
    }

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

    internal bool Measured { get; private set; }

    internal bool Prerendering => (AssignedRenderMode is not null) && !RendererInfo.IsInteractive;

    private protected double Width { get; private set; } = DefaultWidth;

    private protected bool Disposed { get; private set; }

    private protected int ChildVersion { get; private set; }

    private protected string ViewBox
    {
        get
        {
            if ((field is null) || !viewBoxWidth.Equals(Width) || !viewBoxHeight.Equals(Height))
            {
                viewBoxWidth = Width;
                viewBoxHeight = Height;
                field = String.Create(CultureInfo.InvariantCulture, $"0 0 {Width:0.#} {Height:0.#}");
            }

            return field;
        }
    }

    public async ValueTask DisposeAsync()
    {
        Disposed = true;
        sources.Clear();
        await ReleaseAsync().ConfigureAwait(true);
        reference?.Dispose();
        GC.SuppressFinalize(this);
    }

    public override Task SetParametersAsync(ParameterView parameters)
    {
        ChildVersion++;
        return base.SetParametersAsync(parameters);
    }

    internal void OnResize(double value)
    {
        var rounded = Math.Floor(value);
        if (Disposed || !(rounded > 0) || (Measured && (Math.Abs(rounded - Width) < 1)))
        {
            return;
        }

        Measured = true;
        Width = rounded;
        StateHasChanged();
    }

    internal async Task AttachAsync(ElementReference element)
    {
        try
        {
            module = await Runtime.InvokeAsync<IJSObjectReference>("import", ModulePath).ConfigureAwait(true);
            if (Disposed)
            {
                await ReleaseAsync().ConfigureAwait(true);
                return;
            }

            attached = element;
            reference = DotNetObjectReference.Create(new ChartScriptBridge(this));
            await module.InvokeVoidAsync("attach", element, reference).ConfigureAwait(true);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    private async Task ReleaseAsync()
    {
        var loaded = module;
        module = null;
        if (loaded is null)
        {
            return;
        }

        try
        {
            if (reference is not null)
            {
                try
                {
                    await loaded.InvokeVoidAsync("detach", attached).ConfigureAwait(true);
                }
                catch (JSException)
                {
                }
            }

            await loaded.DisposeAsync().ConfigureAwait(true);
        }
        catch (JSDisconnectedException)
        {
        }
        catch (JSException)
        {
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
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

    private protected void WatchSource(object? source) => sources.Add(source);

    private protected void ApplySources() => sources.Apply();

    private protected void StartChildren() => collecting = true;

    private protected void EndChildren() => collecting = false;

    private protected void RequestChildRender()
    {
        if (!collecting)
        {
            RequestRender();
        }
    }

    private void OnSourceChanged()
    {
        if (!Disposed)
        {
            _ = InvokeAsync(RequestRender);
        }
    }
}

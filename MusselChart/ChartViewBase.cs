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

    private DotNetObjectReference<ChartScriptBridge>? self;

    private IJSObjectReference? instance;

    private double viewBoxWidth;

    private double viewBoxHeight;

    private bool collecting;

    protected ChartViewBase()
    {
        sources = new ChartSourceWatcher(InvokeAsync, RequestRender);
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

    internal bool IsDisposed { get; private set; }

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
        IsDisposed = true;
        sources.Clear();
        await ReleaseAsync().ConfigureAwait(true);
        self?.Dispose();
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
        if (!(rounded > 0) || (Measured && (Math.Abs(rounded - Width) < 1)))
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
            if (IsDisposed)
            {
                await ReleaseAsync().ConfigureAwait(true);
                return;
            }

            self = DotNetObjectReference.Create(new ChartScriptBridge(this));
            instance = await module.InvokeAsync<IJSObjectReference>("attach", element, self).ConfigureAwait(true);
            if (IsDisposed)
            {
                await ReleaseAsync().ConfigureAwait(true);
            }
        }
        catch (JSDisconnectedException)
        {
        }
    }

    protected override bool ShouldRender()
    {
        sources.Rendered();
        return true;
    }

    private async Task ReleaseAsync()
    {
        var attached = instance;
        var loaded = module;
        instance = null;
        module = null;
        try
        {
            if (attached is not null)
            {
                try
                {
                    await attached.InvokeVoidAsync("dispose").ConfigureAwait(true);
                }
                catch (JSException)
                {
                }

                await attached.DisposeAsync().ConfigureAwait(true);
            }

            if (loaded is not null)
            {
                await loaded.DisposeAsync().ConfigureAwait(true);
            }
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
        if (!IsDisposed)
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
}

namespace MusselChart.Internal;

using Microsoft.JSInterop;

internal sealed class ChartScriptBridge(ChartViewBase view)
{
    [JSInvokable]
    public void OnResize(double value) => view.OnResize(value);
}

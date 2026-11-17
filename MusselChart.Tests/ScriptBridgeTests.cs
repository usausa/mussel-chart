namespace MusselChart;

using System.Reflection;

using Microsoft.JSInterop;

public sealed class ScriptBridgeTests
{
    [Fact]
    public void ScriptEntryPointsStayOutOfThePublicApi()
    {
        var entryPoints = typeof(MusselChartView).Assembly.GetExportedTypes()
            .SelectMany(static x => x.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(static x => x.IsDefined(typeof(JSInvokableAttribute), false))
            .Select(static x => $"{x.DeclaringType!.Name}.{x.Name}");

        Assert.Empty(entryPoints);
    }

    [Fact]
    public void EveryCallFromTheScriptHasAnEntryPoint()
    {
        var script = File.ReadAllText(Path.Combine(TestPaths.Root, "MusselChart", "wwwroot", "MusselChart.js"));
        var names = script.Split("invokeMethodAsync('").Skip(1).Select(static x => x[..x.IndexOf('\'', StringComparison.Ordinal)]).Distinct().ToList();
        var entryPoints = typeof(MusselChartView).Assembly.GetTypes()
            .SelectMany(static x => x.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(static x => x.IsDefined(typeof(JSInvokableAttribute), false))
            .Select(static x => x.Name)
            .ToList();

        Assert.NotEmpty(names);
        Assert.All(names, name => Assert.Contains(name, entryPoints));
    }
}

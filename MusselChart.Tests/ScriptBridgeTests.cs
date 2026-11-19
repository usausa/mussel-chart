namespace MusselChart;

using System.Reflection;
using System.Text.RegularExpressions;

using Microsoft.JSInterop;

using MusselChart.Internal;

public sealed partial class ScriptBridgeTests
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
        var names = Calls().Matches(script).Select(static x => x.Groups["name"].Value).Distinct().ToList();
        var entryPoints = typeof(ChartScriptBridge).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(static x => x.IsDefined(typeof(JSInvokableAttribute), false))
            .Select(static x => x.Name)
            .ToList();

        Assert.NotEmpty(names);
        Assert.All(names, name => Assert.Contains(name, entryPoints));
    }

    [Fact]
    public void EveryDataNameTheScriptReadsIsWrittenByAView()
    {
        var script = File.ReadAllText(Path.Combine(TestPaths.Root, "MusselChart", "wwwroot", "MusselChart.js"));
        var markup = String.Concat(Directory.GetFiles(Path.Combine(TestPaths.Root, "MusselChart"), "*.razor", SearchOption.AllDirectories).Select(File.ReadAllText));
        var written = DataAttributes().Matches(markup).Select(static x => Dash().Replace(x.Groups["name"].Value, static y => y.Groups["letter"].Value.ToUpperInvariant())).ToHashSet(StringComparer.Ordinal);
        var names = DataNames().Matches(script).Select(static x => x.Groups["name"].Value).Where(static x => x != "attached").Distinct().ToList();

        Assert.NotEmpty(names);
        Assert.All(names, name => Assert.Contains(name, written));
    }

    [GeneratedRegex(@"invokeMethodAsync\(\s*'(?<name>\w+)'")]
    private static partial Regex Calls();

    [GeneratedRegex(@"dataset\.(?<name>[a-zA-Z]+)")]
    private static partial Regex DataNames();

    [GeneratedRegex(@"\bdata-(?<name>[a-z-]+)=")]
    private static partial Regex DataAttributes();

    [GeneratedRegex("-(?<letter>[a-z])")]
    private static partial Regex Dash();
}

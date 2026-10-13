using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Shoko.Abstractions.Plugin;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Plugin;
using Xunit;

namespace Shoko.Tests.Plugin;

public class PluginReferenceCheckerTests
{
    private static string GetDllPath(string name)
        => Path.Combine(AppContext.BaseDirectory, name + ".dll");

    private static PluginReferenceChecker.AssemblySurface ReadSurface(Assembly assembly)
        => PluginReferenceChecker.AssemblySurface.Read(assembly.Location)!;

    private static Dictionary<string, PluginReferenceChecker.AssemblySurface> GetHostAssemblies()
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["Shoko.Abstractions"] = ReadSurface(typeof(IPlugin).Assembly),
            ["Shoko.QueueProcessor"] = ReadSurface(typeof(IQueueScheduler).Assembly),
            ["Shoko.Server"] = ReadSurface(typeof(PluginManager).Assembly),
        };

    [Theory]
    [InlineData("Shoko.Plugin.Tmdb")]
    [InlineData("Shoko.Plugin.WebAOM")]
    [InlineData("Shoko.Plugin.LegacyPlex")]
    [InlineData("Shoko.Tests")]
    public void FindMissingReferences_BuiltAgainstThisServer_FindsNothing(string name)
    {
        var missing = PluginReferenceChecker.FindMissingReferences(GetDllPath(name), GetHostAssemblies());

        Assert.Empty(missing);
    }

    [Fact]
    public void FindMissingReferences_HostWithoutTheTypes_ListsThem()
    {
        // Stands in for a contract that lost what the plugin was built against.
        var hostAssemblies = new Dictionary<string, PluginReferenceChecker.AssemblySurface>(StringComparer.OrdinalIgnoreCase)
        {
            ["Shoko.Abstractions"] = ReadSurface(typeof(object).Assembly),
        };

        var missing = PluginReferenceChecker.FindMissingReferences(GetDllPath("Shoko.Plugin.WebAOM"), hostAssemblies);

        Assert.Contains($"type {typeof(IPlugin).FullName}", missing);
    }

    [Fact]
    public void FindMissingReferences_TargetWithoutTheMethod_ListsIt()
    {
        // The server as it is, but for the one method this test assembly calls on the checker.
        var checker = typeof(PluginReferenceChecker).FullName!;
        var server = ReadSurface(typeof(PluginManager).Assembly);
        var types = server.Types.ToDictionary(pair => pair.Key, pair => pair.Value);
        types[checker] = new()
        {
            Methods = types[checker].Methods.Where(method => method.Name != nameof(PluginReferenceChecker.FindMissingReferences)).ToHashSet(),
            Fields = types[checker].Fields,
            Bases = types[checker].Bases,
        };
        var hostAssemblies = GetHostAssemblies();
        hostAssemblies["Shoko.Server"] = new() { Name = server.Name, Types = types, ForwardedTypes = server.ForwardedTypes };

        var missing = PluginReferenceChecker.FindMissingReferences(GetDllPath("Shoko.Tests"), hostAssemblies);

        Assert.Equal([$"method {checker}.{nameof(PluginReferenceChecker.FindMissingReferences)} with 2 parameters"], missing.Distinct());
    }
}

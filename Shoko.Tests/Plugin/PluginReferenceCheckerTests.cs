using System;
using System.Collections.Generic;
using System.IO;
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

    private static Dictionary<string, Assembly> GetHostAssemblies()
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["Shoko.Abstractions"] = typeof(IPlugin).Assembly,
            ["Shoko.QueueProcessor"] = typeof(IQueueScheduler).Assembly,
            ["Shoko.Server"] = typeof(PluginManager).Assembly,
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
        var hostAssemblies = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase)
        {
            ["Shoko.Abstractions"] = typeof(object).Assembly,
        };

        var missing = PluginReferenceChecker.FindMissingReferences(GetDllPath("Shoko.Plugin.WebAOM"), hostAssemblies);

        Assert.Contains($"type {typeof(IPlugin).FullName}", missing);
    }
}
